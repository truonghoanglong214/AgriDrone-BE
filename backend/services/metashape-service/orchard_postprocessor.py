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

    suggested_col = round(float(np.median(col_deltas)), 1) if col_deltas else 2.5
    suggested_row = round(float(np.median(row_deltas)), 1) if row_deltas else 2.5

    return suggested_row, suggested_col


def find_dominant_row_angle(pts: np.ndarray, custom_angle_deg: Optional[float] = None) -> Tuple[np.ndarray, np.ndarray, float]:
    """
    Xác định chính xác hướng của hàng cây thực tế trong vườn (v_along) và trục vuông góc (v_across):
    Sử dụng thuật toán bỏ phiếu góc giữa các cặp cây láng giềng kề nhau (1.8m <= d <= 3.8m).
    Tránh sai số 10-15 độ của PCA khi ranh giới thửa đất là đa giác méo.
    """
    if custom_angle_deg is not None:
        theta = math.radians(custom_angle_deg)
        v_along = np.array([math.cos(theta), math.sin(theta)])
        v_across = np.array([-math.sin(theta), math.cos(theta)])
        return v_along, v_across, custom_angle_deg

    n = len(pts)
    if n < 4:
        return np.array([1.0, 0.0]), np.array([0.0, 1.0]), 0.0

    neighbor_angles = []
    for i in range(n):
        for j in range(i + 1, n):
            d = np.linalg.norm(pts[i] - pts[j])
            if 1.8 <= d <= 3.8:
                vec = pts[j] - pts[i]
                deg = math.degrees(math.atan2(vec[1], vec[0])) % 180
                neighbor_angles.append(deg)

    if neighbor_angles:
        hist, bin_edges = np.histogram(neighbor_angles, bins=180, range=(0, 180))
        smoothed = np.convolve(hist, [1, 2, 4, 2, 1], mode="same")
        best_angle_deg = float(bin_edges[np.argmax(smoothed)])
    else:
        centered = pts - np.mean(pts, axis=0)
        cov = np.cov(centered, rowvar=False)
        evals, evecs = np.linalg.eigh(cov)
        sort_idx = np.argsort(evals)[::-1]
        v_major = evecs[:, sort_idx[0]]
        best_angle_deg = math.degrees(math.atan2(v_major[1], v_major[0])) % 180

    theta = math.radians(best_angle_deg)
    v_along = np.array([math.cos(theta), math.sin(theta)])
    v_across = np.array([-math.sin(theta), math.cos(theta)])
    return v_along, v_across, round(best_angle_deg, 1)


