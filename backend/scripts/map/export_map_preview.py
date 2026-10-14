"""
Export map preview and extract 4 GPS corners from a GeoTIFF file.
Requirements: rasterio, Pillow, numpy
"""

import argparse
import json
import os
import sys
import numpy as np
from PIL import Image

try:
    import rasterio
    from rasterio.warp import transform_bounds
except ImportError as e:
    sys.stderr.write(f"Missing required library: {e}\n")
    sys.exit(1)


def export_preview(input_tif: str, output_jpg: str, max_size: int = 2048, quality: int = 85):
    if not os.path.exists(input_tif):
        sys.stderr.write(f"File not found: {input_tif}\n")
        sys.exit(1)

    with rasterio.open(input_tif) as src:
        bounds = src.bounds
        src_crs = src.crs

        # Transform bounds to WGS84 (EPSG:4326) if needed
        if src_crs and src_crs.to_string() != "EPSG:4326":
            min_lon, min_lat, max_lon, max_lat = transform_bounds(
                src_crs, "EPSG:4326", bounds.left, bounds.bottom, bounds.right, bounds.top
            )
        else:
            min_lon, min_lat, max_lon, max_lat = bounds.left, bounds.bottom, bounds.right, bounds.top

        # Compute 4 corner GPS coordinates: [Lat, Lon]
        corners = {
            "north_west": [round(max_lat, 7), round(min_lon, 7)],
            "north_east": [round(max_lat, 7), round(max_lon, 7)],
            "south_east": [round(min_lat, 7), round(max_lon, 7)],
            "south_west": [round(min_lat, 7), round(min_lon, 7)],
        }

        bbox = {
            "min_lat": round(min_lat, 7),
            "max_lat": round(max_lat, 7),
            "min_lon": round(min_lon, 7),
            "max_lon": round(max_lon, 7),
        }

        # Read bands for image preview (RGB)
        count = src.count
        if count >= 3:
            bands = [src.read(i) for i in (1, 2, 3)]
            img_array = np.dstack(bands)
        elif count == 1:
            band = src.read(1)
            img_array = np.dstack([band, band, band])
        else:
            bands = [src.read(1)] * 3
            img_array = np.dstack(bands)

        # Handle nodata or alpha if present
        if img_array.dtype != np.uint8:
            # Normalize to 0-255 if needed
            valid_mask = img_array > 0
            if valid_mask.any():
                c_min = img_array[valid_mask].min()
                c_max = img_array[valid_mask].max()
                if c_max > c_min:
                    img_array = np.clip((img_array - c_min) / (c_max - c_min) * 255, 0, 255).astype(np.uint8)
                else:
                    img_array = np.zeros_like(img_array, dtype=np.uint8)
            else:
                img_array = np.zeros_like(img_array, dtype=np.uint8)

        img = Image.fromarray(img_array)

        # Downscale if larger than max_size to keep file lightweight (~2-3MB)
        orig_w, orig_h = img.size
        scale = min(1.0, max_size / max(orig_w, orig_h))
        new_w = int(orig_w * scale)
        new_h = int(orig_h * scale)
        if scale < 1.0:
            img = img.resize((new_w, new_h), Image.Resampling.LANCZOS)

        os.makedirs(os.path.dirname(os.path.abspath(output_jpg)), exist_ok=True)
        img.save(output_jpg, "JPEG", quality=quality, optimize=True)

        result = {
            "success": True,
            "preview_path": output_jpg,
            "original_width": orig_w,
            "original_height": orig_h,
            "preview_width": new_w,
            "preview_height": new_h,
            "bounds": bbox,
            "corners": corners,
        }

        print(json.dumps(result))


def main():
    parser = argparse.ArgumentParser(description="Export map preview and 4 corners from GeoTIFF")
    parser.add_argument("--input", "-i", required=True, help="Input GeoTIFF file path")
    parser.add_argument("--output", "-o", required=True, help="Output JPEG preview file path")
    parser.add_argument("--max-size", type=int, default=2048, help="Max dimension for preview image")
    parser.add_argument("--quality", type=int, default=85, help="JPEG quality (default: 85)")

    args = parser.parse_args()
    try:
        export_preview(args.input, args.output, args.max_size, args.quality)
    except Exception as ex:
        sys.stderr.write(f"Error processing GeoTIFF: {ex}\n")
        sys.exit(2)


if __name__ == "__main__":
    main()
