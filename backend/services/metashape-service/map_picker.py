import json
import html
from typing import Optional
from minio_storage import minio_storage

def get_map_picker_html(
    mission_id: str = "8d2ba93d-1421-448d-be83-a1034e8c4aab",
    tenant_id: str = "cf637409-fb85-4dfd-8fd3-ae3ea799d635",
    farm_id: str = "",
    preview_url: Optional[str] = None,
    min_lat: float = 10.4611274,
    max_lat: float = 10.4617541,
    min_lon: float = 106.4936806,
    max_lon: float = 106.4945788,
    tif_key: Optional[str] = None
) -> str:
    # Auto-generate fresh presigned URL if not passed
    if not preview_url:
        jpg_key = f"tenants/{tenant_id}/media/{mission_id}/map_preview.jpg"
        preview_url = minio_storage.get_presigned_url(jpg_key)
    
    if not tif_key:
        tif_key = f"tenants/{tenant_id}/media/{mission_id}/orthophoto.tif"

    center_lat = (min_lat + max_lat) / 2
    center_lon = (min_lon + max_lon) / 2
    escaped_farm_id = html.escape(farm_id, quote=True)

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
          <input id="plant-gps-farm-id" value="{escaped_farm_id}" placeholder="Farm ID" style="width: 100%; margin-top: 6px;" />
          <input id="plant-gps-api-url" value="http://localhost:5080" placeholder=".NET API URL" style="width: 100%; margin-top: 6px;" />
          <input id="plant-gps-token" type="password" placeholder="Manager JWT token" autocomplete="off" style="width: 100%; margin-top: 6px;" />
        </div>
      </div>
    </div>
  </div>

  <script>
    const missionId = "{mission_id}";
    const tenantId = "{tenant_id}";
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
    let activeTreesList = [];
    let missingTreesList = [];
    let sensedRowSpacing = null;
    let sensedColSpacing = null;

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
      m.bindPopup(`<b>🔴 Nghi ngờ bỏ sót / Khuyết cây</b><br>Hàng: ${{tree.row}}, Cột: ${{tree.col}}<br>Lat: ${{tree.lat}}<br>Lon: ${{tree.lon}}<br><button style="margin-top:6px;padding:6px 12px;background:#10b981;border:none;border-radius:4px;color:#000;cursor:pointer;font-weight:700;" onclick="confirmMissingTree(${{tree.lat}}, ${{tree.lon}})">✅ Xác nhận có cây tại đây</button>`);
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

    async function downloadCurrentTreesCsv() {{
      if (!activeTreesList || activeTreesList.length === 0) {{
        alert("Chưa có danh sách cây để tải!");
        return;
      }}

      let csv = "tree_id,row,col,latitude,longitude,confidence,status,source\\n";
      const trees = [];
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

      const farmId = document.getElementById('plant-gps-farm-id').value.trim();
      const apiUrl = document.getElementById('plant-gps-api-url').value.trim().replace(/\\/$/, '');
      const token = document.getElementById('plant-gps-token').value.trim();
      if (!farmId || !apiUrl || !token) {{
        alert('Cần Farm ID, địa chỉ .NET API và Manager JWT token để lưu PostgreSQL.');
        return;
      }}
      const button = document.getElementById('btn-download-csv');
      button.disabled = true;
      try {{
        const response = await fetch(`${{apiUrl}}/api/system-manager/farms/${{encodeURIComponent(farmId)}}/missions/${{encodeURIComponent(missionId)}}/plant-gps`, {{
          method: 'POST',
          headers: {{ 'Content-Type': 'application/json', 'Authorization': `Bearer ${{token}}` }},
          body: JSON.stringify({{ trees }})
        }});
        if (!response.ok) throw new Error(`API trả về HTTP ${{response.status}}`);
      }} catch (error) {{
        alert(`Không thể lưu dữ liệu cây vào PostgreSQL: ${{error.message}}`);
        return;
      }} finally {{
        button.disabled = false;
      }}

      const blob = new Blob([csv], {{ type: 'text/csv;charset=utf-8;' }});
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `trees_${{missionId}}_latest.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
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
      treeMarkers.forEach(m => map.removeLayer(m));
      treeMarkers = [];
      if (anchorMarker) {{
        map.removeLayer(anchorMarker);
        anchorMarker = null;
      }}

      isPolygonLocked = true;
      updatePolygonLockUI();

      activeTreesList = data.active_trees || [];
      missingTreesList = data.missing_trees || [];

      activeTreesList.forEach(t => {{
        const isAnchor = t.is_anchor;
        const m = L.circleMarker([t.lat, t.lon], {{
          radius: isAnchor ? 10 : 7,
          color: isAnchor ? '#f59e0b' : '#10b981',
          fillColor: isAnchor ? '#fbbf24' : '#34d399',
          fillOpacity: isAnchor ? 1.0 : 0.85,
          weight: isAnchor ? 3 : 2,
          interactive: true,
          bubblingMouseEvents: false
        }}).addTo(map);

        m.bindTooltip(isAnchor ? `📍 Neo H${{t.row}}-C${{t.col}}` : `H${{t.row}}-C${{t.col}}`, {{
          direction: 'top'
        }});

        const popupContent = isAnchor
          ? `<b>📍 Cây Neo Mốc (Hàng ${{t.row}} - Cột ${{t.col}})</b><br>Độ tin cậy: ${{(t.confidence*100).toFixed(0)}}%<br>Lat: ${{t.lat}}<br>Lon: ${{t.lon}}`
          : `<b>🟢 Trụ Thanh Long (Hàng ${{t.row}} - Cột ${{t.col}})</b><br>Độ tin cậy: ${{(t.confidence*100).toFixed(0)}}%<br>Lat: ${{t.lat}}<br>Lon: ${{t.lon}}`;

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
    }}

    async function loadSavedTrees() {{
      try {{
        const resp = await fetch('/api/v1/yolo/trees/' + missionId);
        if (!resp.ok) return;
        const data = await resp.json();
        if (data.has_trees && data.active_trees && data.active_trees.length > 0) {{
          renderTreeResults(data);
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
    loadSavedTrees();
  </script>
</body>
</html>
"""