def assign_grid_and_detect_missing(
    trees: List[Dict[str, Any]],
    row_spacing: float = 2.5,
    col_spacing: float = 2.5,
    anchor_point: Optional[Dict[str, float]] = None,
    custom_angle_deg: Optional[float] = None,
    polygon_geojson: Optional[Dict[str, Any]] = None
) -> Tuple[List[Dict[str, Any]], List[Dict[str, Any]], float, float]:
    """
    1. Xác định hướng chuẩn của hàng cây qua Neighbor Angle Voting.
    2. Phân cụm hàng theo trục v_across (1D clustering) - bảo đảm cây trên cùng 1 luống KHÔNG BAO GIỜ bị nhảy hàng.
    3. Định vị Cây Neo Mốc (Anchor Tree) thành Hàng 1 - Cột 1 (is_anchor = True).
    4. Sắp xếp thứ tự cột trong từng hàng và phát hiện cây khuyết (Missing Trees) theo khoảng cách thực tế giữa các cây kề nhau.
    """
    if not trees:
        return [], [], 2.5, 2.5

    ref_lat = trees[0]["lat"]
    ref_lon = trees[0]["lon"]

    for t in trees:
        if "mx" not in t:
            t["mx"], t["my"] = latlon_to_meters(t["lat"], t["lon"], ref_lat, ref_lon)

    pts = np.array([[t["mx"], t["my"]] for t in trees])

    # 1. Tìm hướng hàng cây và cảm nhận khoảng cách thực tế
    v_along, v_across, detected_angle = find_dominant_row_angle(pts, custom_angle_deg)
    suggested_row, suggested_col = estimate_spacings_from_trees(pts, v_along, v_across)

    # 2. Phân cụm cây thành các hàng thực tế bằng 1D projection trên trục v_across
    proj_across = pts @ v_across
    sorted_across_idx = np.argsort(proj_across)
    row_clusters = []
    curr_cluster = [int(sorted_across_idx[0])]
    split_threshold = max(1.1, row_spacing * 0.45)

    for idx in sorted_across_idx[1:]:
        prev_idx = curr_cluster[-1]
        if proj_across[idx] - proj_across[prev_idx] > split_threshold:
            row_clusters.append(curr_cluster)
            curr_cluster = [int(idx)]
        else:
            curr_cluster.append(int(idx))
    row_clusters.append(curr_cluster)

    # 3. Xác định Cây Neo Mốc
    anchor_tree_idx = None
    if anchor_point and "lat" in anchor_point and "lon" in anchor_point:
        ax_m, ay_m = latlon_to_meters(anchor_point["lat"], anchor_point["lon"], ref_lat, ref_lon)
        raw_anchor = np.array([ax_m, ay_m])
        dists = np.linalg.norm(pts - raw_anchor, axis=1)
        anchor_tree_idx = int(np.argmin(dists))
    else:
        proj_sum = (pts @ v_along) + (pts @ v_across)
        anchor_tree_idx = int(np.argmin(proj_sum))

    # 4. Tìm hàng chứa cây neo và sắp xếp thứ tự các hàng (Row 1 luôn là hàng chứa cây neo)
    anchor_cluster_idx = 0
    for r_i, cl in enumerate(row_clusters):
        if anchor_tree_idx in cl:
            anchor_cluster_idx = r_i
            break

    if anchor_cluster_idx == 0:
        ordered_row_clusters = row_clusters
    elif anchor_cluster_idx == len(row_clusters) - 1:
        ordered_row_clusters = row_clusters[::-1]
    else:
        anc_mean_across = float(np.mean([proj_across[k] for k in row_clusters[anchor_cluster_idx]]))
        remaining = [cl for i, cl in enumerate(row_clusters) if i != anchor_cluster_idx]
        remaining.sort(key=lambda cl: abs(float(np.mean([proj_across[k] for k in cl])) - anc_mean_across))
        ordered_row_clusters = [row_clusters[anchor_cluster_idx]] + remaining

    # 5. Trong Hàng 1 (hàng chứa neo): xác định chiều dọc và snap neo góc nếu cần
    row1 = ordered_row_clusters[0]
    row1_sorted_along = sorted(row1, key=lambda i: float(pts[i] @ v_along))
    anc_pos = row1_sorted_along.index(anchor_tree_idx)

    # Nếu neo gần đầu min hơn: hướng dương là từ min -> max. Snap vào đầu min nếu trong khoảng 1 cây (< 2.8m)
    if anc_pos <= len(row1_sorted_along) / 2:
        dist_to_min = np.linalg.norm(pts[anchor_tree_idx] - pts[row1_sorted_along[0]])
        if dist_to_min < 2.8:
            anchor_tree_idx = row1_sorted_along[0]
    else:
        # Nếu neo gần đầu max hơn: hướng dọc ngược lại
        dist_to_max = np.linalg.norm(pts[anchor_tree_idx] - pts[row1_sorted_along[-1]])
        if dist_to_max < 2.8:
            anchor_tree_idx = row1_sorted_along[-1]
        v_along = -v_along

    anchor_pt = pts[anchor_tree_idx]

    # Gán Row Index và Col Index cho active_trees
    active_trees = []
    occupied_cells = set()
    for r_num, cl in enumerate(ordered_row_clusters, 1):
        cl_sorted = sorted(cl, key=lambda i: float((pts[i] - anchor_pt) @ v_along))
        for idx in cl_sorted:
            s = float((pts[idx] - anchor_pt) @ v_along)
            c_idx = max(1, int(round(s / col_spacing)) + 1)
            while (r_num, c_idx) in occupied_cells:
                c_idx += 1
            occupied_cells.add((r_num, c_idx))
            is_anchor = (idx == anchor_tree_idx)

            active_trees.append({
                "id": f"tree_r{r_num}_c{c_idx}",
                "row": r_num,
                "col": c_idx,
                "lat": float(trees[idx]["lat"]),
                "lon": float(trees[idx]["lon"]),
                "confidence": round(float(trees[idx].get("conf", trees[idx].get("confidence", 1.0))), 3),
                "status": "DETECTED",
                "source": trees[idx].get("source", "YOLO"),
                "is_anchor": is_anchor
            })

    # 4. Tìm các vị trí khuyết (Missing Trees - giữ nguyên thuật toán gốc ban đầu theo yêu cầu)
    max_row = max(t["row"] for t in active_trees)
    max_col = max(t["col"] for t in active_trees)
    missing_trees = []

    cell_map = {(t["row"], t["col"]): (t["lat"], t["lon"]) for t in active_trees}
    origin_pt = anchor_pt

    for r in range(1, max_row + 1):
        for c in range(1, max_col + 1):
            if (r, c) not in occupied_cells:
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
                    xl, yl = latlon_to_meters(lat_l, lon_l, ref_lat, ref_lon)
                    xr, yr = latlon_to_meters(lat_r, lon_r, ref_lat, ref_lon)
                    gap_dist = math.hypot(xr - xl, yr - yl)
                    logger.info(f"[KHOẢNG TRỐNG THỰC TẾ] Hàng {r}: Khoảng cách giữa Cột {left_c} và Cột {right_c} là {gap_dist:.2f}m. -> Xác nhận KHUYẾT CÂY tại Hàng {r} - Cột {c}")
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
                        logger.info(f"[KHUYẾT ĐẦU HÀNG] Hàng {r}: Cột {c} bị thiếu. Cây đầu tiên là Cột {r1} (bước nhảy ~{step_dist:.2f}m). -> Ngoại suy KHUYẾT CÂY tại Hàng {r} - Cột {c}")
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
                    exp_pt = origin_pt + (c - 1) * col_spacing * v_along + (r - 1) * row_spacing * v_across
                    exp_lat, exp_lon = meters_to_latlon(float(exp_pt[0]), float(exp_pt[1]), ref_lat, ref_lon)

                # Kiểm tra ranh giới
                if polygon_geojson and not is_inside_polygon(exp_lat, exp_lon, polygon_geojson):
                    logger.info(f"Ô khuyết Hàng {r} - Cột {c} nằm ngoài polygon ranh giới -> Bỏ qua.")
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

    max_row = max(t["row"] for t in active_trees)
    max_col = max(t["col"] for t in active_trees)
    logger.info(f"Grid analysis: Max Rows={max_row}, Max Cols={max_col}. Dominant angle={detected_angle}°. Detected {len(active_trees)} trees, found {len(missing_trees)} missing trees.")
    return active_trees, missing_trees, suggested_row, suggested_col


