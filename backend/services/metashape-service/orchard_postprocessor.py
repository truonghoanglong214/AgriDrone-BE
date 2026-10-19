import math
import logging
from typing import List, Dict, Any, Optional, Tuple
import numpy as np

logger = logging.getLogger("orchard-postprocessor")

def latlon_to_meters(lat: float, lon: float, ref_lat: float, ref_lon: float) -> Tuple[float, float]:
    """
    Chuyen doi kinh vi do (WGS84) sang toa do phang met (x, y) cuc bo quanh tam vuon.
    x: truc Dong-Tay (m)
    y: truc Bac-Nam (m)
    """
    rad = math.pi / 180.0
    x = (lon - ref_lon) * 111320.0 * math.cos(ref_lat * rad)
    y = (lat - ref_lat) * 110540.0
    return x, y

def meters_to_latlon(x: float, y: float, ref_lat: float, ref_lon: float) -> Tuple[float, float]:
    """
    Chuyen doi nguoc tu toa do met (x, y) sang WGS84 (lat, lon).
    """
    rad = math.pi / 180.0
    lat = ref_lat + (y / 110540.0)
    lon = ref_lon + (x / (111320.0 * math.cos(ref_lat * rad)))
    return round(lat, 7), round(lon, 7)

def pixel_to_gps(px: float, py: float, affine: List[float]) -> Tuple[float, float]:
    """
    Chuyen doi toa do pixel (px, py) tren tile sang GPS (lat, lon) dung affine transform.
    affine: [a, b, c, d, e, f]
    lon = a * px + b * py + c
    lat = d * px + e * py + f
    """
    a, b, c, d, e, f = affine
    lon = a * px + b * py + c
    lat = d * px + e * py + f
    return round(lat, 7), round(lon, 7)


def is_inside_polygon(lat: float, lon: float, polygon_geojson: Optional[Dict[str, Any]]) -> bool:
    """
    Kiem tra toa do (lat, lon) co nam ben trong polygon viền vuon khong.
    GeoJSON dung he toa do [lon, lat].
    Kiem tra hinh hoc chinh xac theo ranh gioi nguoi dung da khoanh.
    """
    if not polygon_geojson:
        return True
    try:
        from shapely.geometry import shape, Point
        geom = polygon_geojson.get("geometry", polygon_geojson)
        poly = shape(geom)
        return poly.contains(Point(lon, lat))
    except Exception as e:
        logger.warning(f"Error checking point in polygon: {e}")
        return True


def deduplicate_trees(
    raw_trees: List[Dict[str, Any]],
    min_distance_meters: float = 1.2
) -> List[Dict[str, Any]]:
    """
    Loc bo cac diem nhan dien bi trung lap hoac qua gan nhau (Spatial NMS):
    - Doi sang he toa do met.
    - Sap xep theo do tin cay (confidence) giam dan.
    - Neu 2 diem cach nhau < min_distance_meters (mac dinh 1.2m), giu lai diem co confidence cao hon.
    """
    if not raw_trees:
        return []

    ref_lat = raw_trees[0]["lat"]
    ref_lon = raw_trees[0]["lon"]

    metric_points = []
    for t in raw_trees:
        x, y = latlon_to_meters(t["lat"], t["lon"], ref_lat, ref_lon)
        metric_points.append({
            **t,
            "mx": x,
            "my": y,
            "conf": t.get("confidence", 1.0)
        })

    # Sort theo confidence cao nhat dung truoc
    metric_points.sort(key=lambda item: item["conf"], reverse=True)

    kept = []
    for candidate in metric_points:
        is_dup = False
        for k in kept:
            dist = math.hypot(candidate["mx"] - k["mx"], candidate["my"] - k["my"])
            if dist < min_distance_meters:
                is_dup = True
                break
        if not is_dup:
            kept.append(candidate)

    logger.info(f"Deduplication: {len(raw_trees)} detections -> {len(kept)} unique trees (removed {len(raw_trees) - len(kept)} duplicates)")
    return kept

