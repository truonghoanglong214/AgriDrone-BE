import os
import csv
import piexif
from datetime import datetime
import sys
import argparse

def parse_time(time_str):
    """Parse flexible datetime strings from flight logs and EXIF metadata."""
    if not time_str:
        raise ValueError("Empty time string")
    time_str = time_str.strip().strip('\x00')
    formats = [
        "%Y-%m-%d %H:%M:%S",
        "%Y:%m:%d %H:%M:%S",
        "%Y-%m-%dT%H:%M:%S",
        "%Y-%m-%d %H:%M:%S.%f",
        "%Y-%m-%dT%H:%M:%S.%fZ",
        "%Y-%m-%dT%H:%M:%S%z",
        "%Y/%m/%d %H:%M:%S"
    ]
    for fmt in formats:
        try:
            return datetime.strptime(time_str, fmt)
        except ValueError:
            continue
    raise ValueError(f"Unsupported timestamp format: {time_str}")

def to_exif_gps(decimal_degree):
    """Convert decimal degrees to EXIF GPS rational format (deg, min, sec)."""
    degrees = int(decimal_degree)
    minutes = int((decimal_degree - degrees) * 60)
    seconds = round((decimal_degree - degrees - minutes / 60) * 3600 * 10000)
    return ((degrees, 1), (minutes, 1), (seconds, 10000))

def main():
    parser = argparse.ArgumentParser(description="Inject GPS EXIF tags into mission images using drone flight logs.")
    parser.add_argument("image_folder", help="Directory containing drone images (.jpg, .jpeg)")
    parser.add_argument("log_file", help="CSV file containing flight log with timestamp, lat, lon, alt")
    parser.add_argument("--offset", type=float, default=0.0, help="Time offset in seconds between camera clock and drone clock (offset = camera_time - drone_time)")
    parser.add_argument("--max-diff", type=float, default=3.0, help="Maximum allowed time difference in seconds to match a log point (default: 3.0s)")

    args = parser.parse_args()

    if not os.path.isdir(args.image_folder):
        print(f"Error: Image directory not found: {args.image_folder}", file=sys.stderr)
        sys.exit(1)

    if not os.path.isfile(args.log_file):
        print(f"Error: Log file not found: {args.log_file}", file=sys.stderr)
        sys.exit(1)

    # 1. Parse CSV log file
    log_records = []
    with open(args.log_file, "r", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        for row in reader:
            try:
                # Support standard headers: timestamp, lat/latitude, lon/longitude, alt/altitude
                raw_time = row.get("timestamp") or row.get("time") or row.get("RecordedAt")
                raw_lat = row.get("lat") or row.get("latitude") or row.get("Latitude")
                raw_lon = row.get("lon") or row.get("longitude") or row.get("Longitude")
                raw_alt = row.get("alt") or row.get("altitude") or row.get("AltitudeM") or "0"

                if not raw_time or not raw_lat or not raw_lon:
                    continue

                dt = parse_time(raw_time)
                log_records.append({
                    "time": dt,
                    "lat": float(raw_lat),
                    "lon": float(raw_lon),
                    "alt": float(raw_alt)
                })
            except Exception:
                continue

    if not log_records:
        print(f"Error: No valid GPS records parsed from {args.log_file}", file=sys.stderr)
        sys.exit(1)

    # Sort log records by time
    log_records.sort(key=lambda x: x["time"])

    # 2. Process images
    image_files = [f for f in os.listdir(args.image_folder) if f.lower().endswith((".jpg", ".jpeg"))]
    if not image_files:
        print(f"Warning: No JPG/JPEG images found in {args.image_folder}")
        sys.exit(0)

    success_count = 0
    fail_count = 0

    for filename in sorted(image_files):
        filepath = os.path.join(args.image_folder, filename)
        try:
            exif_dict = piexif.load(filepath)
            img_time_str = None

            if "Exif" in exif_dict and piexif.ExifIFD.DateTimeOriginal in exif_dict["Exif"]:
                img_time_str = exif_dict["Exif"][piexif.ExifIFD.DateTimeOriginal].decode("utf-8", errors="ignore")
            elif "0th" in exif_dict and piexif.ImageIFD.DateTime in exif_dict["0th"]:
                img_time_str = exif_dict["0th"][piexif.ImageIFD.DateTime].decode("utf-8", errors="ignore")

            if not img_time_str:
                print(f"Skip {filename}: No DateTimeOriginal or DateTime found in EXIF.")
                fail_count += 1
                continue

            img_dt = parse_time(img_time_str)
        except Exception as e:
            print(f"Skip {filename}: Cannot read EXIF datetime: {e}")
            fail_count += 1
            continue

        # Find closest record taking offset into account
        # target_drone_time = img_dt - offset
        target_time = img_dt.timestamp() - args.offset
        nearest_log = min(log_records, key=lambda x: abs(x["time"].timestamp() - target_time))
        diff_seconds = abs(nearest_log["time"].timestamp() - target_time)

        if diff_seconds > args.max_diff:
            print(f"Skip {filename}: Nearest log point differs by {diff_seconds:.2f}s (> {args.max_diff}s limit).")
            fail_count += 1
            continue

        # Inject GPS EXIF
        lat = nearest_log["lat"]
        lon = nearest_log["lon"]
        alt = nearest_log["alt"]

        gps_ifd = {
            piexif.GPSIFD.GPSLatitudeRef: 'N'.encode() if lat >= 0 else 'S'.encode(),
            piexif.GPSIFD.GPSLatitude: to_exif_gps(abs(lat)),
            piexif.GPSIFD.GPSLongitudeRef: 'E'.encode() if lon >= 0 else 'W'.encode(),
            piexif.GPSIFD.GPSLongitude: to_exif_gps(abs(lon)),
            piexif.GPSIFD.GPSAltitudeRef: 0 if alt >= 0 else 1,
            piexif.GPSIFD.GPSAltitude: (int(abs(alt) * 100), 100)
        }

        exif_dict["GPS"] = gps_ifd
        try:
            exif_bytes = piexif.dump(exif_dict)
            piexif.insert(exif_bytes, filepath)
            print(f"Tagged {filename} (diff: {diff_seconds:.2f}s) -> Lat: {lat:.6f}, Lon: {lon:.6f}, Alt: {alt:.1f}m")
            success_count += 1
        except Exception as e:
            print(f"Error inserting EXIF into {filename}: {e}", file=sys.stderr)
            fail_count += 1

    print(f"Completed geotagging: {success_count} succeeded, {fail_count} failed/skipped out of {len(image_files)} images.")
    if success_count == 0 and len(image_files) > 0:
        print("Warning: Failed to geotag any images. Check timestamp sync or timezone offset.", file=sys.stderr)
        sys.exit(2)

    sys.exit(0)

if __name__ == "__main__":
    main()