def export_trees_to_csv(
    active_trees: List[Dict[str, Any]],
    missing_trees: List[Dict[str, Any]],
    csv_path: str,
    polygon_geojson: Optional[Dict[str, Any]] = None
):
    """
    Gôm tất cả các cây (đã nhận diện và khuyết) cùng các điểm mốc ranh giới viền đất (Boundary/Border) xuất ra 1 file CSV tổng duy nhất.
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

    boundary_points = []
    if polygon_geojson:
        try:
            coords = []
            if isinstance(polygon_geojson, dict):
                g_type = polygon_geojson.get("type", "")
                if g_type == "Polygon":
                    coords = polygon_geojson.get("coordinates", [[]])[0]
                elif g_type == "Feature":
                    coords = polygon_geojson.get("geometry", {}).get("coordinates", [[]])[0]
            elif isinstance(polygon_geojson, list):
                coords = polygon_geojson

            for idx, pt in enumerate(coords):
                if len(pt) >= 2:
                    # GeoJSON is [lon, lat]
                    b_lon, b_lat = float(pt[0]), float(pt[1])
                    boundary_points.append({
                        "id": f"boundary_point_{idx + 1}",
                        "row": 0,
                        "col": 0,
                        "lat": b_lat,
                        "lon": b_lon,
                        "confidence": 1.0,
                        "status": "BOUNDARY",
                        "source": "USER_POLYGON"
                    })
        except Exception as e:
            logger.warning(f"Failed to parse polygon_geojson for CSV boundary points: {e}")

    with f:
        writer = csv.writer(f)
        writer.writerow(["tree_id", "row", "col", "latitude", "longitude", "confidence", "status", "source"])
        for b in boundary_points:
            writer.writerow([b["id"], b["row"], b["col"], b["lat"], b["lon"], b["confidence"], b["status"], b["source"]])
        for t in active_trees:
            writer.writerow([t["id"], t["row"], t["col"], t["lat"], t["lon"], t["confidence"], t["status"], t["source"]])
        for m in missing_trees:
            writer.writerow([m["id"], m["row"], m["col"], m["lat"], m["lon"], m["confidence"], m["status"], m["source"]])

    total_records = len(boundary_points) + len(active_trees) + len(missing_trees)
    logger.info(f"Exported {total_records} records ({len(boundary_points)} boundary, {len(active_trees)} active, {len(missing_trees)} missing) to CSV: {actual_path}")
    return actual_path
    return actual_path
