import os
import shutil
import tempfile
import pytest
from tiler import slice_geotiff

SAMPLE_TIF = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "scratch", "test_orthophoto.tif"))

def test_slice_geotiff_grid_mode():
    assert os.path.exists(SAMPLE_TIF), f"Sample GeoTIFF not found at {SAMPLE_TIF}"
    temp_dir = tempfile.mkdtemp()
    try:
        total, manifest = slice_geotiff(
            tif_path=SAMPLE_TIF,
            output_dir=temp_dir,
            tile_size=640,
            overlap=64
        )
        assert total > 0
        assert len(manifest) == total

        for tile in manifest:
            assert "tile_index" in tile
            assert "col_off" in tile
            assert "row_off" in tile
            assert "bounds" in tile
            assert "corners" in tile
            assert "affine_transform" in tile
            assert len(tile["affine_transform"]) == 6
            assert os.path.exists(tile["local_path"])
            assert tile["bounds"]["min_lat"] < tile["bounds"]["max_lat"]
            assert tile["bounds"]["min_lon"] < tile["bounds"]["max_lon"]

        manifest_file = os.path.join(temp_dir, "manifest.json")
        assert os.path.exists(manifest_file)
    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)


def test_slice_geotiff_polygon_filter():
    assert os.path.exists(SAMPLE_TIF)
    temp_dir = tempfile.mkdtemp()
    try:
        # A small polygon located in the north-west corner of the sample GeoTIFF
        # Sample coords: lat ~10.4613 - 10.4616, lon ~106.4938 - 106.4944
        nw_polygon = {
            "type": "Polygon",
            "coordinates": [[
                [106.4938, 10.4615],
                [106.4940, 10.4615],
                [106.4940, 10.4616],
                [106.4938, 10.4616],
                [106.4938, 10.4615]
            ]]
        }

        total_filtered, manifest_filtered = slice_geotiff(
            tif_path=SAMPLE_TIF,
            output_dir=temp_dir,
            tile_size=640,
            overlap=64,
            polygon_geojson=nw_polygon
        )

        total_unfiltered, _ = slice_geotiff(
            tif_path=SAMPLE_TIF,
            output_dir=os.path.join(temp_dir, "unfiltered"),
            tile_size=640,
            overlap=64
        )

        # Polygon filtering should produce fewer tiles than full grid
        assert total_filtered <= total_unfiltered
        assert total_filtered > 0

    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)


def test_slice_geotiff_missing_file():
    with pytest.raises(FileNotFoundError):
        slice_geotiff(
            tif_path="non_existent_file.tif",
            output_dir="some_dir"
        )
