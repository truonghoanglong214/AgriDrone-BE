# Chuyển tiling-slice sang job nền

1. Thêm migration cho hàng đợi tiling trong schema `mission`. Ghi input, trạng thái và output. Dùng unique index giới hạn một job active trên mỗi mission và khóa hàng Mission khi nhận yêu cầu.
2. Sửa command và API tiling để commit job rồi trả 202. Thêm GET theo ID và GET job mới nhất trong phạm vi tenant, farm, mission. Worker dùng advisory lock PostgreSQL để nhận hoặc phục hồi job, và token lần chạy để công bố đúng kết quả.
3. Sửa client Metashape và FastAPI để chạy trong temp riêng. Dùng prefix MinIO theo job và lần chạy, upload tiles trước và manifest sau. Lưu vùng cắt cùng manifest.
4. Sửa YOLO để chọn job hoàn tất và tải manifest cùng tiles tương ứng. Giữ đường đọc tiles tạm cho mission đã cắt theo luồng cũ trước khi triển khai.
5. Build solution, kiểm tra Python và bàn giao API cho FE. Nghiệm thu qua Amplify với GeoTIFF tải trên 60 giây, retry đồng thời, reload, worker restart và YOLO sử dụng đúng job.

## Hợp đồng FE

Các route dưới đây dùng tiền tố `/api/missions/{missionId}/farms/{farmId}` và quyền System Manager hiện hành.

| API | Cách dùng |
| --- | --- |
| `POST /tiling-slice` | Gửi payload cắt hiện tại. Response `202 Accepted` có `jobId`, `status` và header `Location`. Gửi lại cùng input khi job còn active sẽ nhận lại cùng `jobId`. Input khác nhận `409` trong thời gian đó. |
| `GET /tiling-slice/jobs/{jobId}` | Poll khoảng 3 giây. Job ở một trong các trạng thái `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED`. |
| `GET /tiling-slice/jobs/latest` | Gọi khi reload màn hình mission để tìm lại job mới nhất. `404` nghĩa là mission chưa có job. |
| `POST /yolo-detect` | Gửi `tilingJobId` lấy từ job `COMPLETED`, cùng các tùy chọn YOLO. API từ chối job thuộc mission khác hoặc chưa hoàn tất. |

Khi `COMPLETED`, đọc `polygonGeoJson` và `result` với số tile, danh sách tile và khóa manifest. `result.tiles[].url` là đường dẫn BE có xác thực và chuyển hướng tới URL MinIO có thời hạn. Khi `FAILED`, đọc `error` và cho Manager gửi lại POST. Tham số `uploadToMinio` cũ vẫn được nhận, mọi job mới đều ghi MinIO.

## Triển khai và nghiệm thu

Áp dụng migration `20261127100000_MissionTilingJobs` và triển khai cả .NET API lẫn Metashape service. FE cần đọc response 202, poll GET và chuyển `tilingJobId` vào YOLO. Timeout của HTTP proxy phía FE không còn nằm trên đường tải hoặc cắt GeoTIFF. YOLO giữ cơ chế gọi hiện hành.

Nghiệm thu qua Amplify với GeoTIFF 539 MB đã ghi nhận và kiểm tra POST trả 202 trước khi tải xong. Reload hoặc đóng tab khi job `RUNNING`, mở lại và kiểm tra job ID cùng vùng cắt. Thử hai POST cùng input và một POST khác vùng trong khi job đang chạy. Khi hoàn tất, chạy YOLO và đối chiếu số tiles với manifest của đúng job đã chọn. Thử restart worker giữa lúc cắt để xác minh phục hồi job và cô lập output lần chạy trước.

## YOLO chạy nền

- `POST /yolo-detect` nhận body hiện tại cùng `tilingJobId` đã hoàn tất và trả `202 Accepted` với `jobId`, `tilingJobId`, `status` và `Location`. Khi bỏ qua `tilingJobId`, BE dùng job cutting mới nhất nếu job đó đã hoàn tất. Các mission chỉ còn tiles tạm từ luồng cũ cần chạy tiling một lần để tạo manifest bền vững.
- `GET /yolo-detect/jobs/{jobId}` để poll. `GET /yolo-detect/jobs/latest` để tìm lại job sau khi reload. Khi `COMPLETED`, đọc `result.activeTrees`, `result.missingTrees` và các số liệu YOLO. Khi `FAILED`, hiển thị `error` và cho phép gửi lại POST. Từng mission chỉ có một job YOLO active. Gửi lại cùng input sẽ nhận cùng job và input khác nhận `409`.
- Áp dụng migration `20261127120000_MissionYoloJobs` trước khi chạy API mới. FastAPI cần phiên bản hỗ trợ `tile_manifest_storage_key` và endpoint riêng hiện tại phải truy cập được từ container .NET API.
- Nghiệm thu qua Amplify với một run YOLO kéo dài hơn timeout của proxy. POST trả 202 trước khi suy luận xong, reload tiếp tục theo dõi được cùng job, và kết quả quét đủ tiles trong manifest đã chọn.
