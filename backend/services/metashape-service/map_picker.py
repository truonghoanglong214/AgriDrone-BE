import json

import html

from typing import Optional

from minio_storage import minio_storage



def get_map_picker_html(

    mission_id: str,

    tenant_id: str,

    farm_id: str = "",

    preview_url: Optional[str] = None,

    min_lat: float = 10.4613764,

    max_lat: float = 10.4619687,

    min_lon: float = 106.4938479,

    max_lon: float = 106.4941861,

    tif_key: Optional[str] = None,

    available_missions: Optional[list] = None

) -> str:

    # Auto-generate fresh presigned URL if not passed

    if not preview_url:

        jpg_key = f"tenants/{tenant_id}/media/{mission_id}/map_preview.jpg"

        preview_url = minio_storage.get_presigned_url(jpg_key)

    

    if not tif_key:

        tif_key = f"tenants/{tenant_id}/media/{mission_id}/orthophoto.tif"



    center_lat = (min_lat + max_lat) / 2

    center_lon = (min_lon + max_lon) / 2

    if not farm_id:

        farm_id = "b9d9d8a3-94a5-4390-a007-9017a2a131fa"

    if not tenant_id:

        tenant_id = "a83f601b-3d5b-4e14-b735-28b46b213438"

    escaped_farm_id = html.escape(farm_id, quote=True)

    escaped_tenant_id = html.escape(tenant_id, quote=True)



    options_html = ""

    if available_missions:

        for m in available_missions:

            m_id = m.get("mission_id", "")

            t_id = m.get("tenant_id", "")

            selected = ' selected="selected"' if m_id == mission_id else ""

            options_html += f'<option value="{m_id}"{selected}>{m_id} (Tenant: {t_id[:8]}...)</option>'

    if not options_html:

        options_html = f'<option value="{mission_id}" selected="selected">{mission_id}</option>'



    return f"""<!DOCTYPE html>

<html lang="vi">

<head>

  <meta charset="UTF-8">

  <meta name="viewport" content="width=device-width, initial-scale=1.0">

  <title>AgriDrone - Bản Đồ Tương Tác Chọn Điểm GPS & Cắt Viền Vườn</title>

  <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />

  <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>

  <style>

    :root {{

      --bg-dark: #0f172a;

      --card-dark: #1e293b;

      --border-dark: #334155;

      --primary: #10b981;

      --primary-hover: #059669;

      --cyan: #06b6d4;

      --text-main: #f8fafc;

      --text-muted: #94a3b8;

    }}

    * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }}

    body {{ background: var(--bg-dark); color: var(--text-main); display: flex; height: 100vh; overflow: hidden; }}

    

    #map-container {{ flex: 1; height: 100%; position: relative; }}

    #map {{ width: 100%; height: 100%; background: #0b0f19; }}

    

    /* Crosshair & coordinates banner */

    .map-banner {{

      position: absolute; top: 12px; left: 60px; z-index: 1000;

      background: rgba(15, 23, 42, 0.85); backdrop-filter: blur(8px);

      border: 1px solid var(--border-dark); border-radius: 8px;

      padding: 8px 16px; font-size: 13px; display: flex; gap: 16px; align-items: center;

    }}

    .map-banner span {{ color: var(--cyan); font-weight: 600; }}

    

    /* Control sidebar */

    #sidebar {{

      width: 440px; height: 100%; background: var(--card-dark);

      border-left: 1px solid var(--border-dark); display: flex; flex-direction: column;

      box-shadow: -4px 0 20px rgba(0,0,0,0.5); z-index: 1001;

    }}

    .sidebar-header {{

      padding: 16px 20px; border-bottom: 1px solid var(--border-dark);

      background: linear-gradient(135deg, rgba(16, 185, 129, 0.15), transparent);

    }}

    .sidebar-header h2 {{ font-size: 16px; font-weight: 700; color: #fff; display: flex; align-items: center; gap: 8px; }}

    .badge {{ background: var(--primary); color: #000; font-size: 11px; padding: 2px 8px; border-radius: 999px; font-weight: 700; }}

    

    .sidebar-body {{ padding: 16px 20px; flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 16px; }}

    

    .section-title {{ font-size: 13px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: var(--text-muted); margin-bottom: 6px; }}

    .card {{ background: rgba(15, 23, 42, 0.6); border: 1px solid var(--border-dark); border-radius: 8px; padding: 12px; font-size: 13px; }}

    

    .btn {{

      display: inline-flex; align-items: center; justify-content: center; gap: 6px;

      padding: 8px 14px; border-radius: 6px; font-size: 13px; font-weight: 600;

      cursor: pointer; transition: all 0.2s; border: none;

    }}

    .btn-primary {{ background: var(--primary); color: #000; }}

    .btn-primary:hover {{ background: var(--primary-hover); }}

    .btn-secondary {{ background: #334155; color: #f8fafc; }}

    .btn-secondary:hover {{ background: #475569; }}

    .btn-danger {{ background: #ef4444; color: #fff; }}

    .btn-danger:hover {{ background: #dc2626; }}

    

    .btn-group {{ display: flex; gap: 8px; }}

    

    textarea {{

      width: 100%; height: 110px; background: #090d16; border: 1px solid var(--border-dark);

      border-radius: 6px; color: #38bdf8; font-family: monospace; font-size: 11px;

      padding: 8px; resize: none;

    }}

    

    .points-list {{ max-height: 120px; overflow-y: auto; display: flex; flex-direction: column; gap: 4px; font-family: monospace; font-size: 12px; }}

    .point-item {{ display: flex; justify-content: space-between; background: rgba(255,255,255,0.03); padding: 4px 8px; border-radius: 4px; }}

    

    .slider-container {{ display: flex; align-items: center; justify-content: space-between; gap: 10px; font-size: 12px; }}

    input[type=range] {{ flex: 1; accent-color: var(--primary); }}

    

    select, input[type=number] {{

      background: #090d16; border: 1px solid var(--border-dark);

      border-radius: 6px; color: #fff; padding: 5px 8px; font-size: 12px;

    }}

    select:focus, input[type=number]:focus {{

      border-color: var(--primary); outline: none;

    }}

    

    .stat-row {{ display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 4px; }}

    .stat-label {{ color: var(--text-muted); }}

    .stat-val {{ font-weight: 600; color: #fff; }}

    

    /* Result tiles gallery */

    .tiles-grid {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; max-height: 180px; overflow-y: auto; margin-top: 8px; }}

    .tile-thumb {{ width: 100%; aspect-ratio: 1; object-fit: cover; border-radius: 4px; border: 1px solid var(--border-dark); cursor: pointer; }}

    .tile-thumb:hover {{ border-color: var(--primary); transform: scale(1.05); }}

  </style>

</head>

<body>

  <div id="map-container">

    <div class="map-banner">

      <div>🛰️ Tọa độ chuột: <span id="mouse-coords">Di chuyển chuột...</span></div>

      <div>📐 Số điểm đã chọn: <span id="point-count">0</span></div>

      <div>🟢 Diện tích: <span id="area-val">0 ha</span></div>

    </div>

    <div id="map"></div>

  </div>



  <div id="sidebar">

    <div class="sidebar-header">

      <h2>🌱 Bản Đồ Tương Tác <span class="badge">AgriDrone</span></h2>

      <p style="font-size: 12px; color: var(--text-muted); margin-top: 4px;">Zoom, chấm điểm ranh giới & cắt ảnh YOLO</p>

    </div>



    <div class="sidebar-body">

      <!-- Mode Tabs -->

      <div style="display: flex; gap: 6px; padding: 4px; background: rgba(0,0,0,0.5); border-radius: 8px; margin-bottom: 12px; border: 1px solid var(--border-dark);">

        <button id="btn-tab-yolo" onclick="switchPickerMode('yolo')" style="flex: 1; padding: 8px 4px; font-size: 11px; font-weight: 700; border: none; border-radius: 6px; cursor: pointer; background: #06b6d4; color: #000; transition: all 0.2s;">

          🗺️ 1. Bản Đồ & YOLO (Gốc)

        </button>

        <button id="btn-tab-health" onclick="switchPickerMode('health')" style="flex: 1; padding: 8px 4px; font-size: 11px; font-weight: 700; border: none; border-radius: 6px; cursor: pointer; background: transparent; color: #94a3b8; transition: all 0.2s;">

          🩺 2. Cây Bệnh (.NET API)

        </button>

      </div>



      <!-- PANEL 1: YOLO & MAP (Gốc - Mặc định) -->

      <div id="panel-yolo">

        <!-- Mission Switcher -->

        <div class="card" style="border-color: #06b6d4; background: rgba(6, 182, 212, 0.08);">

          <div class="section-title" style="color: #06b6d4; display: flex; justify-content: space-between; align-items: center;">

            <span>🎯 Chọn Chuyến Bay (Mission)</span>

            <span class="badge" style="background: #06b6d4; color: #000; font-size: 10px;">Dynamic</span>

          </div>

          <div style="margin-top: 6px; display: flex; flex-direction: column; gap: 6px;">

            <div>

              <label style="font-size: 11px; color: var(--text-muted);">Danh sách Mission phát hiện trong MinIO:</label>

              <select id="mission-picker-select" style="width: 100%; margin-top: 2px;" onchange="onSelectMissionChange(this.value)">

                {options_html}

              </select>

            </div>

            <div style="display: flex; gap: 6px;">

              <input id="input-custom-mission" value="{mission_id}" placeholder="Hoặc nhập Mission ID..." style="flex: 1; font-family: monospace; font-size: 11px;" />

              <button class="btn btn-secondary" onclick="loadCustomMissionId()" style="padding: 4px 8px; font-size: 11px;">Mở</button>

            </div>

            <div style="font-size: 11px; color: var(--text-muted); display: flex; justify-content: space-between;">

              <span>ID: <b style="color: #38bdf8; font-family: monospace;">{mission_id[:8]}...</b></span>

              <span>Tọa độ: <b id="lbl-coords" style="color: #10b981;">{min_lat:.5f}, {min_lon:.5f}</b></span>

            </div>

          </div>

        </div>



        <!-- Opacity Controller -->

        <div class="card">

          <div class="slider-container">

            <span>Độ mờ ảnh Drone:</span>

            <input type="range" id="opacity-slider" min="0" max="100" value="95" oninput="changeOpacity(this.value)">

            <span id="opacity-val" style="width: 32px;">95%</span>

          </div>

        </div>



      <!-- Drawing Actions -->

      <div class="card">

        <div class="section-title">Thao tác chọn ranh giới</div>

        <p style="font-size: 12px; color: var(--text-muted); margin-bottom: 8px;">

          👉 Bấm trực tiếp lên ảnh để thêm điểm mốc vườn. Điểm cuối sẽ tự khép kín đa giác.

        </p>

        <div class="btn-group">

          <button class="btn btn-secondary" onclick="undoPoint()">↩ Xóa điểm cuối</button>

          <button class="btn btn-danger" onclick="clearAllPoints()">🗑️ Xóa hết</button>

        </div>

        <div style="margin-top: 6px;">

          <button class="btn btn-secondary" onclick="fetchAndApplyCsvBoundary()" style="width: 100%; font-size: 11px; padding: 5px; border-color: #38bdf8; color: #38bdf8; font-weight: 600;">

            📂 Nạp Ranh Giới Gốc Từ File CSV (Temp)

          </button>

        <div style="margin-top: 6px;">

          <button id="btn-toggle-draw-polygon" class="btn btn-secondary" style="width: 100%; font-size: 11px; padding: 5px;" onclick="togglePolygonLock()">

            ✏️ Đang mở vẽ viền (Click map để thêm điểm)

          </button>

        </div>

      </div>



      <!-- Selected Points -->

      <div class="card">

        <div class="section-title">Danh sách mốc tọa độ GPS</div>

        <div class="points-list" id="points-container">

          <div style="color: var(--text-muted); font-size: 12px;">Chưa có điểm nào. Hãy click lên ảnh.</div>

        </div>

      </div>



      <!-- Hidden GeoJSON storage for slicing & detection -->

      <textarea id="geojson-output" style="display: none;"></textarea>



      <!-- Slicing Action -->

      <div class="card" style="border-color: var(--primary);">

        <div class="section-title" style="color: var(--primary); display: flex; justify-content: space-between; align-items: center;">

          <span>🚀 Cắt ảnh YOLO Tự Động (Dual-Scale)</span>

        </div>

        <div style="font-size: 12px; color: var(--text-muted); line-height: 1.5; margin-top: 4px;">

          Tự động cắt đồng thời <b>2 lớp tỉ lệ</b> để YOLO nhận diện chuẩn 100%, chống bỏ sót cây:

        </div>

        <div style="margin-top: 8px; display: flex; flex-direction: column; gap: 6px; font-size: 12px;">

          <div style="display: flex; justify-content: space-between; align-items: center; background: rgba(255,255,255,0.04); padding: 6px 10px; border-radius: 4px;">

            <div>

              <span style="color: #38bdf8; font-weight: 700;">🔹 Lớp 640x640</span>

              <div style="font-size: 10px; color: var(--text-muted);">Cận cảnh chi tiết / Trụ nhỏ</div>

            </div>

            <span style="font-family: monospace; color: #f8fafc;">Gối 64px</span>

          </div>

          <div style="display: flex; justify-content: space-between; align-items: center; background: rgba(255,255,255,0.04); padding: 6px 10px; border-radius: 4px;">

            <div>

              <span style="color: #34d399; font-weight: 700;">🔹 Lớp 1280x1280</span>

              <div style="font-size: 10px; color: var(--text-muted);">Góc rộng toàn cảnh / Hàng lối</div>

            </div>

            <span style="font-family: monospace; color: #f8fafc;">Gối 128px</span>

          </div>

        </div>



        <button id="btn-slice" class="btn btn-primary" style="width: 100%; margin-top: 12px; padding: 10px;" onclick="runSlicing()">

          ✂️ Bắt đầu Cắt Mảnh Bản Đồ (Dual-Scale)

        </button>

        <div id="slice-status" style="font-size: 12px; margin-top: 8px; color: #38bdf8;"></div>

        <div class="tiles-grid" id="tiles-preview"></div>

      </div>



      <!-- Tree Review & Missing Detection -->

      <div class="card" style="border-color: #38bdf8;">

        <div class="section-title" style="color: #38bdf8; display: flex; justify-content: space-between; align-items: center;">

          <span>🌳 Kết quả AI & Cây Bỏ Sót</span>

        </div>

        <div style="font-size: 12px; color: var(--text-muted); line-height: 1.4; margin-top: 4px;">

          Tự động phân chia hàng/cột và phát hiện vị trí bị bỏ sót:

        </div>



        <!-- Parameter inputs -->

        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-top: 8px;">

          <div>

            <label style="font-size: 11px; color: #38bdf8; font-weight: 600;">K.cách Hàng (m) [Ngắn]:</label>

            <input type="number" id="input-row-spacing" value="2.8" step="0.1" style="width: 100%; padding: 4px 6px; font-size: 12px; background: rgba(0,0,0,0.3); border: 1px solid var(--border); border-radius: 4px; color: white;">

          </div>

          <div>

            <label style="font-size: 11px; color: #38bdf8; font-weight: 600;">K.cách Cột (m) [Dài]:</label>

            <input type="number" id="input-col-spacing" value="2.7" step="0.1" style="width: 100%; padding: 4px 6px; font-size: 12px; background: rgba(0,0,0,0.3); border: 1px solid var(--border); border-radius: 4px; color: white;">

          </div>

        </div>



        <!-- AI Spacing Sensed Box -->

        <div id="ai-spacing-box" style="display: none; margin-top: 8px; background: rgba(16, 185, 129, 0.12); border: 1px solid #10b981; border-radius: 6px; padding: 8px; font-size: 11px;">

          <div style="color: #10b981; font-weight: 700; margin-bottom: 4px;">💡 AI cảm nhận khoảng cách thực tế:</div>

          <div style="display: flex; justify-content: space-between; color: #e2e8f0; margin-bottom: 6px;">

            <span>Hàng (ngắn ~5 hàng): <b id="sensed-row-val" style="color: #38bdf8;">2.8m</b></span>

            <span>Cột (dài ~14 trụ): <b id="sensed-col-val" style="color: #38bdf8;">2.7m</b></span>

          </div>

          <button class="btn" style="background: #10b981; color: #000; font-weight: 700; font-size: 11px; padding: 5px 8px; width: 100%; border-radius: 4px; cursor: pointer;" onclick="applySensedSpacingAndRerun()">

            ⚡ Áp dụng thông số này & Chạy lại ngay

          </button>

        </div>





        <div style="margin-top: 8px;">

          <div style="display: flex; justify-content: space-between; font-size: 11px; color: var(--text-muted);">

            <span>Độ nhạy AI (Confidence):</span>

            <span id="conf-val" style="color: #38bdf8; font-weight: 600;">0.25</span>

          </div>

          <input type="range" id="input-conf" min="0.1" max="0.8" step="0.05" value="0.25" style="width: 100%;" oninput="document.getElementById('conf-val').innerText = this.value">

        </div>



        <div style="margin-top: 8px; background: rgba(0,0,0,0.25); padding: 8px; border-radius: 6px; border: 1px dashed #38bdf8;">

          <div style="display: flex; justify-content: space-between; align-items: center;">

            <button id="btn-pick-anchor" class="btn btn-secondary" style="font-size: 11px; padding: 4px 8px;" onclick="togglePickAnchorMode()">

              📍 Chọn Cây Neo (H1 - C1)

            </button>

            <div style="display: flex; align-items: center; gap: 6px;">

              <span id="anchor-badge" style="font-size: 11px; color: #10b981; font-weight: 600;">Tự động</span>

              <button id="btn-clear-anchor" onclick="clearAnchorPoint()" style="display: none; background: rgba(239, 68, 68, 0.2); border: 1px solid #ef4444; color: #ef4444; border-radius: 4px; font-size: 10px; padding: 2px 6px; cursor: pointer;" title="Bỏ chọn cây neo, chuyển về Tự Động">↺ Hủy</button>

            </div>

          </div>

          <div id="anchor-coords-text" style="font-size: 10px; color: var(--text-muted); margin-top: 4px;">Bấm nút rồi nhấp vào 1 cây góc để đặt Hàng 1 - Cột 1</div>

        </div>



        <div style="display: flex; flex-direction: column; gap: 8px; margin-top: 10px;">

          <button id="btn-run-real-yolo" class="btn" style="background: linear-gradient(135deg, #06b6d4, #10b981); color: #000; font-weight: 700; width: 100%;" onclick="runRealYoloInference()">

            🚀 Chạy YOLO Nhận Diện Thật (best.pt)

          </button>

          <button class="btn btn-secondary" style="width: 100%; font-size: 12px;" onclick="toggleAddTreeMode()" id="btn-add-tree-mode">

            ➕ Bật / Tắt Chế Độ Chấm Thêm Trụ Bằng Tay

          </button>

        </div>



        <div id="tree-stats" style="margin-top: 10px; font-size: 12px; display: none; flex-direction: column; gap: 4px;">

          <div style="display: flex; justify-content: space-between;">

            <span style="color: #10b981; font-weight: 600;">🟢 Trụ nhận diện:</span>

            <span id="active-trees-count" style="font-weight: 700;">0</span>

          </div>

          <div style="display: flex; justify-content: space-between;">

            <span style="color: #ef4444; font-weight: 600;">🔴 Nghi ngờ khuyết/sót:</span>

            <span id="missing-trees-count" style="font-weight: 700;">0</span>

          </div>

          <div style="display: flex; justify-content: space-between; color: var(--text-muted);">

            <span>📐 Kích thước lưới:</span>

            <span id="grid-dimensions-val">0 hàng x 0 cột</span>

          </div>

          <button id="btn-download-csv" onclick="downloadCurrentTreesCsv()" class="btn btn-secondary" style="margin-top: 6px; font-size: 11px; display: none; width: 100%; cursor: pointer;">

            📥 Tải File CSV Mới Nhất (Đã chỉnh sửa)

          </button>

          <button id="btn-renumber-grid" onclick="renumberAllRowsAndColumns()" class="btn btn-secondary" style="margin-top: 6px; font-size: 11px; display: none; width: 100%; cursor: pointer; border-color: #38bdf8; color: #38bdf8; font-weight: 700;">

            ⚡ Tính toán lại vị trí cây (Chuẩn hóa Hàng & Cột)

          </button>

          <input id="plant-gps-farm-id" value="{escaped_farm_id}" placeholder="Farm ID" style="width: 100%; margin-top: 6px;" />

          <input id="plant-gps-api-url" value="http://localhost:5080" placeholder=".NET API URL" style="width: 100%; margin-top: 6px;" />

          <input id="plant-gps-token" type="password" placeholder="Manager JWT token" autocomplete="off" style="width: 100%; margin-top: 6px;" />

        </div>

      </div>

      </div>
    </div> <!-- end panel-yolo -->



    <!-- PANEL 2: Bản Đồ Sức Khỏe (.NET API) -->

    <div id="panel-health" style="display: none;">

      <!-- Farm & Tenant Health Map Selector -->

      <div class="card" style="border-color: #10b981; background: rgba(16, 185, 129, 0.08);">

        <div class="section-title" style="color: #10b981; display: flex; justify-content: space-between; align-items: center;">

          <span>🏡 Nông Trại & Trạng Thái Sức Khỏe</span>

          <span class="badge" id="badge-health-map-status" style="background: #10b981; color: #000; font-size: 10px;">Chờ nạp</span>

        </div>

        <div style="font-size: 11px; color: var(--text-muted); line-height: 1.4; margin-top: 4px;">

          Tự động lấy ảnh bản đồ nền từ <b style="color: #38bdf8;">Farm ID</b> và dữ liệu 61 trụ cây kèm lịch sử bệnh/trái từ <b style="color: #34d399;">Tenant ID</b>.

        </div>

        <div style="margin-top: 8px; display: flex; flex-direction: column; gap: 8px;">

          <div>

            <label style="font-size: 11px; color: #38bdf8; font-weight: 600;">1. Chuyến bay ghép bản đồ (Base Map):</label>

            <input id="input-base-mission" value="9978d458-ffb1-43e3-936f-1776ba4a239c" placeholder="Mission bản đồ nền..." style="width: 100%; font-family: monospace; font-size: 11px; padding: 5px 8px; margin-top: 3px;" />

          </div>

          <div>

            <label style="font-size: 11px; color: #fbbf24; font-weight: 600;">2. Chuyến bay quét bệnh (Disease Flight):</label>

            <input id="input-disease-mission" value="17bfaee6-2e55-4c94-ae35-225df0d99d4d" placeholder="Mission quét bệnh..." style="width: 100%; font-family: monospace; font-size: 11px; padding: 5px 8px; margin-top: 3px;" />

          </div>

          <button class="btn btn-primary" onclick="loadFarmHealthMap()" style="padding: 9px; font-size: 12px; font-weight: 700; width: 100%;">

            🔄 Nạp Dữ Liệu Bản Đồ & Vết Bệnh

          </button>

          <div id="base-map-meta-info" style="font-size: 11px; color: var(--text-muted); display: flex; justify-content: space-between; background: rgba(0,0,0,0.25); padding: 6px 8px; border-radius: 4px; border: 1px solid var(--border-dark);">

            <span>Map Mission: <b id="lbl-source-mission" style="color: #38bdf8; font-family: monospace;">{mission_id[:8]}...</b></span>

            <span>Tọa độ: <b id="lbl-coords-health" style="color: #10b981;">{min_lat:.5f}, {min_lon:.5f}</b></span>

          </div>

        </div>

      </div>



      <!-- Classification Statistics Card -->

      <div class="card" style="border-color: #f59e0b; background: rgba(245, 158, 11, 0.05);">

        <div class="section-title" style="color: #f59e0b; display: flex; justify-content: space-between; align-items: center;">

          <span>📊 Thống Kê Phân Loại Cây Trồng</span>

          <span class="badge" id="badge-disease-count" style="background: #ef4444; color: #fff; font-size: 10px;">16 Cây Bệnh</span>

        </div>

        <div style="margin-top: 8px; display: flex; flex-direction: column; gap: 6px; font-size: 12px;">

          <div style="display: flex; justify-content: space-between; padding: 5px 8px; background: rgba(255,255,255,0.03); border-radius: 4px;">

            <span style="color: #f8fafc; font-weight: 600;">🌳 Tổng số trụ trong vườn:</span>

            <b id="stat-total-plants" style="color: #38bdf8;">61</b>

          </div>

          <div style="display: flex; justify-content: space-between; padding: 5px 8px; background: rgba(16,185,129,0.1); border-radius: 4px; border-left: 3px solid #10b981;">

            <span style="color: #10b981; font-weight: 600;">🟢 Trụ bình thường (chưa bệnh/trái):</span>

            <b id="stat-normal-trees" style="color: #10b981;">34</b>

          </div>

          <div style="display: flex; justify-content: space-between; padding: 5px 8px; background: rgba(6,182,212,0.1); border-radius: 4px; border-left: 3px solid #06b6d4;">

            <span style="color: #06b6d4; font-weight: 600;">🍏 Trụ có trái khỏe mạnh:</span>

            <b id="stat-healthy-trees" style="color: #06b6d4;">11</b>

          </div>

          <div style="display: flex; justify-content: space-between; padding: 5px 8px; background: rgba(239,68,68,0.1); border-radius: 4px; border-left: 3px solid #ef4444;">

            <span style="color: #ef4444; font-weight: 600;">🔴 Trụ phát hiện bệnh:</span>

            <b id="stat-diseased-trees" style="color: #ef4444;">16</b>

          </div>

          <div id="disease-breakdown" style="margin-top: 6px; padding-top: 6px; border-top: 1px dashed var(--border-dark); font-size: 11px; color: var(--text-muted); display: flex; flex-direction: column; gap: 4px;">

          </div>

        </div>

      </div>


      <!-- Outbreak Zone Alerts Card -->
      <div class="card" style="border-color: #ef4444; background: rgba(239, 68, 68, 0.05); margin-top: 10px;">
        <div class="section-title" style="color: #ef4444; display: flex; justify-content: space-between; align-items: center;">
          <span>🚨 Cảnh Báo Ổ Dịch (Outbreak Zones)</span>
          <span class="badge" id="badge-zone-count" style="background: #ef4444; color: #fff; font-size: 10px;">0 Ổ Dịch</span>
        </div>
        <div style="font-size: 11px; color: var(--text-muted); line-height: 1.4; margin-top: 4px;">
          Tự động phát hiện & khoanh vùng: <b>Ổ dịch mật độ cao</b> (High-Density) & <b>Ổ dịch lây lan</b> (Same-Disease).
        </div>
        <!-- Filter Controls -->
        <div style="display: flex; gap: 10px; margin-top: 8px; padding: 7px 10px; background: rgba(0,0,0,0.4); border-radius: 6px; font-size: 11px; border: 1px solid rgba(255,255,255,0.08);">
          <label style="display: flex; align-items: center; gap: 5px; color: #f97316; cursor: pointer; font-weight: 700;">
            <input type="checkbox" id="chk-show-th1" checked onchange="toggleZoneVisibility()" /> 🟠 TH1: Mật độ cao
          </label>
          <label style="display: flex; align-items: center; gap: 5px; color: #facc15; cursor: pointer; font-weight: 700;">
            <input type="checkbox" id="chk-show-th2" checked onchange="toggleZoneVisibility()" /> 🟡 TH2: Cùng bệnh (Vàng)
          </label>
        </div>

        <div id="zone-alerts-container" style="margin-top: 8px; display: flex; flex-direction: column; gap: 8px; max-height: 380px; overflow-y: auto;">
          <div style="color: var(--text-muted); font-size: 11px;">Chưa phát hiện ổ dịch.</div>
        </div>
      </div>

    </div> <!-- end panel-health -->

    </div> <!-- end sidebar-body -->
  </div> <!-- end sidebar -->



  <script>

    const missionId = "{mission_id}";

    const tenantId = "{tenant_id}";



    function onSelectMissionChange(newMissionId) {{

      if (newMissionId && newMissionId !== missionId) {{

        window.location.href = '/map-picker?mission_id=' + encodeURIComponent(newMissionId);

      }}

    }}



    function loadCustomMissionId() {{

      const customId = document.getElementById('input-custom-mission').value.trim();

      if (customId && customId !== missionId) {{

        window.location.href = '/map-picker?mission_id=' + encodeURIComponent(customId);

      }}

    }}

    const tifKey = "{tif_key}";

    const imageUrl = "{preview_url}";

    const bounds = [[{min_lat}, {min_lon}], [{max_lat}, {max_lon}]];



    // Initialize Leaflet Map

    const map = L.map('map', {{

      center: [{center_lat}, {center_lon}],

      zoom: 18,

      maxZoom: 22

    }});



    // Google Satellite Layer

    const googleSat = L.tileLayer('https://mt1.google.com/vt/lyrs=y&x={{x}}&y={{y}}&z={{z}}', {{

      maxZoom: 22,

      attribution: 'Google Satellite'

    }}).addTo(map);



    // OpenStreetMap Layer

    const osm = L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{

      maxZoom: 19,

      attribution: 'OpenStreetMap'

    }});



    L.control.layers({{

      "Google Vệ Tinh (Hybrid)": googleSat,

      "OpenStreetMap": osm

    }}, null, {{ position: 'topright' }}).addTo(map);



    // Add Drone Orthomosaic Overlay

    let imageOverlay = L.imageOverlay(imageUrl, bounds, {{

      opacity: 0.95,

      interactive: true

    }}).addTo(map);



    map.fitBounds(bounds);



    // State for polygon points & trees

    let points = [];

    let markers = [];

    let polygonLayer = null;



    let isAddTreeMode = false;

    let isPickAnchorMode = false;

    let isPolygonLocked = false;

    let anchorTreeLat = null;

    let anchorTreeLon = null;

    let anchorMarker = null;



    let treeMarkers = [];

    let healthMarkers = [];



    function switchPickerMode(mode) {{

      const btnYolo = document.getElementById('btn-tab-yolo');

      const btnHealth = document.getElementById('btn-tab-health');

      const panelYolo = document.getElementById('panel-yolo');

      const panelHealth = document.getElementById('panel-health');



      if (mode === 'yolo') {{

        if (panelYolo) panelYolo.style.display = 'block';

        if (panelHealth) panelHealth.style.display = 'none';

        if (btnYolo) {{ btnYolo.style.background = '#06b6d4'; btnYolo.style.color = '#000'; }}

        if (btnHealth) {{ btnHealth.style.background = 'transparent'; btnHealth.style.color = '#94a3b8'; }}



        healthMarkers.forEach(m => map.removeLayer(m));

        healthMarkers = [];

        zoneLayers.forEach(l => map.removeLayer(l));

        zoneLayers = [];

        refreshAllTreeMarkers();

        updatePolygon();

      }} else {{

        if (panelYolo) panelYolo.style.display = 'none';

        if (panelHealth) panelHealth.style.display = 'block';

        if (btnHealth) {{ btnHealth.style.background = '#10b981'; btnHealth.style.color = '#000'; }}

        if (btnYolo) {{ btnYolo.style.background = 'transparent'; btnYolo.style.color = '#94a3b8'; }}



        treeMarkers.forEach(m => map.removeLayer(m));

        treeMarkers = [];

        loadFarmHealthMap();

      }}

    }}

    window.switchPickerMode = switchPickerMode;



    function loadBoundaryFromCsvPoints(bPoints) {{

      if (!bPoints || bPoints.length === 0) return;

      points = bPoints.map(b => [b.lon, b.lat]);

      markers.forEach(m => map.removeLayer(m));

      markers = [];

      points.forEach((pt, i) => {{

        const marker = L.circleMarker([pt[1], pt[0]], {{

          radius: 5,

          color: '#10b981',

          fillColor: '#34d399',

          fillOpacity: 1

        }}).addTo(map);

        marker.bindPopup(`<b>Điểm #${{i+1}} (Từ CSV Temp)</b><br>Lat: ${{pt[1]}}<br>Lon: ${{pt[0]}}`);

        markers.push(marker);

      }});

      updatePolygon();

      renderPointsList();

      if (points.length >= 3) {{

        isPolygonLocked = true;

        updatePolygonLockUI();

      }}

    }}

    window.loadBoundaryFromCsvPoints = loadBoundaryFromCsvPoints;



    async function fetchAndApplyCsvBoundary() {{

      try {{

        const resp = await fetch('/api/v1/yolo/trees/' + missionId);

        if (!resp.ok) return alert('Không tìm thấy file CSV của mission: ' + missionId);

        const data = await resp.json();

        if (data.boundary_points && data.boundary_points.length > 0) {{

          loadBoundaryFromCsvPoints(data.boundary_points);

          alert(`Đã nạp thành công ${{data.boundary_points.length}} điểm mốc ranh giới từ file CSV temp trên máy!`);

        }} else {{

          alert('File CSV trên máy chưa có điểm mốc BOUNDARY nào.');

        }}

      }} catch (e) {{

        alert('Lỗi nạp boundary: ' + e);

      }}

    }}

    window.fetchAndApplyCsvBoundary = fetchAndApplyCsvBoundary;

    let activeTreesList = [];

    let missingTreesList = [];

    let sensedRowSpacing = null;

    let sensedColSpacing = null;



    // Disease & Healthy Fruit Overlay State

    let diseaseDetections = [];

    let matchingToleranceMeters = 2.0;

    let isDiseaseOverlayEnabled = true;

    let isRawDetectionsEnabled = false;

    let rawDetectionMarkers = [];

    let rawDetectionLines = [];



    const CLASS_TRANSLATIONS = {{

      'Healthy_Fruit_Green': '🍏 Quả xanh khỏe',

      'Healthy_Fruit_Damaged': '🍏 Quả xước nhẹ',

      'Disease_Sun_Burn': '🔴 Cháy nắng',

      'Disease_White_Spot': '🔴 Đốm trắng',

      'Disease_Soft_Rot': '🔴 Thối mềm',

      'Disease_Brown_Spot': '🔴 Đốm nâu',

      'Disease_Anthracnose': '🔴 Thán thư'

    }};



    function getHaversineDistanceMeters(lat1, lon1, lat2, lon2) {{

      const R = 6371000;

      const dLat = (lat2 - lat1) * Math.PI / 180;

      const dLon = (lon2 - lon1) * Math.PI / 180;

      const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +

                Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *

                Math.sin(dLon / 2) * Math.sin(dLon / 2);

      const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

      return R * c;

    }}



    function togglePolygonLock() {{

      isPolygonLocked = !isPolygonLocked;

      updatePolygonLockUI();

    }}



    function updatePolygonLockUI() {{

      const btn = document.getElementById('btn-toggle-draw-polygon');

      if (!btn) return;

      if (isPolygonLocked) {{

        btn.innerHTML = '🔒 Viền đã khóa (Click để mở khóa vẽ tiếp)';

        btn.style.borderColor = '#10b981';

        btn.style.color = '#10b981';

      }} else {{

        btn.innerHTML = '✏️ Đang mở vẽ viền (Click map để thêm điểm)';

        btn.style.borderColor = '#38bdf8';

        btn.style.color = '#38bdf8';

      }}

    }}



    function applySensedSpacingAndRerun() {{

      if (sensedRowSpacing !== null && sensedColSpacing !== null) {{

        document.getElementById('input-row-spacing').value = sensedRowSpacing;

        document.getElementById('input-col-spacing').value = sensedColSpacing;

        runRealYoloInference();

      }}

    }}



    function setAnchorPoint(lat, lon) {{

      anchorTreeLat = lat;

      anchorTreeLon = lon;

      if (anchorMarker) map.removeLayer(anchorMarker);

      anchorMarker = L.circleMarker([lat, lon], {{

        radius: 10,

        color: '#f59e0b',

        fillColor: '#fbbf24',

        fillOpacity: 1,

        weight: 3,

        zIndexOffset: 1000

      }}).addTo(map);

      anchorMarker.bindPopup(`<b>📍 Cây Neo Mốc (Hàng 1 - Cột 1)</b><br>Lat: ${{lat}}<br>Lon: ${{lon}}`).openPopup();

      document.getElementById('anchor-badge').innerText = `Đã chọn (${{lat.toFixed(5)}}, ${{lon.toFixed(5)}})`;

      document.getElementById('anchor-badge').style.color = '#f59e0b';

      const clearBtn = document.getElementById('btn-clear-anchor');

      if (clearBtn) clearBtn.style.display = 'inline-block';

    }}



    function clearAnchorPoint() {{

      anchorTreeLat = null;

      anchorTreeLon = null;

      if (anchorMarker) {{

        map.removeLayer(anchorMarker);

        anchorMarker = null;

      }}

      document.getElementById('anchor-badge').innerText = 'Tự động';

      document.getElementById('anchor-badge').style.color = '#10b981';

      const clearBtn = document.getElementById('btn-clear-anchor');

      if (clearBtn) clearBtn.style.display = 'none';

      if (isPickAnchorMode) togglePickAnchorMode();

    }}



    function togglePickAnchorMode() {{

      isPickAnchorMode = !isPickAnchorMode;

      const btn = document.getElementById('btn-pick-anchor');

      if (isPickAnchorMode) {{

        btn.style.background = '#f59e0b';

        btn.style.color = '#000';

        btn.innerText = '🎯 Nhấp vào 1 cây góc trên map';

        if (isAddTreeMode) toggleAddTreeMode();

      }} else {{

        btn.style.background = '';

        btn.style.color = '';

        btn.innerText = '📍 Chọn Cây Neo (H1 - C1)';

      }}

    }}



    function toggleAddTreeMode() {{

      isAddTreeMode = !isAddTreeMode;

      const btn = document.getElementById('btn-add-tree-mode');

      if (isAddTreeMode) {{

        btn.style.background = '#06b6d4';

        btn.innerText = '🎯 Đang Chấm Trụ (Click bản đồ)';

        if (isPickAnchorMode) togglePickAnchorMode();

      }} else {{

        btn.style.background = '#334155';

        btn.innerText = '➕ Chấm Thêm Trụ';

      }}

    }}



    function addConfirmedTreeMarker(lat, lng, source, conf, row, col) {{

      let r = row;

      let c = col;



      if (r === undefined || c === undefined) {{

        // Kiểm tra xem điểm click có trúng ngay marker ô khuyết nào không (< 2m ~ 0.000018 độ)

        let closestMissing = null;

        let minDist = 999999;

        missingTreesList.forEach(m => {{

          const d = Math.hypot(m.lat - lat, m.lon - lng);

          if (d < minDist) {{

            minDist = d;

            closestMissing = m;

          }}

        }});



        if (closestMissing && minDist < 0.000018) {{

          r = closestMissing.row;

          c = closestMissing.col;

          treeMarkers = treeMarkers.filter(m => {{

            const p = m.getLatLng();

            if (Math.abs(p.lat - closestMissing.lat) < 0.00001 && Math.abs(p.lng - closestMissing.lon) < 0.00001) {{

              map.removeLayer(m);

              return false;

            }}

            return true;

          }});

          missingTreesList = missingTreesList.filter(m => m !== closestMissing);

        }} else {{

          r = 1;

          c = activeTreesList.length + 1;

        }}

      }}



      const m = L.circleMarker([lat, lng], {{

        radius: 7,

        color: '#10b981',

        fillColor: '#34d399',

        fillOpacity: 0.9,

        weight: 2,

        interactive: true,

        bubblingMouseEvents: false

      }}).addTo(map);

      m.bindPopup(`<b>🟢 Trụ Thanh Long (Hàng ${{r}} - Cột ${{c}})</b><br>Nguồn: ${{source}}<br>Lat: ${{lat}}<br>Lon: ${{lng}}`);

      m.on('click', function(ev) {{

        if (ev && ev.originalEvent) ev.originalEvent.stopPropagation();

        if (isPickAnchorMode) {{

          setAnchorPoint(lat, lng);

          togglePickAnchorMode();

          return;

        }}

        m.openPopup();

      }});

      treeMarkers.push(m);

      activeTreesList.push({{

        id: `tree_r${{r}}_c${{c}}`,

        row: r,

        col: c,

        lat: lat,

        lon: lng,

        confidence: conf !== undefined ? conf : 1.0,

        status: 'DETECTED',

        source: source

      }});

      updateTreeCounters();

    }}



    function addMissingTreeMarker(tree) {{

      const m = L.circleMarker([tree.lat, tree.lon], {{

        radius: 10,

        color: '#dc2626',

        fillColor: '#f87171',

        fillOpacity: 0.9,

        weight: 3,

        dashArray: '4, 4',

        interactive: true,

        bubblingMouseEvents: false

      }}).addTo(map);



      m.bindTooltip(`🔴 Khuyết: H${{tree.row}}-C${{tree.col}}`, {{

        permanent: false,

        direction: 'top'

      }});



      const popupHtml = `

        <div style="font-family: sans-serif; font-size: 12px; line-height: 1.5;">

          <b style="color: #ef4444; font-size: 13px;">🔴 Vị Trí Nghi Ngờ Khuyết Cây</b><br>

          Hàng: <b>${{tree.row}}</b> | Cột: <b>${{tree.col}}</b><br>

          <span style="color: #94a3b8; font-size: 11px;">Tọa độ: ${{tree.lat.toFixed(6)}}, ${{tree.lon.toFixed(6)}}</span>

          <div style="margin-top: 6px; padding: 4px; background: rgba(0,0,0,0.3); border-radius: 4px;">

            <div style="display: flex; align-items: center; justify-content: space-between; gap: 4px;">

              <span style="font-size: 11px; color: #94a3b8;">Sửa vị trí:</span>

              <label style="font-size: 11px; color: #cbd5e1;">Hàng:</label>

              <input type="number" id="edit-row-${{tree.id}}" value="${{tree.row}}" min="1" max="999" style="width: 42px; padding: 2px 4px; background: #0f172a; color: #ef4444; border: 1px solid #334155; border-radius: 3px; font-size: 11px; font-weight: 700; text-align: center;">

              <label style="font-size: 11px; color: #cbd5e1;">Cột:</label>

              <input type="number" id="edit-col-${{tree.id}}" value="${{tree.col}}" min="1" max="999" style="width: 42px; padding: 2px 4px; background: #0f172a; color: #ef4444; border: 1px solid #334155; border-radius: 3px; font-size: 11px; font-weight: 700; text-align: center;">

              <button style="padding: 2px 6px; background: #ef4444; color: #fff; border: none; border-radius: 3px; font-size: 11px; font-weight: 700; cursor: pointer;" onclick="saveManualMissingRowCol('${{tree.id}}')">💾</button>

            </div>

          </div>

          <div style="margin-top: 8px; display: flex; flex-direction: column; gap: 6px;">

            <button style="padding:6px 10px; background:#10b981; border:none; border-radius:4px; color:#000; cursor:pointer; font-weight:700; font-size:11px;" onclick="confirmMissingTree(${{tree.lat}}, ${{tree.lon}})">

              ✅ Xác nhận có cây thực tế tại đây

            </button>

            <button style="padding:6px 10px; background:#ef4444; border:none; border-radius:4px; color:#fff; cursor:pointer; font-weight:700; font-size:11px;" onclick="dismissMissingTree(${{tree.lat}}, ${{tree.lon}})">

              ❌ Xóa điểm khuyết này (Đánh lại thứ tự hàng)

            </button>

          </div>

        </div>

      `;

      m.bindPopup(popupHtml);



      m.on('click', function(ev) {{

        if (ev && ev.originalEvent) ev.originalEvent.stopPropagation();

        m.openPopup();

      }});

      treeMarkers.push(m);

    }}



    window.confirmMissingTree = function(lat, lon) {{

      treeMarkers = treeMarkers.filter(m => {{

        const p = m.getLatLng();

        if (Math.abs(p.lat - lat) < 0.00001 && Math.abs(p.lng - lon) < 0.00001) {{

          map.removeLayer(m);

          return false;

        }}

        return true;

      }});

      const missingTree = missingTreesList.find(t => Math.abs(t.lat - lat) < 0.00001 && Math.abs(t.lon - lon) < 0.00001);

      missingTreesList = missingTreesList.filter(t => !(Math.abs(t.lat - lat) < 0.00001 && Math.abs(t.lon - lon) < 0.00001));

      const row = missingTree ? missingTree.row : 1;

      const col = missingTree ? missingTree.col : (activeTreesList.length + 1);

      addConfirmedTreeMarker(lat, lon, 'Người dùng xác nhận', 1.0, row, col);

    }};



    window.dismissMissingTree = function(lat, lon) {{

      const target = missingTreesList.find(t => Math.abs(t.lat - lat) < 0.00001 && Math.abs(t.lon - lon) < 0.00001);

      if (!target) return;



      const targetRow = target.row;

      const targetCol = target.col;



      // 1. Xóa marker trên bản đồ

      treeMarkers = treeMarkers.filter(m => {{

        const p = m.getLatLng();

        if (Math.abs(p.lat - lat) < 0.00001 && Math.abs(p.lng - lon) < 0.00001) {{

          map.removeLayer(m);

          return false;

        }}

        return true;

      }});



      // 2. Xóa khỏi danh sách missingTreesList

      missingTreesList = missingTreesList.filter(t => t !== target);



      // 3. Đánh lại số thứ tự cột trong HÀNG ĐÓ:

      // Các cây đứng sau điểm khuyết bị xóa (col > targetCol) sẽ lùi 1 đơn vị (col -= 1)

      activeTreesList.forEach(t => {{

        if (t.row === targetRow && t.col > targetCol) {{

          t.col -= 1;

          t.id = `tree_r${{t.row}}_c${{t.col}}`;

        }}

      }});



      missingTreesList.forEach(m => {{

        if (m.row === targetRow && m.col > targetCol) {{

          m.col -= 1;

          m.id = `missing_r${{m.row}}_c${{m.col}}`;

        }}

      }});



      // 4. Cập nhật lại giao diện và marker

      matchDetectionsToTrees();

      refreshAllTreeMarkers();



      updateTreeCounters();

    }};



    window.deleteActiveTree = function(id, lat, lon) {{

      const target = activeTreesList.find(t => t.id === id || (Math.abs(t.lat - lat) < 0.00001 && Math.abs(t.lon - lon) < 0.00001));

      if (!target) return;



      const targetRow = target.row;

      const targetCol = target.col;



      treeMarkers = treeMarkers.filter(m => {{

        const p = m.getLatLng();

        if (Math.abs(p.lat - lat) < 0.00001 && Math.abs(p.lng - lon) < 0.00001) {{

          map.removeLayer(m);

          return false;

        }}

        return true;

      }});



      activeTreesList = activeTreesList.filter(t => t !== target);



      activeTreesList.forEach(t => {{

        if (t.row === targetRow && t.col > targetCol) {{

          t.col -= 1;

          t.id = `tree_r${{t.row}}_c${{t.col}}`;

        }}

      }});



      missingTreesList.forEach(m => {{

        if (m.row === targetRow && m.col > targetCol) {{

          m.col -= 1;

          m.id = `missing_r${{m.row}}_c${{m.col}}`;

        }}

      }});



      matchDetectionsToTrees();

      refreshAllTreeMarkers();



      updateTreeCounters();

    }};



    window.openImageModal = function(imgUrl, title, infoText) {{

      const modal = document.getElementById('image-modal');

      const modalImg = document.getElementById('modal-img');

      const modalTitle = document.getElementById('modal-title');

      const modalInfo = document.getElementById('modal-info');

      if (!modal || !modalImg) return;

      modalImg.src = imgUrl;

      if (modalTitle) modalTitle.innerText = title || 'Chi tiết ảnh nhận diện';

      if (modalInfo) modalInfo.innerHTML = infoText || '';

      modal.style.display = 'flex';

    }};



    window.closeImageModal = function() {{

      const modal = document.getElementById('image-modal');

      if (modal) modal.style.display = 'none';

    }};



    function updateDiseaseStatsBox(normal, healthy, diseased, unmatched, breakdown) {{

      const box = document.getElementById('disease-stats-box');

      if (!box) return;

      box.style.display = 'block';

      const elN = document.getElementById('stat-normal-trees');

      const elH = document.getElementById('stat-healthy-trees');

      const elD = document.getElementById('stat-diseased-trees');

      const elU = document.getElementById('stat-unmatched-detections');

      const elB = document.getElementById('disease-breakdown');



      if (elN) elN.innerText = normal;

      if (elH) elH.innerText = healthy;

      if (elD) elD.innerText = diseased;

      if (elU) elU.innerText = unmatched;



      if (elB) {{

        let bHtml = '';

        for (const [cls, cnt] of Object.entries(breakdown)) {{

          const vi = CLASS_TRANSLATIONS[cls] || cls;

          bHtml += `<div style="display: flex; justify-content: space-between;"><span>${{vi}}:</span><b style="color: #fff;">${{cnt}} vết</b></div>`;

        }}

        elB.innerHTML = bHtml;

      }}

    }}



    function renderRawDetectionMarkers() {{

      rawDetectionMarkers.forEach(m => map.removeLayer(m));

      rawDetectionMarkers = [];

      rawDetectionLines.forEach(l => map.removeLayer(l));

      rawDetectionLines = [];



      if (!isRawDetectionsEnabled || !diseaseDetections || diseaseDetections.length === 0) return;



      diseaseDetections.forEach(d => {{

        const isH = (d.class_name || '').toLowerCase().startsWith('healthy');

        const color = isH ? '#06b6d4' : '#ef4444';

        const viName = CLASS_TRANSLATIONS[d.class_name] || d.class_name;



        const m = L.circleMarker([d.lat, d.lon], {{

          radius: 5,

          color: color,

          fillColor: '#ffffff',

          fillOpacity: 0.95,

          weight: 2

        }}).addTo(map);



        const cropImgTag = d.crop_image_url ? `<img src="${{d.crop_image_url}}" style="width: 100%; height: 90px; object-fit: cover; margin-top: 4px; border-radius: 4px; cursor: pointer;" onclick="openImageModal('${{d.crop_image_url}}', '${{viName}}', 'Mã: ${{d.id}}')">` : '';



        m.bindPopup(`

          <div style="font-family: sans-serif; font-size: 11px;">

            <b style="color: ${{color}};">${{viName}}</b><br>

            Mã vết: ${{d.id}}<br>

            Độ tin cậy: <b>${{((d.confidence || 0) * 100).toFixed(1)}}%</b><br>

            Tọa độ: ${{d.lat.toFixed(6)}}, ${{d.lon.toFixed(6)}}<br>

            ${{cropImgTag}}

          </div>

        `);

        rawDetectionMarkers.push(m);



        const matchedTree = activeTreesList.find(t => t.matchedDetections && t.matchedDetections.includes(d));

        if (matchedTree) {{

          const line = L.polyline([[d.lat, d.lon], [matchedTree.lat, matchedTree.lon]], {{

            color: color,

            weight: 1.5,

            opacity: 0.6,

            dashArray: '3, 4'

          }}).addTo(map);

          rawDetectionLines.push(line);

        }}

      }});

    }}



    function matchDetectionsToTrees() {{

      activeTreesList.forEach(t => {{

        t.matchedDetections = [];

        t.health_status = 'NORMAL';

      }});



      if (!isDiseaseOverlayEnabled || !diseaseDetections || diseaseDetections.length === 0) {{

        updateDiseaseStatsBox(activeTreesList.length, 0, 0, 0, {{}});

        renderRawDetectionMarkers();

        return;

      }}



      let unmatchedCount = 0;

      const diseaseSummary = {{}};



      diseaseDetections.forEach(det => {{

        const cName = det.class_name || 'Unknown';

        diseaseSummary[cName] = (diseaseSummary[cName] || 0) + 1;



        let closestTree = null;

        let minDistance = Infinity;



        activeTreesList.forEach(t => {{

          const dist = getHaversineDistanceMeters(t.lat, t.lon, det.lat, det.lon);

          if (dist < minDistance) {{

            minDistance = dist;

            closestTree = t;

          }}

        }});



        if (closestTree && minDistance <= matchingToleranceMeters) {{

          det.distance_meters = minDistance;

          closestTree.matchedDetections.push(det);

        }} else {{

          det.distance_meters = minDistance;

          unmatchedCount++;

        }}

      }});



      let normalCount = 0;

      let healthyCount = 0;

      let diseasedCount = 0;



      activeTreesList.forEach(t => {{

        const diseases = t.matchedDetections.filter(d => !d.class_name.toLowerCase().startsWith('healthy'));

        const fruits = t.matchedDetections.filter(d => d.class_name.toLowerCase().startsWith('healthy'));



        if (diseases.length > 0) {{

          t.health_status = 'DISEASE';

          diseasedCount++;

        }} else if (fruits.length > 0) {{

          t.health_status = 'HEALTHY_FRUIT';

          healthyCount++;

        }} else {{

          t.health_status = 'NORMAL';

          normalCount++;

        }}

      }});



      updateDiseaseStatsBox(normalCount, healthyCount, diseasedCount, unmatchedCount, diseaseSummary);

      renderRawDetectionMarkers();

    }}



    async function loadDiseaseDetections(customMissionId) {{

      const badge = document.getElementById('badge-disease-status');

      const missionInput = document.getElementById('input-disease-mission');

      let targetMission = customMissionId || (missionInput ? missionInput.value.trim() : '') || '17bfaee6-2e55-4c94-ae35-225df0d99d4d';



      if (badge) {{

        badge.innerText = 'Đang tải...';

        badge.style.background = '#38bdf8';

      }}



      try {{

        let resp = await fetch('/api/v1/disease/detections/' + encodeURIComponent(targetMission));

        if (!resp.ok) {{

          throw new Error('Không thể tải dữ liệu bệnh từ mission ' + targetMission);

        }}

        let data = await resp.json();

        if (!data.detections || data.detections.length === 0) {{

          if (targetMission !== '17bfaee6-2e55-4c94-ae35-225df0d99d4d') {{

            resp = await fetch('/api/v1/disease/detections/17bfaee6-2e55-4c94-ae35-225df0d99d4d');

            if (resp.ok) {{

              data = await resp.json();

              targetMission = '17bfaee6-2e55-4c94-ae35-225df0d99d4d';

              if (missionInput) missionInput.value = targetMission;

            }}

          }}

        }}



        diseaseDetections = data.detections || [];





        matchDetectionsToTrees();

        refreshAllTreeMarkers();





        if (badge) {{

          badge.innerText = `Đã nạp ${{diseaseDetections.length}} vết`;

          badge.style.background = '#10b981';

        }}

      }} catch(err) {{

        if (badge) {{

          badge.innerText = 'Lỗi nạp';

          badge.style.background = '#ef4444';

        }}

      }}

    }}



    function onToleranceChange(val) {{

      matchingToleranceMeters = parseFloat(val) || 2.0;

      const tVal = document.getElementById('tolerance-val');

      if (tVal) tVal.innerText = matchingToleranceMeters.toFixed(1) + ' m';



        matchDetectionsToTrees();

        refreshAllTreeMarkers();



    }}



    function toggleDiseaseOverlay() {{

      const chk = document.getElementById('chk-show-disease-markers');

      isDiseaseOverlayEnabled = chk ? chk.checked : true;



        matchDetectionsToTrees();

        refreshAllTreeMarkers();



    }}



    function toggleRawDetections() {{

      const chk = document.getElementById('chk-show-raw-detections');

      isRawDetectionsEnabled = chk ? chk.checked : false;

      renderRawDetectionMarkers();

    }}



    function refreshAllTreeMarkers() {{

      treeMarkers.forEach(m => map.removeLayer(m));

      treeMarkers = [];

      if (anchorMarker) {{

        map.removeLayer(anchorMarker);

        anchorMarker = null;

      }}



      activeTreesList.forEach(t => {{

        const isAnchor = t.is_anchor;

        let markerColor = '#10b981';

        let markerFill = '#34d399';

        let markerRadius = 7;

        let markerWeight = 2;

        let badgeHtml = '🟢 Trụ Bình Thường';

        let badgeColor = '#10b981';

        let tooltipText = `H${{t.row}}-C${{t.col}}`;



        if (isAnchor) {{

          markerColor = '#f59e0b';

          markerFill = '#fbbf24';

          markerRadius = 10;

          markerWeight = 3;

          badgeHtml = '📍 Cây Neo Mốc (H1-C1)';

          badgeColor = '#f59e0b';

          tooltipText = `📍 Neo H${{t.row}}-C${{t.col}}`;

        }} else if (isDiseaseOverlayEnabled && t.health_status === 'DISEASE') {{

          // 🔴 Cây Bệnh: Viền đỏ đậm, ruột vàng cam nổi bật cảnh báo

          markerColor = '#dc2626';

          markerFill = '#f59e0b';

          markerRadius = 9;

          markerWeight = 3;

          const disNames = (t.matchedDetections || [])

            .filter(d => !d.class_name.toLowerCase().startsWith('healthy'))

            .map(d => CLASS_TRANSLATIONS[d.class_name] || d.class_name)

            .filter((v, i, a) => a.indexOf(v) === i)

            .join(', ');

          badgeHtml = `⚠️ Cây Bệnh: ${{disNames || 'Nhiễm bệnh'}}`;

          badgeColor = '#ef4444';

          tooltipText = `🔴 H${{t.row}}-C${{t.col}}: ${{disNames}}`;

        }} else if (isDiseaseOverlayEnabled && t.health_status === 'HEALTHY_FRUIT') {{

          // 🍏 Cây Có Trái Khỏe: Xanh ngọc / Cyan sáng bắt mắt (khác hoàn toàn xanh lá thường)

          markerColor = '#0284c7';

          markerFill = '#06b6d4';

          markerRadius = 8;

          markerWeight = 2.5;

          badgeHtml = '🍏 Trụ Có Trái Khỏe';

          badgeColor = '#06b6d4';

          tooltipText = `🍏 H${{t.row}}-C${{t.col}}: Trái khỏe (${{(t.matchedDetections || []).length}})`;

        }} else {{

          // 🟢 Cây Không Có Dữ Liệu Detect: Màu xanh lá chuẩn bình thường

          markerColor = '#10b981';

          markerFill = '#34d399';

          markerRadius = 7;

          markerWeight = 2;

          badgeHtml = '🟢 Trụ Bình Thường';

          badgeColor = '#10b981';

          tooltipText = `🟢 H${{t.row}}-C${{t.col}}`;

        }}

        const m = L.circleMarker([t.lat, t.lon], {{

          radius: markerRadius,

          color: markerColor,

          fillColor: markerFill,

          fillOpacity: isAnchor ? 1.0 : 0.85,

          weight: markerWeight,

          interactive: true,

          bubblingMouseEvents: false

        }}).addTo(map);



        m.bindTooltip(tooltipText, {{ direction: 'top' }});



        let detectionsHtml = '';

        if (t.matchedDetections && t.matchedDetections.length > 0) {{

          detectionsHtml += `

            <div style="margin-top: 8px; border-top: 1px solid rgba(255,255,255,0.15); padding-top: 6px;">

              <div style="font-weight: 700; color: #fbbf24; font-size: 11px; margin-bottom: 4px;">

                🔍 Vết nhận diện (${{t.matchedDetections.length}} đối tượng):

              </div>

              <div style="display: flex; flex-direction: column; gap: 6px; max-height: 180px; overflow-y: auto;">

          `;

          t.matchedDetections.forEach(d => {{

            const isH = (d.class_name || '').toLowerCase().startsWith('healthy');

            const viName = CLASS_TRANSLATIONS[d.class_name] || d.class_name;

            const itemColor = isH ? '#06b6d4' : '#ef4444';

            const cropThumb = d.crop_image_url ? `<img src="${{d.crop_image_url}}" style="width: 48px; height: 48px; object-fit: cover; border-radius: 4px; border: 1px solid ${{itemColor}}; cursor: pointer;" onclick="openImageModal('${{d.crop_image_url}}', '${{viName}}', 'Độ tin cậy: ${{((d.confidence || 0) * 100).toFixed(1)}}% | Lệch GPS: ${{d.distance_meters.toFixed(2)}}m')">` : '';

            const annLink = d.annotated_image_url ? `<a href="${{d.annotated_image_url}}" target="_blank" style="color: #38bdf8; text-decoration: underline; font-size: 10px;">Toàn cảnh 🔍</a>` : '';

            detectionsHtml += `

              <div style="display: flex; gap: 8px; align-items: center; background: rgba(0,0,0,0.3); padding: 4px 6px; border-radius: 4px; border-left: 3px solid ${{itemColor}};">

                ${{cropThumb}}

                <div style="flex: 1; font-size: 11px; line-height: 1.3;">

                  <div style="font-weight: 700; color: ${{itemColor}};">${{viName}}</div>

                  <div style="color: #94a3b8; font-size: 10px;">Tin cậy: <b style="color: #fff;">${{((d.confidence || 0) * 100).toFixed(1)}}%</b> | Lệch: <b style="color: #fbbf24;">${{d.distance_meters.toFixed(2)}}m</b></div>

                  <div style="margin-top: 2px;">${{annLink}}</div>

                </div>

              </div>

            `;

          }});

          detectionsHtml += `</div></div>`;

        }} else if (isDiseaseOverlayEnabled) {{

          detectionsHtml = '<div style="margin-top: 6px; font-size: 11px; color: #10b981;">✅ Trụ bình thường (chưa phát hiện sâu bệnh hoặc quả).</div>';

        }}



        const popupContent = `

          <div style="font-family: sans-serif; font-size: 12px; line-height: 1.5;">

            <b style="color: ${{badgeColor}}; font-size: 13px;">${{badgeHtml}}</b><br>

            Hàng: <b>${{t.row}}</b> | Cột: <b>${{t.col}}</b><br>

            Độ tin cậy: <b>${{((t.confidence || 1.0) * 100).toFixed(0)}}%</b><br>

            <span style="color: #94a3b8; font-size: 11px;">Tọa độ: ${{t.lat.toFixed(6)}}, ${{t.lon.toFixed(6)}}</span><br>

            ${{detectionsHtml}}

            <div style="margin-top: 6px; padding: 4px; background: rgba(0,0,0,0.3); border-radius: 4px;">

              <div style="display: flex; align-items: center; justify-content: space-between; gap: 4px;">

                <span style="font-size: 11px; color: #94a3b8;">Sửa vị trí:</span>

                <label style="font-size: 11px; color: #cbd5e1;">Hàng:</label>

                <input type="number" id="edit-row-${{t.id}}" value="${{t.row}}" min="1" max="999" style="width: 42px; padding: 2px 4px; background: #0f172a; color: #38bdf8; border: 1px solid #334155; border-radius: 3px; font-size: 11px; font-weight: 700; text-align: center;">

                <label style="font-size: 11px; color: #cbd5e1;">Cột:</label>

                <input type="number" id="edit-col-${{t.id}}" value="${{t.col}}" min="1" max="999" style="width: 42px; padding: 2px 4px; background: #0f172a; color: #38bdf8; border: 1px solid #334155; border-radius: 3px; font-size: 11px; font-weight: 700; text-align: center;">

                <button style="padding: 2px 6px; background: #38bdf8; color: #000; border: none; border-radius: 3px; font-size: 11px; font-weight: 700; cursor: pointer;" onclick="saveManualTreeRowCol('${{t.id}}')">💾</button>

              </div>

            </div>

            <div style="margin-top: 6px; display: flex; gap: 6px;">

              <button style="flex: 1; padding:4px 8px; background:#ef4444; border:none; border-radius:4px; color:#fff; cursor:pointer; font-size:11px; font-weight:600;" onclick="deleteActiveTree('${{t.id}}', ${{t.lat}}, ${{t.lon}})">

                🗑️ Xóa cây

              </button>

              <button style="flex: 1; padding:4px 8px; background:#f59e0b; border:none; border-radius:4px; color:#000; cursor:pointer; font-size:11px; font-weight:700;" onclick="setAnchorPoint(${{t.lat}}, ${{t.lon}})">

                📍 Đặt làm Neo

              </button>

            </div>

          </div>

        `;

        m.bindPopup(popupContent);



        m.on('click', function(ev) {{

          if (ev && ev.originalEvent) ev.originalEvent.stopPropagation();

          if (isPickAnchorMode) {{

            setAnchorPoint(t.lat, t.lon);

            togglePickAnchorMode();

            return;

          }}

          m.openPopup();

        }});

        treeMarkers.push(m);

      }});



      missingTreesList.forEach(t => addMissingTreeMarker(t));

    }}



    window.saveManualTreeRowCol = function(treeId) {{

      const rInput = document.getElementById(`edit-row-${{treeId}}`);

      const cInput = document.getElementById(`edit-col-${{treeId}}`);

      if (!rInput || !cInput) return;

      const nr = parseInt(rInput.value);

      const nc = parseInt(cInput.value);

      if (isNaN(nr) || isNaN(nc) || nr < 1 || nc < 1) {{

        alert("Vui lòng nhập Hàng và Cột >= 1");

        return;

      }}

      const t = activeTreesList.find(x => x.id === treeId);

      if (t) {{

        t.row = nr;

        t.col = nc;

        t.id = `tree_r${{nr}}_c${{nc}}`;



        matchDetectionsToTrees();

        refreshAllTreeMarkers();



        updateTreeCounters();

      }}

    }};



    window.saveManualMissingRowCol = function(treeId) {{

      const rInput = document.getElementById(`edit-row-${{treeId}}`);

      const cInput = document.getElementById(`edit-col-${{treeId}}`);

      if (!rInput || !cInput) return;

      const nr = parseInt(rInput.value);

      const nc = parseInt(cInput.value);

      if (isNaN(nr) || isNaN(nc) || nr < 1 || nc < 1) {{

        alert("Vui lòng nhập Hàng và Cột >= 1");

        return;

      }}

      const m = missingTreesList.find(x => x.id === treeId);

      if (m) {{

        m.row = nr;

        m.col = nc;

        m.id = `missing_r${{nr}}_c${{nc}}`;



        matchDetectionsToTrees();

        refreshAllTreeMarkers();



        updateTreeCounters();

      }}

    }};



    window.renumberAllRowsAndColumns = async function() {{

      if (!activeTreesList || activeTreesList.length === 0) {{

        alert("Chưa có danh sách cây để tính toán!");

        return;

      }}



      const btn = document.getElementById('btn-renumber-grid');

      const oldText = btn ? btn.innerText : '';

      if (btn) {{

        btn.disabled = true;

        btn.innerText = '⏳ Đang phân chia Hàng & Cột chuẩn xác...';

      }}



      try {{

        let polygonJson = null;

        const geoOutput = document.getElementById('geojson-output');

        if (geoOutput && geoOutput.value) {{

          try {{ polygonJson = JSON.parse(geoOutput.value); }} catch(e) {{}}

        }}



        const rowSpacing = parseFloat(document.getElementById('input-row-spacing').value) || 2.5;

        const colSpacing = parseFloat(document.getElementById('input-col-spacing').value) || 2.5;



        const reqBody = {{

          mission_id: missionId,

          active_trees: activeTreesList,

          row_spacing: rowSpacing,

          col_spacing: colSpacing,

          anchor_lat: anchorTreeLat,

          anchor_lon: anchorTreeLon,

          polygon_geojson: polygonJson

        }};



        const resp = await fetch('/api/v1/yolo/recalculate-grid', {{

          method: 'POST',

          headers: {{ 'Content-Type': 'application/json' }},

          body: JSON.stringify(reqBody)

        }});



        if (!resp.ok) {{

          const err = await resp.json();

          throw new Error(err.detail || 'Lỗi khi tính toán lại lưới');

        }}



        const data = await resp.json();

        activeTreesList = data.active_trees || [];

        missingTreesList = data.missing_trees || [];





        matchDetectionsToTrees();

        refreshAllTreeMarkers();



        updateTreeCounters();

        document.getElementById('grid-dimensions-val').innerText = `${{data.max_row || 0}} hàng x ${{data.max_col || 0}} cột`;



        alert(`✅ Đã phân chia Hàng & Cột chuẩn xác!\n- Cây Neo luôn là Hàng 1 - Cột 1\n- Tổng: ${{activeTreesList.length}} cây nhận diện\n- Khuyết/sót: ${{missingTreesList.length}} cây\n- Kích thước: ${{data.max_row}} hàng x ${{data.max_col}} cột`);

      }} catch(err) {{

        alert("Lỗi: " + err.message);

      }} finally {{

        if (btn) {{

          btn.disabled = false;

          btn.innerText = oldText;

        }}

      }}

    }};



    async function downloadCurrentTreesCsv() {{

      if ((!activeTreesList || activeTreesList.length === 0) && (!points || points.length === 0)) {{

        alert("Chưa có danh sách cây hoặc ranh giới viền để tải!");

        return;

      }}



      let csv = "tree_id,row,col,latitude,longitude,confidence,status,source\\n";

      const trees = [];



      // 1. Thêm các điểm mốc ranh giới viền đất (Boundary/Border) người dùng đã chọn

      if (points && points.length > 0) {{

        points.forEach((pt, idx) => {{

          const bLat = pt[1];

          const bLon = pt[0];

          const bId = `boundary_point_${{idx + 1}}`;

          trees.push({{ treeId: bId, row: 0, col: 0, latitude: bLat, longitude: bLon, confidence: 1.0, status: 'BOUNDARY', source: 'USER_POLYGON' }});

          csv += `${{bId}},0,0,${{bLat}},${{bLon}},1.0,BOUNDARY,USER_POLYGON\\n`;

        }});

      }}



      // 2. Thêm danh sách cây đã nhận diện

      activeTreesList.forEach((t, i) => {{

        const id = t.id || `tree_r${{t.row || 1}}_c${{t.col || (i+1)}}`;

        const row = t.row || 1;

        const col = t.col || (i + 1);

        const conf = t.confidence !== undefined ? t.confidence : 1.0;

        const status = t.status || 'DETECTED';

        const source = t.source || 'YOLO';

        trees.push({{ treeId: id, row, col, latitude: t.lat, longitude: t.lon, confidence: conf, status, source }});

        csv += `${{id}},${{row}},${{col}},${{t.lat}},${{t.lon}},${{conf}},${{status}},${{source}}\\n`;

      }});



      // 3. Thêm danh sách cây nghi ngờ khuyết

      missingTreesList.forEach(m => {{

        const id = m.id || `missing_r${{m.row}}_c${{m.col}}`;

        trees.push({{ treeId: id, row: m.row, col: m.col, latitude: m.lat, longitude: m.lon, confidence: 0, status: 'MISSING', source: 'INTERPOLATED_GRID' }});

        csv += `${{id}},${{m.row}},${{m.col}},${{m.lat}},${{m.lon}},0.0,MISSING,INTERPOLATED_GRID\\n`;

      }});



      const withoutGps = trees.filter(t => !Number.isFinite(t.latitude) || !Number.isFinite(t.longitude));

      if (withoutGps.length > 0) {{

        alert(`${{withoutGps.length}} cây chưa có GPS hợp lệ. Hãy bổ sung tọa độ trước khi lưu và tải CSV.`);

        return;

      }}



      // 1. Luôn tải file CSV về máy ngay lập tức

      const blob = new Blob([csv], {{ type: 'text/csv;charset=utf-8;' }});

      const url = URL.createObjectURL(blob);

      const a = document.createElement('a');

      a.href = url;

      a.download = `trees_${{missionId}}_latest.csv`;

      document.body.appendChild(a);

      a.click();

      document.body.removeChild(a);

      URL.revokeObjectURL(url);



      // 2. Tùy chọn đồng bộ vào .NET API (PostgreSQL) nếu đã nhập Farm ID và Token

      const farmId = document.getElementById('plant-gps-farm-id') ? document.getElementById('plant-gps-farm-id').value.trim() : '';

      const apiUrl = document.getElementById('plant-gps-api-url') ? document.getElementById('plant-gps-api-url').value.trim().replace(/\\/$/, '') : '';

      const token = document.getElementById('plant-gps-token') ? document.getElementById('plant-gps-token').value.trim() : '';

      if (farmId && apiUrl && token) {{

        const button = document.getElementById('btn-download-csv');

        if (button) button.disabled = true;

        try {{

          const response = await fetch(`${{apiUrl}}/api/system-manager/farms/${{encodeURIComponent(farmId)}}/missions/${{encodeURIComponent(missionId)}}/plant-gps`, {{

            method: 'POST',

            headers: {{ 'Content-Type': 'application/json', 'Authorization': `Bearer ${{token}}` }},

            body: JSON.stringify({{ trees }})

          }});

          if (!response.ok) throw new Error(`API trả về HTTP ${{response.status}}`);

          alert('✅ Đã tải file CSV và đồng bộ thành công vào PostgreSQL Database!');

        }} catch (error) {{

          alert(`⚠️ Đã tải file CSV về máy, nhưng đồng bộ vào PostgreSQL thất bại: ${{error.message}}`);

        }} finally {{

          if (button) button.disabled = false;

        }}

      }}

    }}



    function updateTreeCounters() {{

      document.getElementById('tree-stats').style.display = 'flex';

      document.getElementById('active-trees-count').innerText = activeTreesList.length;

      document.getElementById('missing-trees-count').innerText = missingTreesList.length;

    }}



    // Track mouse coordinates

    map.on('mousemove', function(e) {{

      document.getElementById('mouse-coords').innerText = e.latlng.lat.toFixed(7) + ', ' + e.latlng.lng.toFixed(7);

    }});



    // Click map to add points or trees

    map.on('click', function(e) {{

      const lat = parseFloat(e.latlng.lat.toFixed(7));

      const lng = parseFloat(e.latlng.lng.toFixed(7));



      if (isPickAnchorMode) {{

        setAnchorPoint(lat, lng);

        togglePickAnchorMode();

        return;

      }}



      if (isAddTreeMode) {{

        addConfirmedTreeMarker(lat, lng, 'Người dùng bổ sung', 1.0);

        return;

      }}



      // Nếu viền vườn đã khóa (hoặc đã chạy YOLO), không tự ý chèn thêm mốc làm méo vườn

      if (isPolygonLocked) {{

        return;

      }}

      

      points.push([lng, lat]); // GeoJSON is [Lon, Lat]



      const marker = L.circleMarker([lat, lng], {{

        radius: 5,

        color: '#10b981',

        fillColor: '#34d399',

        fillOpacity: 1

      }}).addTo(map);

      marker.bindPopup(`<b>Điểm #${{points.length}}</b><br>Lat: ${{lat}}<br>Lon: ${{lng}}`).openPopup();

      markers.push(marker);



      updatePolygon();

    }});



    function changeOpacity(val) {{

      const op = val / 100;

      imageOverlay.setOpacity(op);

      document.getElementById('opacity-val').innerText = val + '%';

    }}



    function updatePolygon() {{

      if (polygonLayer) {{

        map.removeLayer(polygonLayer);

      }}



      document.getElementById('point-count').innerText = points.length;



      // Update points list in sidebar

      const container = document.getElementById('points-container');

      if (points.length === 0) {{

        container.innerHTML = '<div style="color: var(--text-muted); font-size: 12px;">Chưa có điểm nào. Hãy click lên ảnh.</div>';

        document.getElementById('geojson-output').value = '';

        document.getElementById('area-val').innerText = '0 ha';

        return;

      }}



      let html = '';

      points.forEach((pt, i) => {{

        html += `<div class="point-item"><span>#${{i+1}}: ${{pt[1]}}, ${{pt[0]}}</span></div>`;

      }});

      container.innerHTML = html;



      // Build closed GeoJSON coordinates

      const closedCoords = [...points];

      if (points.length >= 3) {{

        closedCoords.push(points[0]); // Close ring

      }}



      const geojsonObj = {{

        "type": "Polygon",

        "coordinates": [closedCoords]

      }};



      document.getElementById('geojson-output').value = JSON.stringify(geojsonObj, null, 2);



      // Draw Leaflet polygon if >= 2 points

      if (points.length >= 2) {{

        const latLngs = points.map(pt => [pt[1], pt[0]]);

        polygonLayer = L.polygon(latLngs, {{

          color: '#06b6d4',

          fillColor: '#06b6d4',

          fillOpacity: 0.25,

          weight: 2,

          dashArray: '4, 4'

        }}).addTo(map);



        // Approximate area calculation

        if (points.length >= 3) {{

          const areaM2 = L.GeometryUtil ? L.GeometryUtil.geodesicArea(latLngs) : 0;

          if (areaM2 > 0) {{

            const ha = (areaM2 / 10000).toFixed(2);

            document.getElementById('area-val').innerText = ha + ' ha';

          }}

        }}

      }}



      try {{

        localStorage.setItem('agridrone_polygon_' + missionId, JSON.stringify(points));

      }} catch (e) {{}}

    }}



    function undoPoint() {{

      if (points.length > 0) {{

        points.pop();

        const m = markers.pop();

        if (m) map.removeLayer(m);

        updatePolygon();

      }}

    }}



    function clearAllPoints() {{

      points = [];

      markers.forEach(m => map.removeLayer(m));

      markers = [];

      if (polygonLayer) map.removeLayer(polygonLayer);

      polygonLayer = null;

      isPolygonLocked = false;

      updatePolygonLockUI();

      try {{

        localStorage.removeItem('agridrone_polygon_' + missionId);

      }} catch (e) {{}}

      updatePolygon();

    }}





    async function runSlicing() {{

      const btn = document.getElementById('btn-slice');

      const statusDiv = document.getElementById('slice-status');

      const previewGrid = document.getElementById('tiles-preview');



      if (points.length < 3) {{

        alert("⚠️ Bạn chưa khoanh vùng vườn! Vui lòng click chuột lên ảnh bản đồ để chọn ít nhất 3 điểm bao quanh khu vườn trước khi bấm Cắt Ảnh.");

        return;

      }}



      isPolygonLocked = true;

      updatePolygonLockUI();



      const polygonJson = JSON.parse(document.getElementById('geojson-output').value);



      btn.disabled = true;

      btn.innerText = '⏳ Đang cắt ảnh Dual-Scale (640 + 1280)...';

      statusDiv.innerText = 'Đang chạy Windowed Reading cả 2 tỉ lệ (640px & 1280px)...';

      previewGrid.innerHTML = '';



      try {{

        const resp = await fetch('/api/v1/tiling/slice', {{

          method: 'POST',

          headers: {{ 'Content-Type': 'application/json' }},

          body: JSON.stringify({{

            mission_id: missionId,

            tenant_id: tenantId,

            tif_storage_key: tifKey,

            dual_scale: true,

            polygon_geojson: polygonJson,

            upload_to_minio: false

          }})

        }});



        if (!resp.ok) {{

          const err = await resp.json();

          throw new Error(err.detail || 'Lỗi khi cắt ảnh');

        }}



        const data = await resp.json();

        statusDiv.innerHTML = `<span style="color: #10b981; font-weight: 700;">✅ Hoàn thành: Đã sinh ${{data.total_tiles}} mảnh!<br><small style="color: #94a3b8;">(Gồm ${{data.total_tiles_640}} ảnh 640x640 và ${{data.total_tiles_1280}} ảnh 1280x1280)</small></span>`;

        

        let gridHtml = '';

        data.tiles.slice(0, 9).forEach(tile => {{

          if (tile.url) {{

            const badge = tile.tile_size ? `${{tile.tile_size}}px` : '';

            gridHtml += `<a href="${{tile.url}}" target="_blank" title="Tile #${{tile.tile_index}} (${{badge}})"><img class="tile-thumb" src="${{tile.url}}"></a>`;

          }}

        }});

        previewGrid.innerHTML = gridHtml;



      }} catch (err) {{

        statusDiv.innerHTML = `<span style="color: #ef4444;">❌ Lỗi: ${{err.message}}</span>`;

      }} finally {{

        btn.disabled = false;

        btn.innerText = '✂️ Bắt đầu Cắt Mảnh Bản Đồ (Dual-Scale)';

      }}

    }}



    async function runRealYoloInference() {{

      if (points.length < 3) {{

        alert("⚠️ Bạn chưa khoanh vùng vườn! Vui lòng click chuột lên ảnh bản đồ để chọn ít nhất 3 điểm bao quanh khu vườn trước khi chạy YOLO.");

        return;

      }}



      const btn = document.getElementById('btn-run-real-yolo');

      btn.disabled = true;

      btn.innerText = '⏳ Đang quét YOLO trên GPU...';



      try {{

        const polygonJson = JSON.parse(document.getElementById('geojson-output').value);



        const rowSpacing = parseFloat(document.getElementById('input-row-spacing').value) || 2.8;

        const colSpacing = parseFloat(document.getElementById('input-col-spacing').value) || 2.5;

        const confThreshold = parseFloat(document.getElementById('input-conf').value) || 0.25;



        const reqBody = {{

          mission_id: missionId,

          tenant_id: tenantId,

          confidence_threshold: confThreshold,

          row_spacing: rowSpacing,

          col_spacing: colSpacing,

          polygon_geojson: polygonJson

        }};



        if (anchorTreeLat !== null && anchorTreeLon !== null) {{

          reqBody.anchor_lat = anchorTreeLat;

          reqBody.anchor_lon = anchorTreeLon;

        }}



        const resp = await fetch('/api/v1/yolo/detect-mission', {{

          method: 'POST',

          headers: {{ 'Content-Type': 'application/json' }},

          body: JSON.stringify(reqBody)

        }});



        if (!resp.ok) {{

          const err = await resp.json();

          throw new Error(err.detail || 'Lỗi khi chạy YOLO');

        }}



        const data = await resp.json();

        renderTreeResults(data);



        let alertMsg = `YOLO hoàn tất!\n- Đã quét: ${{data.total_tiles_scanned}} ảnh tiles\n- Tìm thấy: ${{data.total_raw_detections}} đối tượng\n- Đã gôm và lọc trùng: ${{data.total_unique_trees}} cây\n- Vị trí nghi ngờ bỏ sót: ${{data.total_missing_trees}} cây\n- Kích thước lưới: ${{data.max_row}} hàng x ${{data.max_col}} cột`;

        if (data.suggested_row_spacing && data.suggested_col_spacing) {{

          alertMsg += `\n\n💡 AI cảm nhận khoảng cách thực tế:\n  • Hàng (chiều ngắn): ${{data.suggested_row_spacing}}m\n  • Cột (chiều dài): ${{data.suggested_col_spacing}}m\n(Bạn có thể bấm 'Áp dụng thông số này & Chạy lại ngay' ở bảng điều khiển)`;

        }}

        alert(alertMsg);

      }} catch (err) {{

        alert('Lỗi: ' + err.message);

      }} finally {{

        btn.disabled = false;

        btn.innerText = '🚀 Chạy YOLO Nhận Diện Thật (best.pt)';

      }}

    }}



    function renderTreeResults(data) {{

      isPolygonLocked = true;

      updatePolygonLockUI();



      activeTreesList = data.active_trees || [];

      missingTreesList = data.missing_trees || [];



      matchDetectionsToTrees();

      refreshAllTreeMarkers();





      document.getElementById('tree-stats').style.display = 'flex';

      document.getElementById('active-trees-count').innerText = data.total_unique_trees || activeTreesList.length;

      document.getElementById('missing-trees-count').innerText = data.total_missing_trees || missingTreesList.length;

      document.getElementById('grid-dimensions-val').innerText = `${{data.max_row || 0}} hàng x ${{data.max_col || 0}} cột`;



      if (data.suggested_row_spacing && data.suggested_col_spacing) {{

        sensedRowSpacing = data.suggested_row_spacing;

        sensedColSpacing = data.suggested_col_spacing;

        document.getElementById('sensed-row-val').innerText = `${{sensedRowSpacing}}m`;

        document.getElementById('sensed-col-val').innerText = `${{sensedColSpacing}}m`;

        document.getElementById('ai-spacing-box').style.display = 'block';

      }}



      const dlBtn = document.getElementById('btn-download-csv');

      if (dlBtn) dlBtn.style.display = 'block';

      const renumBtn = document.getElementById('btn-renumber-grid');

      if (renumBtn) renumBtn.style.display = 'block';

    }}



    async function loadSavedTrees() {{

      try {{

        const resp = await fetch('/api/v1/yolo/trees/' + missionId);

        if (!resp.ok) {{

          if (missionId !== '9978d458-ffb1-43e3-936f-1776ba4a239c') {{

            const fbResp = await fetch('/api/v1/yolo/trees/9978d458-ffb1-43e3-936f-1776ba4a239c');

            if (fbResp.ok) {{

              const fbData = await fbResp.json();

              if (fbData.has_trees && fbData.active_trees && fbData.active_trees.length > 0) {{

                renderTreeResults(fbData);

                loadDiseaseDetections();

                return;

              }}

            }}

          }}

          return;

        }}

        const data = await resp.json();

        if (data.has_trees && data.active_trees && data.active_trees.length > 0) {{

          renderTreeResults(data);

          loadDiseaseDetections();

          return;

        }}

      }} catch (e) {{}}

    }}



    // Tự động khôi phục các điểm viền vườn đã chọn nếu người dùng F5 tải lại trang

    function restoreSavedPolygon() {{

      try {{

        const saved = localStorage.getItem('agridrone_polygon_' + missionId);

        if (saved) {{

          const parsed = JSON.parse(saved);

          if (Array.isArray(parsed) && parsed.length > 0) {{

            points = parsed;

            markers.forEach(m => map.removeLayer(m));

            markers = [];

            points.forEach((pt, i) => {{

              const marker = L.circleMarker([pt[1], pt[0]], {{

                radius: 5,

                color: '#10b981',

                fillColor: '#34d399',

                fillOpacity: 1

              }}).addTo(map);

              marker.bindPopup(`<b>Điểm #${{i+1}}</b><br>Lat: ${{pt[1]}}<br>Lon: ${{pt[0]}}`);

              markers.push(marker);

            }});

            updatePolygon();

            if (points.length >= 3) {{

              isPolygonLocked = true;

              updatePolygonLockUI();

            }}

          }}

        }}

      }} catch (e) {{}}

    }}

    restoreSavedPolygon();

    async function loadFarmHealthMap() {{

      const badge = document.getElementById('badge-health-map-status');

      if (badge) {{

        badge.innerText = 'Đang nạp từ .NET API...';

        badge.style.background = '#38bdf8';

      }}



      try {{

        // 1. Ưu tiên gọi trực tiếp Backend .NET API (chuẩn kiến trúc Clean Architecture)

        let data = null;

        try {{

          const netResp = await fetch('http://localhost:5080/api/farms/b9d9d8a3-94a5-4390-a007-9017a2a131fa/plant-health-map?tenantId=a83f601b-3d5b-4e14-b735-28b46b213438');

          if (netResp.ok) {{

            data = await netResp.json();

            console.log('✅ Đã nạp thành công từ .NET Backend API (port 5080):', data);

          }}

        }} catch (netErr) {{

          console.warn('.NET API port 5080 chưa sẵn sàng, dùng fallback AI endpoints:', netErr);

        }}



        if (data && data.plants && data.plants.length > 0) {{

          // Áp dụng dữ liệu từ .NET Backend API
          if (data.baseMap && data.baseMap.previewUrl) {{
            let mapBounds = bounds;
            if (data.baseMap.bounds) {{
              const b = data.baseMap.bounds;
              if (data.baseMap.sourceMissionId && data.baseMap.sourceMissionId === missionId) {{
                mapBounds = bounds;
              }} else {{
                mapBounds = [[b.minLat, b.minLon], [b.maxLat, b.maxLon]];
              }}
            }}
            if (imageOverlay) {{
              imageOverlay.setUrl(data.baseMap.previewUrl);
              imageOverlay.setBounds(mapBounds);
            }} else {{
              imageOverlay = L.imageOverlay(data.baseMap.previewUrl, mapBounds, {{ opacity: 0.95 }}).addTo(map);
            }}
            map.fitBounds(mapBounds);
          }}

          // Tự động nạp Boundary Polygon đã duyệt từ .NET API (nếu có)
          if (data.boundaryGeoJson) {{
            try {{
              const geo = typeof data.boundaryGeoJson === 'string' ? JSON.parse(data.boundaryGeoJson) : data.boundaryGeoJson;
              if (geo && geo.coordinates && geo.coordinates[0]) {{
                const coords = geo.coordinates[0];
                const cleanCoords = (coords.length > 3 && coords[0][0] === coords[coords.length-1][0] && coords[0][1] === coords[coords.length-1][1])
                  ? coords.slice(0, -1)
                  : coords;
                const bPoints = cleanCoords.map(c => ({{ lon: c[0], lat: c[1] }}));
                loadBoundaryFromCsvPoints(bPoints);
              }}
            }} catch (errGeo) {{
              console.warn('Lỗi phân tích boundaryGeoJson:', errGeo);
            }}
          }}



          if (data.summary) {{

            const elTot = document.getElementById('stat-total-plants');

            const elNorm = document.getElementById('stat-normal-trees');

            const elHeal = document.getElementById('stat-healthy-trees');

            const elDis = document.getElementById('stat-diseased-trees');

            const elDisBadge = document.getElementById('badge-disease-count');

            const elBrk = document.getElementById('disease-breakdown');



            if (elTot) elTot.innerText = data.summary.totalPlants || 0;

            if (elNorm) elNorm.innerText = data.summary.normalPlants || 0;

            if (elHeal) elHeal.innerText = data.summary.healthyFruitPlants || 0;

            if (elDis) elDis.innerText = data.summary.diseasePlants || 0;

            if (elDisBadge) elDisBadge.innerText = `${{data.summary.diseasePlants || 0}} Cây Bệnh`;



            if (elBrk && data.summary.diseaseBreakdown) {{

              let bHtml = '';

              for (const [cls, cnt] of Object.entries(data.summary.diseaseBreakdown)) {{

                const vi = CLASS_TRANSLATIONS[cls] || cls;

                const isH = cls.toLowerCase().startsWith('healthy');

                const color = isH ? '#06b6d4' : '#f59e0b';

                bHtml += `<div style="display: flex; justify-content: space-between;">

                  <span style="color: ${{color}};">• ${{vi}}:</span>

                  <b style="color: #fff;">${{cnt}} vết</b>

                </div>`;

              }}

              elBrk.innerHTML = bHtml;

            }}

          }}



          const normalizedPlants = data.plants.map(p => ({{

            id: p.id,

            plant_code: p.plantCode,

            row: p.row,

            col: p.col,

            lat: p.lat,

            lon: p.lon,

            health_status: p.healthStatus,

            observations: (p.observations || []).map(o => ({{

              class_name: o.className,

              class_name_vi: o.classNameVi || CLASS_TRANSLATIONS[o.className] || o.className,

              confidence: o.confidence,

              gps_distance_m: o.gpsDistanceM,

              image_name: o.imageName,

              crop_image_url: o.cropImageUrl,

              annotated_image_url: o.annotatedImageUrl,

              detection_id: o.detectionId

            }}))

          }}));



          renderFarmPlants(normalizedPlants);
          if (data.zones) {{
            renderDiseaseZones(data.zones);
          }}



          if (badge) {{

            badge.innerText = `Hoàn tất (.NET API: ${{normalizedPlants.length}} cây)`;

            badge.style.background = '#10b981';

          }}

          return;

        }}



        // 2. Fallback nếu gọi .NET không có: dùng dữ liệu AI nội bộ

        const baseInput = document.getElementById('input-base-mission');

        const diseaseInput = document.getElementById('input-disease-mission');

        const baseMission = (baseInput ? baseInput.value.trim() : '') || '9978d458-ffb1-43e3-936f-1776ba4a239c';

        const diseaseMission = (diseaseInput ? diseaseInput.value.trim() : '') || '17bfaee6-2e55-4c94-ae35-225df0d99d4d';



        const [treesResp, diseaseResp] = await Promise.all([

          fetch('/api/v1/yolo/trees/' + encodeURIComponent(baseMission)),

          fetch('/api/v1/disease/detections/' + encodeURIComponent(diseaseMission))

        ]);



        if (!treesResp.ok) throw new Error('Không thể tải cây');

        const treesData = await treesResp.json();

        const activeTrees = treesData.active_trees || [];

        let diseaseList = [];

        if (diseaseResp.ok) {{

          const dData = await diseaseResp.json();

          diseaseList = dData.detections || [];

        }}



        const toleranceMeters = 2.0;

        let normalCount = 0;

        let healthyFruitCount = 0;

        let diseaseCount = 0;

        const diseaseSummary = {{}};



        const combinedPlants = activeTrees.map(t => {{

          const matched = [];

          diseaseList.forEach(d => {{

            const dist = getHaversineDistanceMeters(t.lat, t.lon, d.lat, d.lon);

            if (dist <= toleranceMeters) {{

              matched.push({{

                ...d,

                gps_distance_m: dist,

                class_name_vi: CLASS_TRANSLATIONS[d.class_name] || d.class_name

              }});

            }}

          }});



          matched.forEach(d => {{

            const c = d.class_name || 'Unknown';

            diseaseSummary[c] = (diseaseSummary[c] || 0) + 1;

          }});



          const hasDisease = matched.some(d => !d.class_name.toLowerCase().startsWith('healthy'));

          const hasHealthy = matched.some(d => d.class_name.toLowerCase().startsWith('healthy'));



          let health_status = 'NORMAL';

          if (hasDisease) {{

            health_status = 'DISEASE';

            diseaseCount++;

          }} else if (hasHealthy) {{

            health_status = 'HEALTHY_FRUIT';

            healthyFruitCount++;

          }} else {{

            health_status = 'NORMAL';

            normalCount++;

          }}



          return {{

            plant_code: t.plant_code || `Trụ H${{t.row}}-C${{t.col}}`,

            row: t.row,

            col: t.col,

            lat: t.lat,

            lon: t.lon,

            health_status: health_status,

            observations: matched

          }};

        }});



        renderFarmPlants(combinedPlants);



        if (badge) {{

          badge.innerText = `Đã nạp (${{combinedPlants.length}} cây)`;

          badge.style.background = '#10b981';

        }}



      }} catch (err) {{

        console.error('Lỗi nạp dữ liệu:', err);

        if (badge) {{

          badge.innerText = 'Lỗi nạp';

          badge.style.background = '#ef4444';

        }}

      }}

    }}

    window.loadFarmHealthMap = loadFarmHealthMap;



    
    let zoneLayers = [];

    let currentDiseaseZones = [];

    function renderDiseaseZones(zones) {{
      zoneLayers.forEach(l => map.removeLayer(l));
      zoneLayers = [];
      currentDiseaseZones = zones || [];

      const elContainer = document.getElementById('zone-alerts-container');
      const elBadgeCount = document.getElementById('badge-zone-count');

      if (!zones || zones.length === 0) {{
        if (elContainer) elContainer.innerHTML = '<div style="color: var(--text-muted); font-size: 11px;">Chưa phát hiện ổ dịch lây lan nào.</div>';
        if (elBadgeCount) elBadgeCount.innerText = '0 Ổ Dịch';
        return;
      }}

      if (elBadgeCount) elBadgeCount.innerText = `${{zones.length}} Ổ Dịch`;
      let alertsHtml = '';

      zones.forEach((z, idx) => {{
        const isSameDisease = z.alertType === 'SAME_DISEASE_EPIDEMIC';
        // TH1: Cam Đỏ (#f97316), TH2: Vàng Chanh (#facc15)
        const color = isSameDisease ? '#facc15' : '#f97316';
        const badgeLabel = isSameDisease ? 'TH2 • CÙNG BỆNH' : 'TH1 • MẬT ĐỘ CAO';
        const badgeBg = isSameDisease ? 'rgba(250, 204, 21, 0.2)' : 'rgba(249, 115, 22, 0.2)';
        const title = isSameDisease 
          ? `🟡 Ổ Dịch Lây Lan: ${{z.diseaseNameVi || z.diseaseName || 'Cháy Nắng'}}`
          : '🟠 Ổ Dịch Mật Độ Cao';

        // Tạo độ chênh bán kính: TH1 (vòng ngoài +0.6m) lồng TH2 (vòng trong giữ nguyên)
        // để khi 2 vòng trùng vị trí vẫn nhìn thấy rõ 2 lớp màu khác nhau!
        const displayRadius = isSameDisease ? z.radiusMeters : (z.radiusMeters + 0.6);

        let layer = null;
        if (z.centerLat && z.centerLon && z.radiusMeters) {{
          layer = L.circle([z.centerLat, z.centerLon], {{
            radius: displayRadius,
            color: color,
            weight: isSameDisease ? 2.5 : 3.0,
            opacity: 0.95,
            dashArray: isSameDisease ? '4, 4' : '10, 6',
            fillColor: color,
            fillOpacity: isSameDisease ? 0.22 : 0.12
          }}).addTo(map);
        }}

        if (layer) {{
          layer._zoneType = z.alertType;
          layer._zoneIndex = idx;

          let breakdownList = '';
          let totalLesions = 0;
          if (z.diseaseBreakdown) {{
            for (const [k, v] of Object.entries(z.diseaseBreakdown)) {{
              breakdownList += `<li>${{CLASS_TRANSLATIONS[k] || k}}: <b>${{v}} vết</b></li>`;
              totalLesions += v;
            }}
          }}

          layer.bindPopup(`
            <div style="font-family: sans-serif; font-size: 12px; min-width: 230px;">
              <div style="font-weight: 700; color: ${{color}}; border-bottom: 1px solid #334155; padding-bottom: 5px; display: flex; justify-content: space-between; align-items: center;">
                <span>${{title}}</span>
                <span style="font-size: 10px; background: ${{badgeBg}}; color: ${{color}}; padding: 2px 5px; border-radius: 4px; font-weight: 700;">Zone #${{idx + 1}}</span>
              </div>
              <div style="margin-top: 7px;">🌳 Quy mô: <b style="color: #38bdf8;">${{z.totalTrees}} trụ cây</b></div>
              <div>📐 Bán kính vùng bệnh: <b>${{z.radiusMeters}}m</b> <span style="font-size: 10px; color: #94a3b8;">(hiển thị: ${{displayRadius}}m)</span></div>
              <div style="margin-top: 4px; font-size: 11px; color: #94a3b8;">
                <b>Phân bố vết bệnh (${{totalLesions}} vết):</b>
                <ul style="margin: 2px 0 0 16px; padding: 0;">${{breakdownList}}</ul>
              </div>
            </div>
          `);
          zoneLayers.push(layer);

          // Gắn thêm Marker nhãn nhỏ nổi bật ngay tâm (hoặc mép) để nhìn thấy ngay có cả 2 Zone
          const tagIcon = L.divIcon({{
            className: 'zone-tag-marker',
            html: `<div style="background: ${{color}}; color: ${{isSameDisease ? '#000' : '#fff'}}; font-size: 9px; font-weight: 800; padding: 1px 5px; border-radius: 999px; box-shadow: 0 1px 4px rgba(0,0,0,0.8); border: 1.5px solid #1e293b; white-space: nowrap; cursor: pointer;" onclick="focusOnZone(${{idx}})">${{isSameDisease ? '🟡 TH2' : '🟠 TH1'}} #${{idx + 1}}</div>`,
            iconSize: [50, 18],
            iconAnchor: [isSameDisease ? 48 : -2, isSameDisease ? 8 : -8]
          }});
          const tagMarker = L.marker([z.centerLat, z.centerLon], {{ icon: tagIcon }}).addTo(map);
          tagMarker._zoneType = z.alertType;
          zoneLayers.push(tagMarker);
        }}

        let breakdownBadges = '';
        let lesionsCount = 0;
        if (z.diseaseBreakdown) {{
          for (const [k, v] of Object.entries(z.diseaseBreakdown)) {{
            breakdownBadges += `<span style="background: rgba(255,255,255,0.06); padding: 2px 6px; border-radius: 4px; margin-right: 4px; color: #cbd5e1;">${{CLASS_TRANSLATIONS[k] || k}}: <b style="color: ${{color}};">${{v}} vết</b></span>`;
            lesionsCount += v;
          }}
        }}

        alertsHtml += `
          <div id="zone-card-${{idx}}" style="background: rgba(15, 23, 42, 0.7); border: 1.5px solid ${{color}}55; border-left: 5px solid ${{color}}; padding: 9px 11px; border-radius: 6px; cursor: pointer; transition: all 0.2s;" 
               onclick="focusOnZone(${{idx}})" 
               onmouseover="this.style.background='rgba(30, 41, 59, 0.95)'; this.style.borderColor='${{color}}'" 
               onmouseout="this.style.background='rgba(15, 23, 42, 0.7)'; this.style.borderColor='${{color}}55'">
            <div style="display: flex; justify-content: space-between; align-items: center;">
              <div style="font-size: 12px; font-weight: 700; color: ${{color}};">${{title}}</div>
              <span style="font-size: 9px; font-weight: 800; background: ${{badgeBg}}; color: ${{color}}; padding: 2px 6px; border-radius: 3px; border: 1px solid ${{color}}44;">${{badgeLabel}}</span>
            </div>
            <div style="font-size: 11px; color: #f8fafc; margin-top: 5px; display: flex; justify-content: space-between;">
              <span>🌳 Quy mô: <b style="color: #38bdf8;">${{z.totalTrees}} cây</b> <span style="font-size: 10px; color: #94a3b8;">(${{lesionsCount}} vết)</span></span>
              <span>📐 Bán kính: <b style="color: ${{color}};">${{z.radiusMeters}}m</b></span>
            </div>
            <div style="margin-top: 5px; font-size: 10px; color: #94a3b8; display: flex; flex-wrap: wrap; gap: 4px;">
              ${{breakdownBadges}}
            </div>
            <div style="font-size: 10px; color: #38bdf8; margin-top: 5px; display: flex; align-items: center; gap: 3px;">
              <span>📍 Nhấp để zoom & highlight trên bản đồ</span>
            </div>
          </div>
        `;
      }});

      if (elContainer) elContainer.innerHTML = alertsHtml;
    }}

    function toggleZoneVisibility() {{
      const showTh1 = document.getElementById('chk-show-th1')?.checked ?? true;
      const showTh2 = document.getElementById('chk-show-th2')?.checked ?? true;

      zoneLayers.forEach((layer) => {{
        const isTh1 = layer._zoneType === 'HIGH_DENSITY_OUTBREAK';
        const isTh2 = layer._zoneType === 'SAME_DISEASE_EPIDEMIC';
        const shouldShow = (isTh1 && showTh1) || (isTh2 && showTh2);
        if (shouldShow) {{
          if (!map.hasLayer(layer)) map.addLayer(layer);
        }} else {{
          if (map.hasLayer(layer)) map.removeLayer(layer);
        }}
      }});
    }}
    window.toggleZoneVisibility = toggleZoneVisibility;

    function focusOnZone(idx) {{
      const z = currentDiseaseZones[idx];
      if (!z || !z.centerLat || !z.centerLon) return;
      map.flyTo([z.centerLat, z.centerLon], 20, {{ duration: 0.8 }});

      // Tìm circle layer tương ứng của zone này
      const targetLayer = zoneLayers.find(l => l._zoneIndex === idx && l.bindPopup);
      if (targetLayer) {{
        if (!map.hasLayer(targetLayer)) map.addLayer(targetLayer);
        targetLayer.openPopup();
        if (targetLayer.setStyle) {{
          const isSame = z.alertType === 'SAME_DISEASE_EPIDEMIC';
          targetLayer.setStyle({{ weight: 6, fillOpacity: 0.5 }});
          setTimeout(() => {{
            targetLayer.setStyle({{ weight: isSame ? 2.5 : 3.0, fillOpacity: isSame ? 0.22 : 0.12 }});
          }}, 2500);
        }}
      }}
    }}
    window.focusOnZone = focusOnZone;

    function renderFarmPlants(plants) {{

      healthMarkers.forEach(m => map.removeLayer(m));

      healthMarkers = [];



      plants.forEach(t => {{

        let markerColor = '#10b981';

        let markerFill = '#34d399';

        let markerRadius = 7;

        let markerWeight = 2;

        let badgeHtml = '🟢 Trụ Bình Thường';

        let badgeColor = '#10b981';

        let tooltipText = `H${{t.row}}-C${{t.col}}`;



        if (t.health_status === 'DISEASE') {{

          // 🔴 Cây Nhiễm Bệnh: Viền đỏ đậm, ruột vàng cam cảnh báo

          markerColor = '#dc2626';

          markerFill = '#f59e0b';

          markerRadius = 9;

          markerWeight = 3;

          const disNames = (t.observations || [])

            .filter(d => !d.class_name.toLowerCase().startsWith('healthy'))

            .map(d => d.class_name_vi || d.class_name)

            .filter((v, i, a) => a.indexOf(v) === i)

            .join(', ');

          badgeHtml = `⚠️ Cây Bệnh: ${{disNames || 'Nhiễm bệnh'}}`;

          badgeColor = '#ef4444';

          tooltipText = `🔴 H${{t.row}}-C${{t.col}}: ${{disNames || 'Nhiễm bệnh'}}`;



        }} else if (t.health_status === 'HEALTHY_FRUIT') {{

          // 🍏 Cây Có Trái Khỏe: Xanh ngọc / Cyan sáng bắt mắt (khác hẳn xanh thường)

          markerColor = '#0284c7';

          markerFill = '#06b6d4';

          markerRadius = 8;

          markerWeight = 2.5;

          badgeHtml = '🍏 Trụ Có Trái Khỏe';

          badgeColor = '#06b6d4';

          tooltipText = `🍏 H${{t.row}}-C${{t.col}}: Trái khỏe (${{(t.observations || []).length}})`;



        }} else {{

          // 🟢 Cây Bình Thường (chưa ghi nhận bệnh/trái)

          markerColor = '#10b981';

          markerFill = '#34d399';

          markerRadius = 7;

          markerWeight = 2;

          badgeHtml = '🟢 Trụ Bình Thường';

          badgeColor = '#10b981';

          tooltipText = `🟢 H${{t.row}}-C${{t.col}}`;

        }}



        const m = L.circleMarker([t.lat, t.lon], {{

          radius: markerRadius,

          color: markerColor,

          fillColor: markerFill,

          fillOpacity: 0.9,

          weight: markerWeight,

          interactive: true,

          bubblingMouseEvents: false

        }}).addTo(map);



        m.bindTooltip(tooltipText, {{ direction: 'top' }});



        // Popup chi tiết khi click vào trụ

        let obsHtml = '';

        if (t.observations && t.observations.length > 0) {{

          obsHtml += `

            <div style="margin-top: 8px; border-top: 1px solid rgba(255,255,255,0.15); padding-top: 6px;">

              <div style="font-weight: 700; color: #fbbf24; font-size: 11px; margin-bottom: 4px;">

                🔍 Vết nhận diện (${{t.observations.length}} đối tượng):

              </div>

              <div style="max-height: 220px; overflow-y: auto; display: flex; flex-direction: column; gap: 6px;">

          `;

          t.observations.forEach(d => {{

            const isH = d.class_name.toLowerCase().startsWith('healthy');

            const tagColor = isH ? '#06b6d4' : '#ef4444';

            const viName = d.class_name_vi || d.class_name;

            const confPct = ((d.confidence || 0) * 100).toFixed(1);

            const distM = d.gps_distance_m !== undefined ? `${{Number(d.gps_distance_m).toFixed(2)}}m` : '0.0m';



            const cropThumb = d.crop_image_url

              ? `<img src="${{d.crop_image_url}}" style="width: 100%; height: 80px; object-fit: cover; border-radius: 4px; border: 1px solid rgba(255,255,255,0.2); cursor: pointer;" onclick="openImageModal('${{d.crop_image_url}}', '${{viName}}', 'Trụ: ${{t.plant_code}} | H${{t.row}}-C${{t.col}} | Lệch GPS: ${{distM}} | Tin cậy: ${{confPct}}%')" title="Bấm để xem ảnh phóng to">`

              : '';



            const annotatedLink = d.annotated_image_url

              ? `<a href="${{d.annotated_image_url}}" target="_blank" style="color: #38bdf8; text-decoration: underline; font-size: 10px; display: inline-block; margin-top: 2px;">🖼️ Xem ảnh gốc khoanh viền</a>`

              : '';



            obsHtml += `

              <div style="background: rgba(0,0,0,0.4); padding: 6px; border-radius: 4px; border-left: 3px solid ${{tagColor}}; font-size: 11px;">

                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 2px;">

                  <span style="font-weight: 700; color: ${{tagColor}};">${{viName}}</span>

                  <span style="background: ${{tagColor}}; color: #000; font-size: 9px; font-weight: 700; padding: 1px 5px; border-radius: 3px;">${{confPct}}%</span>

                </div>

                <div style="font-size: 10px; color: #94a3b8; display: flex; justify-content: space-between;">

                  <span>Drone: <code>${{d.image_name || 'N/A'}}</code></span>

                  <span>Lệch tâm: <b>${{distM}}</b></span>

                </div>

                ${{cropThumb}}

                ${{annotatedLink}}

              </div>

            `;

          }});

          obsHtml += '</div></div>';

        }}



        const popupContent = `

          <div style="min-width: 220px; font-family: -apple-system, BlinkMacSystemFont, sans-serif; color: #f8fafc;">

            <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 4px;">

              <b style="font-size: 13px; color: #fff;">${{t.plant_code}}</b>

              <span style="background: ${{badgeColor}}; color: #fff; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px;">

                H${{t.row}} - C${{t.col}}

              </span>

            </div>

            <div style="font-size: 11px; margin-bottom: 4px;">

              <span style="padding: 2px 6px; border-radius: 4px; background: rgba(255,255,255,0.08);">${{badgeHtml}}</span>

            </div>

            <div style="font-size: 10px; color: #94a3b8; margin-bottom: 4px;">

              📍 Lat: ${{t.lat.toFixed(6)}}, Lon: ${{t.lon.toFixed(6)}}

            </div>

            ${{obsHtml}}

          </div>

        `;



        m.bindPopup(popupContent, {{ maxWidth: 320 }});

        healthMarkers.push(m);

      }});

    }}



    // Auto-load saved trees for Tab 1 (YOLO) on start

    loadSavedTrees();

  </script>

  <!-- High-Res Image Detail Modal -->

  <div id="image-modal" style="display: none; position: fixed; inset: 0; background: rgba(0,0,0,0.85); z-index: 99999; justify-content: center; align-items: center; padding: 20px;" onclick="closeImageModal()">

    <div style="background: #1e293b; border: 1px solid #334155; border-radius: 8px; max-width: 800px; width: 95%; max-height: 90vh; overflow-y: auto; padding: 18px; position: relative;" onclick="event.stopPropagation()">

      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; border-bottom: 1px solid #334155; padding-bottom: 8px;">

        <h3 id="modal-title" style="color: #fff; font-size: 15px; font-weight: 700;">Chi tiết phát hiện</h3>

        <button style="background: #ef4444; color: #fff; border: none; border-radius: 4px; padding: 4px 10px; font-size: 12px; font-weight: 700; cursor: pointer;" onclick="closeImageModal()">✕ Đóng</button>

      </div>

      <div style="text-align: center; background: #0b0f19; border-radius: 6px; padding: 10px; border: 1px solid #334155;">

        <img id="modal-img" src="" style="max-width: 100%; max-height: 55vh; object-fit: contain; border-radius: 4px;">

      </div>

      <div id="modal-info" style="margin-top: 12px; font-size: 12px; color: #cbd5e1; line-height: 1.6;"></div>

    </div>

  </div>

</body>

</html>

"""