def estimate_spacings_from_trees(pts: np.ndarray, v_col: np.ndarray, v_row: np.ndarray) -> Tuple[float, float]:
    """
    Tự động ước lượng khoảng cách hàng (row_spacing) và khoảng cách cột (col_spacing)
    dựa trên phân bố không gian thực tế của các cây YOLO phát hiện được:
    - Chiếu các cây lên trục Cột (Major Axis - dọc luống) và trục Hàng (Minor Axis - ngang luống).
    - Đo khoảng cách giữa các cây liền kề trên cùng 1 luống để tính col_spacing.
    - Đo khoảng cách giữa các luống liền kề để tính row_spacing.
    - Lấy trung vị (median) để loại trừ nhiễu / cây khuyết, làm tròn 1 chữ số thập phân.
    """
    if len(pts) < 4:
        return 2.8, 2.7

    c_proj = pts @ v_col
    r_proj = pts @ v_row

    col_deltas = []
    row_deltas = []
    n = len(pts)

    for i in range(n):
        for j in range(i + 1, n):
            dc = abs(c_proj[i] - c_proj[j])
            dr = abs(r_proj[i] - r_proj[j])

            # Các cây trên cùng 1 luống (dr < 0.9m) và là láng giềng kề nhau (1.5m <= dc <= 4.5m)
            if dr < 0.9 and 1.5 <= dc <= 4.5:
                col_deltas.append(dc)

            # Các cây thuộc 2 luống kề nhau (dc < 0.9m) và là láng giềng kề nhau (1.5m <= dr <= 4.5m)
            if dc < 0.9 and 1.5 <= dr <= 4.5:
                row_deltas.append(dr)

    suggested_col = round(float(np.median(col_deltas)), 1) if col_deltas else 2.7
    suggested_row = round(float(np.median(row_deltas)), 1) if row_deltas else 2.8

    return suggested_row, suggested_col


