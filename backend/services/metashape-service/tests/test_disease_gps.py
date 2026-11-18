"""GPS regressions; no reconstruction, inference or object-storage calls."""
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

from PIL import Image

import main
from models import DiseaseDetectMissionRequest, ImageItem


class DiseaseGpsTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.image = Path(self.directory.name) / "capture.jpg"

    def write_image(self, gps=None):
        exif = Image.Exif()
        if gps is not None:
            exif[34853] = gps
        Image.new("RGB", (16, 16)).save(self.image, exif=exif)

    def test_reads_gps_without_optional_piexif(self):
        self.write_image({1: "N", 2: (10.0, 27.0, 42.0),
                          3: "E", 4: (106.0, 29.0, 38.0)})
        with patch.dict("sys.modules", {"piexif": None}):
            latitude, longitude = main.extract_exif_gps(str(self.image))
        self.assertAlmostEqual(latitude, 10.4616666667)
        self.assertAlmostEqual(longitude, 106.4938888889)

    def test_respects_southern_and_western_references(self):
        self.write_image({1: "S", 2: (10.0, 0.0, 0.0),
                          3: "W", 4: (106.0, 0.0, 0.0)})
        self.assertEqual(main.extract_exif_gps(str(self.image)), (-10.0, -106.0))

    def test_missing_incomplete_and_invalid_gps_are_unavailable(self):
        for gps in (None, {1: "N", 2: (10.0, 0.0, 0.0)},
                    {1: "X", 2: (10.0, 0.0, 0.0), 3: "E", 4: (106.0, 0.0, 0.0)},
                    {1: "N", 2: (91.0, 0.0, 0.0), 3: "E", 4: (106.0, 0.0, 0.0)}):
            with self.subTest(gps=gps):
                self.write_image(gps)
                self.assertIsNone(main.extract_exif_gps(str(self.image)))

    def test_explicit_zero_coordinates_are_not_missing_metadata(self):
        self.write_image({1: "N", 2: (0.0, 0.0, 0.0),
                          3: "E", 4: (0.0, 0.0, 0.0)})
        self.assertEqual(main.extract_exif_gps(str(self.image)), (0.0, 0.0))

    def test_missing_gps_stops_before_inference_or_export(self):
        self.write_image()
        detector = Mock(names={}, ckpt_path="best_disease.pt",
                        predict=Mock(return_value=[SimpleNamespace(boxes=[])]))
        storage = Mock()
        request = DiseaseDetectMissionRequest(
            mission_id="missing-gps-test", tenant_id="test",
            images=[ImageItem(storage_key=str(self.image), url=None)])
        with patch.object(main.settings, "temp_dir", self.directory.name), \
                patch.object(main, "get_disease_model", return_value=detector), \
                patch.object(main, "minio_storage", storage):
            with self.assertRaises(main.HTTPException) as error:
                main.detect_mission_diseases(request)
        self.assertEqual(error.exception.status_code, 422)
        self.assertIn("GPS", error.exception.detail)
        detector.predict.assert_not_called()
        storage.upload_file.assert_not_called()


if __name__ == "__main__":
    unittest.main()
