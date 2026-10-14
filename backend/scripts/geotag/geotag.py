import os
import csv
import piexif
from datetime import datetime
import sys
import argparse

def parse_time(time_str):
    """Parse datetime strings from CSV and EXIF metadata."""
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
    try:
        return datetime.fromisoformat(time_str)
    except Exception:
        pass
    raise ValueError(f"Unsupported timestamp format: {time_str}")

def to_exif_gps(decimal_degree):
    """Convert decimal degrees to EXIF GPS rational format (deg, min, sec)."""
    degrees = int(decimal_degree)
    minutes = int((decimal_degree - degrees) * 60)
    seconds = round((decimal_degree - degrees - minutes / 60) * 3600 * 10000)
    return ((degrees, 1), (minutes, 1), (seconds, 10000))

def main():
    parser = argparse.ArgumentParser(description="Inject GPS EXIF tags into mission images using flight log CSV.")
    parser.add_argument("image_folder", help="Directory containing drone images (.jpg, .jpeg)")
    parser.add_argument("log_file", help="CSV file containing flight log (timestamp, lat, lon, alt)")
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
        if reader.fieldnames:
            reader.fieldnames = [fn.strip() for fn in reader.fieldnames]

        for row in reader:
            try:
                raw_lat = row.get("lat") or row.get("latitude") or row.get("Latitude")
                raw_lon = row.get("lon") or row.get("longitude") or row.get("Longitude")
                raw_alt = row.get("alt") or row.get("altitude") or row.get("AltitudeM") or "0"
                raw_time = row.get("timestamp") or row.get("time") or row.get("RecordedAt")

                if not raw_lat or not raw_lon or not raw_time:
                    continue

                lat = float(raw_lat)
                lon = float(raw_lon)
                alt = float(raw_alt)
                dt = parse_time(raw_time)

                log_records.append({
                    "time": dt,
                    "lat": lat,
                    "lon": lon,
                    "alt": alt
                })
            except Exception:
                continue

    if not log_records:
        print(f"Error: No valid GPS records parsed from {args.log_file}", file=sys.stderr)
        sys.exit(1)

    # Sort log records by time
    log_records.sort(key=lambda x: x["time"])
    print(f"Loaded {len(log_records)} GPS points from {os.path.basename(args.log_file)} (Time range: {log_records[0]['time']} -> {log_records[-1]['time']})")

    # 2. Process images
    image_files = [f for f in os.listdir(args.image_folder) if f.lower().endswith((".jpg", ".jpeg"))]
    if not image_files:
        print(f"Warning: No JPG/JPEG images found in {args.image_folder}")
        sys.exit(0)

    sorted_image_files = sorted(image_files)

    # Detect whether DateTimeOriginal is static across images (common in GoPro timelapse/burst mode)
    use_0th_datetime = False
    if len(sorted_image_files) >= 2:
        try:
            exif0 = piexif.load(os.path.join(args.image_folder, sorted_image_files[0]))
            exif1 = piexif.load(os.path.join(args.image_folder, sorted_image_files[1]))
            dto0 = exif0.get("Exif", {}).get(piexif.ExifIFD.DateTimeOriginal)
            dto1 = exif1.get("Exif", {}).get(piexif.ExifIFD.DateTimeOriginal)
            dt0th0 = exif0.get("0th", {}).get(piexif.ImageIFD.DateTime)
            dt0th1 = exif1.get("0th", {}).get(piexif.ImageIFD.DateTime)

            if dto0 and dto1 and dto0 == dto1 and dt0th0 and dt0th1 and dt0th0 != dt0th1:
                use_0th_datetime = True
                print("Detected static DateTimeOriginal (GoPro timelapse mode). Using per-frame 0th.DateTime instead.")
        except Exception:
            pass

    def extract_time_str(exif):
        if use_0th_datetime:
            if "0th" in exif and piexif.ImageIFD.DateTime in exif["0th"]:
                return exif["0th"][piexif.ImageIFD.DateTime].decode("utf-8", errors="ignore")
        if "Exif" in exif and piexif.ExifIFD.DateTimeOriginal in exif["Exif"]:
            return exif["Exif"][piexif.ExifIFD.DateTimeOriginal].decode("utf-8", errors="ignore")
        if "0th" in exif and piexif.ImageIFD.DateTime in exif["0th"]:
            return exif["0th"][piexif.ImageIFD.DateTime].decode("utf-8", errors="ignore")
        return None

    # Auto-detect camera timezone offset when offset is 0.0
    effective_offset = args.offset
    if effective_offset == 0.0 and sorted_image_files:
        for sample_fn in sorted_image_files[:5]:
            sample_path = os.path.join(args.image_folder, sample_fn)
            try:
                sample_exif = piexif.load(sample_path)
                sample_time_str = extract_time_str(sample_exif)
                if sample_time_str:
                    s_dt = parse_time(sample_time_str)
                    min_diff_0 = min(abs(r["time"].timestamp() - s_dt.timestamp()) for r in log_records)
                    if min_diff_0 > args.max_diff:
                        candidate_offset = round((s_dt.timestamp() - log_records[0]["time"].timestamp()) / 1800) * 1800
                        min_diff_candidate = min(abs(r["time"].timestamp() - (s_dt.timestamp() - candidate_offset)) for r in log_records)
                        if min_diff_candidate <= args.max_diff:
                            effective_offset = float(candidate_offset)
                            print(f"Auto-detected camera timezone offset: {candidate_offset / 3600:+.1f} hours ({effective_offset}s)")
                            break
            except Exception:
                continue

    success_count = 0
    fail_count = 0

    for filename in sorted_image_files:
        filepath = os.path.join(args.image_folder, filename)
        try:
            exif_dict = piexif.load(filepath)
            img_time_str = extract_time_str(exif_dict)

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
        target_time = img_dt.timestamp() - effective_offset
        nearest_log = min(log_records, key=lambda x: abs(x["time"].timestamp() - target_time))
        diff_seconds = abs(nearest_log["time"].timestamp() - target_time)

        if diff_seconds > args.max_diff:
            print(f"Skip {filename}: Nearest log point differs by {diff_seconds:.2f}s (> {args.max_diff}s limit). Image time: {img_dt}, Nearest GPS time: {nearest_log['time']}")
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

        # Sync DateTimeOriginal and DateTimeDigitized so NodeODM/OpenSfM reads correct per-image timestamps
        formatted_time = img_dt.strftime("%Y:%m:%d %H:%M:%S").encode("utf-8")
        if "Exif" not in exif_dict or not isinstance(exif_dict["Exif"], dict):
            exif_dict["Exif"] = {}
        exif_dict["Exif"][piexif.ExifIFD.DateTimeOriginal] = formatted_time
        exif_dict["Exif"][piexif.ExifIFD.DateTimeDigitized] = formatted_time

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
        print("Warning: Failed to geotag any images. Check timestamp sync or offset.", file=sys.stderr)
        sys.exit(2)

    sys.exit(0)

if __name__ == "__main__":
    main()