def assign_grid_and_detect_missing(
    trees: List[Dict[str, Any]],
    row_spacing: float = 2.8,
    col_spacing: float = 2.7,
    anchor_point: Optional[Dict[str, float]] = None,
    custom_angle_deg: Optional[float] = None,
    polygon_geojson: Optional[Dict[str, Any]] = None
) -> Tuple[List[Dict[str, Any]], List[Dict[str, Any]], float, float]:
    """
    1. Tự động tính vector phương hướng của hàng cây (PCA) và vector vuông góc.
    2. Chiếu tọa độ theo gốc Cây Neo Mốc (Anchor Point) để bảo đảm Hàng >= 1 và Cột >= 1.
    3. Gán chỉ số Hàng (Row) và Cột (Col) chuẩn xác cho từng cây, không bị trùng lặp hay dồn về (1, 1).
    4. Quét ma trận để phát hiện các vị trí BỎ SÓT / KHUYẾT CÂY (Missing Trees - Chấm Đỏ) theo đúng ô lưới.
    5. Cảm nhận và ước tính khoảng cách hàng / cột thực tế từ các cây đã nhận diện.
    """
    if not trees:
        return [], [], 2.8, 2.7

    ref_lat = trees[0]["lat"]
    ref_lon = trees[0]["lon"]

    for t in trees:
        if "mx" not in t:
            t["mx"], t["my"] = latlon_to_meters(t["lat"], t["lon"], ref_lat, ref_lon)

    pts = np.array([[t["mx"], t["my"]] for t in trees])

    # 1. Tính vector định hướng bằng PCA
    if custom_angle_deg is not None:
        angle = math.radians(custom_angle_deg)
        v_col = np.array([math.cos(angle), math.sin(angle)])
        v_row = np.array([-math.sin(angle), math.cos(angle)])
    else:
        centered = pts - np.mean(pts, axis=0)
        cov = np.cov(centered, rowvar=False)
        evals, evecs = np.linalg.eigh(cov)
        sort_idx = np.argsort(evals)[::-1]
        v_col = evecs[:, sort_idx[0]]  # Trục dọc theo hàng cây (Major axis)
        v_row = evecs[:, sort_idx[1]]  # Trục ngang giữa các hàng (Minor axis)

    # Ước tính khoảng cách Hàng và Cột AI cảm nhận được từ các cây phát hiện
    suggested_row, suggested_col = estimate_spacings_from_trees(pts, v_col, v_row)

    # 2. Xác định điểm gốc tham chiếu (Anchor)
    anchor_tree_idx = None
    if anchor_point and "lat" in anchor_point and "lon" in anchor_point:
        ax_m, ay_m = latlon_to_meters(anchor_point["lat"], anchor_point["lon"], ref_lat, ref_lon)
        raw_anchor = np.array([ax_m, ay_m])
        # Hút (snap) vào tâm cây YOLO đã nhận diện gần nhất
        dists_to_anchor = np.linalg.norm(pts - raw_anchor, axis=1)
        closest_idx = int(np.argmin(dists_to_anchor))
        anchor_pt = pts[closest_idx]
        anchor_tree_idx = closest_idx
    else:
        # Mặc định lấy cây ở góc cực tiểu Tây-Bắc (min c_proj + min r_proj)
        c_proj_init = pts @ v_col
        r_proj_init = pts @ v_row
        corner_idx = int(np.argmin(c_proj_init + r_proj_init))
        anchor_pt = pts[corner_idx]
        anchor_tree_idx = corner_idx

    # Định hướng các vector sao cho hướng từ Anchor đi vào trong vườn luôn dương
    d_pts = pts - anchor_pt
    if np.mean(d_pts @ v_col) < 0:
        v_col = -v_col
    if np.mean(d_pts @ v_row) < 0:
        v_row = -v_row

    # 3. Tính khoảng cách chiếu theo trục Cột và trục Hàng
    col_dists = d_pts @ v_col
    row_dists = d_pts @ v_row

    # Chuẩn hóa để hàng và cột LUÔN bắt đầu từ 1 (không bao giờ nhảy vọt lên hàng 4 hay cột 12)
    # min_c và min_r sẽ tự động bù đắp nếu anchor không nằm ở góc ngoài cùng
    min_c = float(np.min(col_dists))
    min_r = float(np.min(row_dists))

    col_norm = col_dists - min_c
    row_norm = row_dists - min_r

    # Gán Row Index và Col Index
    occupied_cells = set()
    active_trees = []

    for i, t in enumerate(trees):
        is_anchor = (i == anchor_tree_idx)
        row_idx = int(round(row_norm[i] / row_spacing)) + 1
        col_idx = int(round(col_norm[i] / col_spacing)) + 1

        tree_info = {
            "id": f"tree_r{row_idx}_c{col_idx}",
            "row": row_idx,
            "col": col_idx,
            "lat": float(t["lat"]),
            "lon": float(t["lon"]),
            "confidence": round(float(t.get("conf", t.get("confidence", 1.0))), 3),
            "status": "DETECTED",
            "source": t.get("source", "YOLO"),
            "is_anchor": is_anchor
        }
        active_trees.append(tree_info)
        occupied_cells.add((row_idx, col_idx))

    # 4. Tìm các vị trí khuyết (Missing Trees / Bỏ sót theo ô ma trận chuẩn Hàng và Cột)
    max_row = max(t["row"] for t in active_trees)
    max_col = max(t["col"] for t in active_trees)
    missing_trees = []

    # Bản đồ tọa độ thực tế của các cây đã nhận diện theo ô (r, c)
    cell_map = {(t["row"], t["col"]): (t["lat"], t["lon"]) for t in active_trees}
    origin_pt = anchor_pt + min_c * v_col + min_r * v_row

    for r in range(1, max_row + 1):
        for c in range(1, max_col + 1):
            if (r, c) not in occupied_cells:
                # Tìm tọa độ GPS dựa trên các cây láng giềng cùng hàng (row) hoặc cùng cột (col)
                exp_lat = None
                exp_lon = None

                # A. Ưu tiên 1: Nội suy giữa cây bên trái và cây bên phải cùng hàng
                left_c = max([cc for cc in range(1, c) if (r, cc) in cell_map], default=None)
                right_c = min([cc for cc in range(c + 1, max_col + 1) if (r, cc) in cell_map], default=None)

                if left_c is not None and right_c is not None:
                    lat_l, lon_l = cell_map[(r, left_c)]
                    lat_r, lon_r = cell_map[(r, right_c)]
                    w = (c - left_c) / (right_c - left_c)
                    exp_lat = lat_l + w * (lat_r - lat_l)
                    exp_lon = lon_l + w * (lon_r - lon_l)
                    # Tính khoảng cách mét thực tế giữa 2 cây kề cận
                    xl, yl = latlon_to_meters(lat_l, lon_l, ref_lat, ref_lon)
                    xr, yr = latlon_to_meters(lat_r, lon_r, ref_lat, ref_lon)
                    gap_dist = math.hypot(xr - xl, yr - yl)
                    logger.info(f"[KHOẢNG TRỐNG THỰC TẾ] Hàng {r}: Khoảng cách giữa Cột {left_c} và Cột {right_c} là {gap_dist:.2f}m (chuẩn ~{col_spacing}m). -> Xác nhận KHUYẾT CÂY tại Hàng {r} - Cột {c} (Lat: {exp_lat:.7f}, Lon: {exp_lon:.7f})")
                elif (r - 1, c) in cell_map and (r + 1, c) in cell_map:
                    # B. Ưu tiên 2: Nội suy giữa hàng trên và hàng dưới cùng cột
                    lat_t, lon_t = cell_map[(r - 1, c)]
                    lat_b, lon_b = cell_map[(r + 1, c)]
                    exp_lat = (lat_t + lat_b) / 2.0
                    exp_lon = (lon_t + lon_b) / 2.0
                    xt, yt = latlon_to_meters(lat_t, lon_t, ref_lat, ref_lon)
                    xb, yb = latlon_to_meters(lat_b, lon_b, ref_lat, ref_lon)
                    gap_dist = math.hypot(xb - xt, yb - yt)
                    logger.info(f"[KHOẢNG TRỐNG THỰC TẾ] Cột {c}: Khoảng cách giữa Hàng {r-1} và Hàng {r+1} là {gap_dist:.2f}m. -> Xác nhận KHUYẾT CÂY tại Hàng {r} - Cột {c}")
                elif right_c is not None:
                    # C. Ngoại suy từ phía bên phải (cây ở đầu hàng, ví dụ c = 1)
                    r1 = right_c
                    r2 = min([cc for cc in range(r1 + 1, max_col + 1) if (r, cc) in cell_map], default=None)
                    if r2 is not None:
                        lat_1, lon_1 = cell_map[(r, r1)]
                        lat_2, lon_2 = cell_map[(r, r2)]
                        step_lat = (lat_2 - lat_1) / (r2 - r1)
                        step_lon = (lon_2 - lon_1) / (r2 - r1)
                        exp_lat = lat_1 - (r1 - c) * step_lat
                        exp_lon = lon_1 - (r1 - c) * step_lon
                        x1, y1 = latlon_to_meters(lat_1, lon_1, ref_lat, ref_lon)
                        x2, y2 = latlon_to_meters(lat_2, lon_2, ref_lat, ref_lon)
                        step_dist = math.hypot(x2 - x1, y2 - y1) / (r2 - r1)
                        logger.info(f"[KHUYẾT ĐẦU HÀNG] Hàng {r}: Cột {c} bị thiếu. Cây đầu tiên phát hiện được là Cột {r1} (bước nhảy ~{step_dist:.2f}m). -> Ngoại suy KHUYẾT CÂY tại Hàng {r} - Cột {c} (Lat: {exp_lat:.7f}, Lon: {exp_lon:.7f})")
                elif left_c is not None:
                    # D. Ngoại suy từ phía bên trái (cây ở cuối hàng)
                    l1 = left_c
                    l2 = max([cc for cc in range(1, l1) if (r, cc) in cell_map], default=None)
                    if l2 is not None:
                        lat_1, lon_1 = cell_map[(r, l1)]
                        lat_2, lon_2 = cell_map[(r, l2)]
                        step_lat = (lat_1 - lat_2) / (l1 - l2)
                        step_lon = (lon_1 - lon_2) / (l1 - l2)
                        exp_lat = lat_1 + (c - l1) * step_lat
                        exp_lon = lon_1 + (c - l1) * step_lon
                        logger.info(f"[KHUYẾT CUỐI HÀNG] Hàng {r}: Cột {c} bị thiếu. -> Ngoại suy KHUYẾT CÂY tại Hàng {r} - Cột {c}")

                # Fallback: dùng phép chiếu ma trận lưới
                if exp_lat is None or exp_lon is None:
                    exp_pt = origin_pt + (c - 1) * col_spacing * v_col + (r - 1) * row_spacing * v_row
                    exp_lat, exp_lon = meters_to_latlon(float(exp_pt[0]), float(exp_pt[1]), ref_lat, ref_lon)

                # Kiểm tra ranh giới: Giữ nguyên hàm is_inside_polygon để đảm bảo cây khuyết phải nằm trong vườn
                if polygon_geojson and not is_inside_polygon(exp_lat, exp_lon, polygon_geojson):
                    logger.info(f"Ô khuyết Hàng {r} - Cột {c} (Lat: {exp_lat:.7f}, Lon: {exp_lon:.7f}) nằm ngoài polygon ranh giới -> Bỏ qua.")
                    continue

                missing_trees.append({
                    "id": f"missing_r{r}_c{c}",
                    "row": r,
                    "col": c,
                    "lat": round(float(exp_lat), 7),
                    "lon": round(float(exp_lon), 7),
                    "confidence": 0.0,
                    "status": "MISSING",
                    "source": "INTERPOLATED_GRID"
                })

    logger.info(f"Grid analysis: Max Rows={max_row}, Max Cols={max_col}. Detected {len(active_trees)} trees, found {len(missing_trees)} missing gaps.")
    return active_trees, missing_trees, suggested_row, suggested_col


