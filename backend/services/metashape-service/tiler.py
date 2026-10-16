import argparse
import json
import logging
import os
import sys
from typing import Any, Dict, List, Optional, Tuple
import numpy as np
from PIL import Image
import rasterio
from rasterio.warp import transform_bounds
from rasterio.windows import Window, bounds as window_bounds
try:
    from shapely.geometry import box, shape
except ImportError:
    box = None
    shape = None

logger = logging.getLogger("tiler")


def slice_geotiff(
    tif_path: str,
    output_dir: str,
    tile_size: int = 640,
    overlap: int = 64,
    polygon_geojson: Optional[Dict[str, Any]] = None,
    image_format: str = "JPEG",
    quality: int = 90,
    min_valid_pixel_ratio: float = 0.05
) -> Tuple[int, List[Dict[str, Any]]]:
    """
    Slices a large GeoTIFF into manageable chips using rasterio Windowed Reading.
    
    Features:
    - Low RAM consumption (< 200MB) via Windowed Reading.
    - Grid Tiling with configurable tile size and overlap (stride).
    - Optional Polygon filtering (skips tiles outside the specified farm boundary).
    - Ignores nodata/empty background tiles.
    - Computes exact GPS coordinates and Affine Transform for each tile so YOLO bounding
      boxes can be accurately mapped back to real-world latitude/longitude coordinates.
    """
    if not os.path.exists(tif_path):
        raise FileNotFoundError(f"Input GeoTIFF not found: {tif_path}")

    os.makedirs(output_dir, exist_ok=True)

    polygon_geom = None
    if polygon_geojson:
        try:
            # Handle both FeatureCollection, Feature, or direct Geometry dict
            if polygon_geojson.get("type") == "FeatureCollection" and polygon_geojson.get("features"):
                geom_dict = polygon_geojson["features"][0].get("geometry")
            elif polygon_geojson.get("type") == "Feature":
                geom_dict = polygon_geojson.get("geometry")
            else:
                geom_dict = polygon_geojson

            if geom_dict:
                polygon_geom = shape(geom_dict)
                logger.info(f"Loaded polygon filter geometry: {polygon_geom.geom_type}")
        except Exception as e:
            logger.warning(f"Failed to parse polygon GeoJSON filter: {e}. Slicing entire grid.")

    tiles_manifest = []
    stride = tile_size - overlap
    if stride <= 0:
        stride = tile_size

    with rasterio.open(tif_path) as src:
        total_w = src.width
        total_h = src.height
        src_crs = src.crs
        src_transform = src.transform
        band_count = src.count

        logger.info(
            f"Slicing GeoTIFF ({total_w}x{total_h}, bands={band_count}) into {tile_size}x{tile_size} tiles (stride={stride})"
        )

        tile_index = 0
        for row_off in range(0, total_h, stride):
            for col_off in range(0, total_w, stride):
                # Calculate window dimensions (clamp at image boundaries)
                w = min(tile_size, total_w - col_off)
                h = min(tile_size, total_h - row_off)

                if w <= 0 or h <= 0:
                    continue

                win = Window(col_off=col_off, row_off=row_off, width=w, height=h)

                # Compute geographic bounds for this window
                win_left, win_bottom, win_right, win_top = window_bounds(win, src_transform)

                # Convert to EPSG:4326 if needed
                if src_crs and src_crs.to_string() != "EPSG:4326":
                    min_lon, min_lat, max_lon, max_lat = transform_bounds(
                        src_crs, "EPSG:4326", win_left, win_bottom, win_right, win_top
                    )
                else:
                    min_lon, min_lat, max_lon, max_lat = win_left, win_bottom, win_right, win_top

                # If polygon boundary filter is provided, check intersection
                if polygon_geom is not None:
                    tile_box = box(min_lon, min_lat, max_lon, max_lat)
                    if not tile_box.intersects(polygon_geom):
                        continue

                # Read only this specific window into memory (Windowed Reading)
                if band_count >= 3:
                    bands_data = src.read((1, 2, 3), window=win)
                    img_array = np.dstack([bands_data[0], bands_data[1], bands_data[2]])
                elif band_count == 1:
                    band1 = src.read(1, window=win)
                    img_array = np.dstack([band1, band1, band1])
                else:
                    band1 = src.read(1, window=win)
                    img_array = np.dstack([band1] * 3)

                # Check if tile has valid content (not purely nodata or black)
                valid_mask = img_array > 0
                valid_pixel_ratio = np.mean(valid_mask)
                if valid_pixel_ratio < min_valid_pixel_ratio:
                    continue

                # Pad to square tile_size x tile_size if edge tile is smaller
                if w < tile_size or h < tile_size:
                    padded_array = np.zeros((tile_size, tile_size, 3), dtype=img_array.dtype)
                    padded_array[0:h, 0:w, :] = img_array
                    img_array = padded_array

                # Ensure uint8
                if img_array.dtype != np.uint8:
                    c_min, c_max = img_array.min(), img_array.max()
                    if c_max > c_min:
                        img_array = np.clip((img_array - c_min) / (c_max - c_min) * 255, 0, 255).astype(np.uint8)
                    else:
                        img_array = np.zeros_like(img_array, dtype=np.uint8)

                ext = "jpg" if image_format.upper() == "JPEG" else "png"
                tile_filename = f"tile_{tile_index:04d}_r{row_off}_c{col_off}.{ext}"
                tile_local_path = os.path.join(output_dir, tile_filename)

                img = Image.fromarray(img_array)
                if image_format.upper() == "JPEG":
                    img.save(tile_local_path, "JPEG", quality=quality, optimize=True)
                else:
                    img.save(tile_local_path, "PNG")

                # Affine transform for this specific tile
                # Pixel (px, py) on tile -> global image coordinate: (col_off + px, row_off + py)
                tile_transform = rasterio.windows.transform(win, src_transform)

                tile_meta = {
                    "tile_index": tile_index,
                    "file_name": tile_filename,
                    "local_path": tile_local_path,
                    "col_off": col_off,
                    "row_off": row_off,
                    "width": w,
                    "height": h,
                    "bounds": {
                        "min_lat": round(min_lat, 7),
                        "max_lat": round(max_lat, 7),
                        "min_lon": round(min_lon, 7),
                        "max_lon": round(max_lon, 7),
                    },
                    "corners": {
                        "north_west": [round(max_lat, 7), round(min_lon, 7)],
                        "north_east": [round(max_lat, 7), round(max_lon, 7)],
                        "south_east": [round(min_lat, 7), round(max_lon, 7)],
                        "south_west": [round(min_lat, 7), round(min_lon, 7)],
                    },
                    "affine_transform": [
                        tile_transform.a, tile_transform.b, tile_transform.c,
                        tile_transform.d, tile_transform.e, tile_transform.f
                    ]
                }
                tiles_manifest.append(tile_meta)
                tile_index += 1

    manifest_path = os.path.join(output_dir, "manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(tiles_manifest, f, indent=2)

    logger.info(f"Tiling complete: generated {len(tiles_manifest)} valid tiles. Saved to {output_dir}")
    return len(tiles_manifest), tiles_manifest


def main():
    parser = argparse.ArgumentParser(description="Slice GeoTIFF into smaller tiles for YOLO object detection.")
    parser.add_argument("--input", "-i", required=True, help="Path to input GeoTIFF file (.tif)")
    parser.add_argument("--output-dir", "-o", required=True, help="Directory to save sliced tiles")
    parser.add_argument("--tile-size", type=int, default=640, help="Tile width/height in pixels (default: 640)")
    parser.add_argument("--overlap", type=int, default=64, help="Tile overlap in pixels (default: 64)")
    parser.add_argument("--polygon", "-p", help="Optional GeoJSON file containing farm boundary polygon")

    args = parser.parse_args()

    polygon_geojson = None
    if args.polygon and os.path.exists(args.polygon):
        with open(args.polygon, "r", encoding="utf-8") as pf:
            polygon_geojson = json.load(pf)

    total, manifest = slice_geotiff(
        tif_path=args.input,
        output_dir=args.output_dir,
        tile_size=args.tile_size,
        overlap=args.overlap,
        polygon_geojson=polygon_geojson
    )
    print(json.dumps({"total_tiles": total, "output_dir": args.output_dir}))


if __name__ == "__main__":
    main()
