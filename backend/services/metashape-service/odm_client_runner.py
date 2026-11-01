import argparse
import glob
import os
import sys
import time

# Add current directory to sys.path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from odm_workflow import run_odm_orthophoto_pipeline


def progress_printer(percent: int, message: str):
    bar_length = 30
    filled = int(bar_length * percent / 100)
    bar = "=" * filled + "-" * (bar_length - filled)
    print(f"\r[{bar}] {percent}% | {message}", end="", flush=True)
    if percent == 100:
        print()


def main():
    parser = argparse.ArgumentParser(
        description="Script Python gọi NodeODM để ghép ảnh drone ra bản đồ GeoTIFF chất lượng cao (tương đương Metashape)."
    )
    parser.add_argument(
        "--input-dir", "-i",
        required=True,
        help="Đường dẫn thư mục chứa ảnh drone đầu vào (.jpg, .jpeg, .tif)"
    )
    parser.add_argument(
        "--output-dir", "-o",
        default="./output_odm",
        help="Thư mục xuất file kết quả (mặc định: ./output_odm)"
    )
    parser.add_argument(
        "--host",
        default="localhost",
        help="Địa chỉ NodeODM (mặc định: localhost)"
    )
    parser.add_argument(
        "--port", "-p",
        type=int,
        default=3000,
        help="Cổng NodeODM (mặc định: 3000)"
    )

    args = parser.parse_args()

    input_dir = os.path.abspath(args.input_dir)
    output_dir = os.path.abspath(args.output_dir)
    os.makedirs(output_dir, exist_ok=True)

    output_tif = os.path.join(output_dir, "orthomosaic.tif")
    output_jpg = os.path.join(output_dir, "orthomosaic_preview.jpg")

    # Quét danh sách ảnh
    extensions = ("*.jpg", "*.jpeg", "*.JPG", "*.JPEG", "*.tif", "*.TIF")
    photo_paths = []
    for ext in extensions:
        photo_paths.extend(glob.glob(os.path.join(input_dir, ext)))

    photo_paths = sorted(list(set(photo_paths)))

    if not photo_paths:
        print(f"[!] Lỗi: Không tìm thấy ảnh nào trong thư mục: {input_dir}")
        sys.exit(1)

    print(f"=== BẮT ĐẦU PIPELINE PYODM (CHẤT LƯỢNG CAO NHẤT) ===")
    print(f"- Số lượng ảnh đầu vào: {len(photo_paths)}")
    print(f"- NodeODM: http://{args.host}:{args.port}")
    print(f"- File xuất TIF: {output_tif}")
    print(f"- File xuất JPG: {output_jpg}")
    print("=" * 55)

    start_time = time.time()
    try:
        bounds, corners, width, height = run_odm_orthophoto_pipeline(
            photo_paths=photo_paths,
            output_tif_path=output_tif,
            output_jpg_path=output_jpg,
            node_host=args.host,
            node_port=args.port,
            progress_callback=progress_printer
        )

        elapsed = time.time() - start_time
        file_size_mb = os.path.getsize(output_tif) / (1024 * 1024)

        print("\n\n" + "=" * 55)
        print(" THÀNH CÔNG!")
        print(f"- Thời gian xử lý: {elapsed / 60:.1f} phút")
        print(f"- Kích thước GeoTIFF: {file_size_mb:.2f} MB ({width} x {height} px)")
        print(f"- Bounding Box (WGS84): {bounds}")
        print(f"- 4 góc GPS (Corners): {corners}")
        print(f"- File TIF đã lưu tại: {output_tif}")
        print("=" * 55)

    except Exception as ex:
        print(f"\n[!] Thất bại trong quá trình xử lý: {ex}")
        sys.exit(1)


if __name__ == "__main__":
    main()