def export_trees_to_csv(active_trees: List[Dict[str, Any]], missing_trees: List[Dict[str, Any]], csv_path: str):
    """
    Gôm tất cả các cây (đã nhận diện và khuyết) xuất ra 1 file CSV tổng duy nhất.
    Nếu file đang bị Microsoft Excel / WPS khóa, tự động lưu file _latest.csv thay thế tránh crash.
    """
    import csv
    import os
    actual_path = csv_path
    try:
        f = open(actual_path, mode="w", newline="", encoding="utf-8")
    except PermissionError:
        base, ext = os.path.splitext(csv_path)
        actual_path = f"{base}_latest{ext}"
        logger.warning(f"File {csv_path} is locked by Excel. Saving to {actual_path} instead.")
        f = open(actual_path, mode="w", newline="", encoding="utf-8")

    with f:
        writer = csv.writer(f)
        writer.writerow(["tree_id", "row", "col", "latitude", "longitude", "confidence", "status", "source"])
        for t in active_trees:
            writer.writerow([t["id"], t["row"], t["col"], t["lat"], t["lon"], t["confidence"], t["status"], t["source"]])
        for m in missing_trees:
            writer.writerow([m["id"], m["row"], m["col"], m["lat"], m["lon"], m["confidence"], m["status"], m["source"]])
    logger.info(f"Exported {len(active_trees) + len(missing_trees)} trees to CSV: {actual_path}")
    return actual_path
