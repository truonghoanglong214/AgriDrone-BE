# Danh sách test case AgriDrone V2.6

Ma trận để viết test mới, đối chiếu test hiện có, dọn test thừa và nhập vào workbook **Test Cases**. Tổng chốt **90 test case và 10 UAT tính riêng**, khớp đúng `Review 2 Commitment` mục 8.3 (90 ca và 10 demo) và công thức của [Test-Plans.md](Test-Plans.md). Giữ 7 nhóm tính năng, 5 luồng và 7 NFR theo phiếu V2.6 và code V3. Các mã `TC-…` là mã thống nhất dùng trong workbook, Unit Test và Report 5.

| Phần | Test level | Số ca |
|---|---|---:|
| Unit (7 tính năng × 3) | Component | 21 |
| API theo tính năng (7 tính năng × 2) | Integration | 14 |
| System theo use case (7 tính năng × 2) | System | 14 |
| E2E (5 luồng chính) | System | 5 |
| API kiểm chuyển trạng thái (22 hợp lệ + 7 bị chặn) | Integration | 29 |
| NFR (NFR1–NFR7) | System | 7 |
| **Tổng chốt theo Commitment** | **Tự động CI: 64/90 (71,1%)** | **90** |
| **UAT tính riêng** | Acceptance | **10** |

- **Có ứng viên** nghĩa là repo có phương thức để đối chiếu. Chỉ dùng lại khi khớp dữ liệu, kết quả mong đợi và test level. Test domain/handler dùng được làm bằng chứng từng phần cho ca API.
- **Cần bổ sung** nghĩa là cần triển khai ca theo thiết kế dưới đây nếu đối chiếu vẫn thiếu. **Rà API** nghĩa là cần xác định thao tác công khai hoặc sự kiện nghiệp vụ thực sự kích hoạt chuyển trạng thái trước khi giữ ca trong nhóm API.
- Với ca bị từ chối, kiểm cả mã lỗi nghiệp vụ và dữ liệu lưu sau yêu cầu. Ghi HTTP status theo contract của đúng endpoint. Với ca có sự kiện, gửi qua điểm vào thật, chờ xử lý rồi đọc trạng thái đã lưu. Ghi lại `TC ID`, fixture, kết quả và bằng chứng lần chạy.
- Ví dụ dùng giá MAP 1.000 đ/trụ, COMBO 2.000 đ/trụ và cọc 100.000 đ làm **dữ liệu seed**. Ngưỡng NFR ghi “pilot” lấy theo phiếu. Đọc giờ theo UTC+7 và giữ `TenantId`, `FarmId`, `OrderId`, mã trụ và phiên bản kết quả riêng cho từng fixture.

## 1. Ca kiểm theo tính năng

Cột **Loại** ghi test level và kỹ thuật thiết kế. `EP` là phân vùng tương đương, `BVA` là giá trị biên, `DT` là bảng quyết định, `ST` là chuyển trạng thái, `UC` là kiểm theo use case.

### F-01 · Yêu cầu khảo sát, duyệt và tạo đơn (WF-01)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F01-U01 | Unit · EP | Tạo yêu cầu MAP từ dữ liệu hợp lệ và cấu hình đang áp dụng. | Có mã tra cứu, bắt đầu ở `Submitted`, dữ liệu yêu cầu được giữ đúng. | Có ứng viên A1 |
| TC-F01-U02 | Unit · BVA | Dùng thời điểm hiệu lực cấu hình giá: ngay trước mốc và đúng mốc. Tạo đơn ở mỗi thời điểm. | Mỗi đơn chụp đúng phiên bản giá/cọc theo mốc hiệu lực. | Cần bổ sung |
| TC-F01-U03 | Unit · DT | Xét duyệt Follow-up trên Farm có Baseline chưa phát hành. | Bị từ chối, không cấp Tenant, Farm hoặc Order. | Có ứng viên A2 |
| TC-F01-A01 | API · EP | Gửi yêu cầu thiếu trường bắt buộc hoặc mã dịch vụ ngoài danh mục MAP/COMBO. | API trả lỗi dữ liệu, không có yêu cầu mới hay mã tra cứu. | Có ứng viên A1 ở mức handler |
| TC-F01-A02 | API · DT | TenantOwner đăng nhập và gọi API duyệt yêu cầu của Admin. | Từ chối theo quyền, không tạo tài nguyên hoặc Outbox. | Có ứng viên A3 cho quyền |
| TC-F01-S01 | System · UC | Khách chọn gói, gửi yêu cầu và tra cứu. Admin bắt đầu xét duyệt rồi duyệt với checklist hợp lệ. | Tracking đúng trạng thái. Tạo đúng Tenant/Farm/Order, chụp giá/cọc và tạo lời mời theo nghiệp vụ. | Có ứng viên A1, A2 cho từng phần |
| TC-F01-S02 | System · UC | Chủ vườn có Baseline đã phát hành đặt Follow-up COMBO; bảng giá thay đổi sau khi tạo đơn. | Tạo Order trả phí mới trên đúng Tenant/Farm, tham chiếu Baseline, giữ giá và cọc đã chụp. | Có ứng viên A2 cho cấp Order |

### F-02 · Đặt lịch và tổ chức lượt bay (WF-02)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F02-U01 | Unit · DT | Lịch đã xác nhận, đủ cọc, Manager và drone hợp lệ. Đánh giá điều kiện tạo lượt đầu. | Cho tạo lượt thuộc đúng Order/Mission. | Có ứng viên A4 cho điều kiện domain |
| TC-F02-U02 | Unit · BVA | Xác nhận lịch ngay trước và đúng mốc hết hiệu lực theo cấu hình. | Quyết định đúng ở từng phía của mốc. Lịch hết hạn giữ nguyên. | Cần bổ sung |
| TC-F02-U03 | Unit · ST | Tạo lượt Re-flight từ lượt kết thúc lỗi, rồi thử với Order khác. | Lượt hợp lệ giữ liên kết lượt trước và cùng Order. Lượt khác Order bị từ chối. | Có ứng viên A4 |
| TC-F02-A01 | API · EP | Manager gửi khoảng lịch có giờ kết thúc bằng hoặc trước giờ bắt đầu. | Lỗi dữ liệu, không sinh lịch mới. | Cần bổ sung |
| TC-F02-A02 | API · DT | Owner của Farm B gọi xác nhận lịch của Order thuộc Farm A. | Từ chối, lịch không bị đổi. | Có ứng viên A5 mức tích hợp |
| TC-F02-S01 | System · UC | Manager đề xuất lịch, Owner xác nhận và nộp cọc, Manager tạo lượt. | Có lịch xác nhận, cọc đúng Order và lượt bay sẵn sàng cho checklist. | Có ứng viên A5, A6 cho từng phần |
| TC-F02-S02 | System · UC | Ảnh của lượt đầu không đạt. Manager mở Re-flight đã được duyệt và thử mở trùng lần nữa. | Chỉ có một lượt kế nhiệm đang hoạt động, cùng Order, giữ dữ liệu cũ và không thu thêm phí. | Có ứng viên A7 mức tích hợp |

### F-03 · Checklist và thực hiện chuyến bay (WF-02)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F03-U01 | Unit · DT | Checklist phiên bản đang áp dụng đủ mục bắt buộc và lượt sẵn sàng. Ghi bắt đầu. | Lượt sang `InFlight`, có bằng chứng checklist và thời điểm bắt đầu. | Có ứng viên A4 |
| TC-F03-U02 | Unit · BVA | Ghi thời điểm kết thúc bằng thời điểm bắt đầu, rồi thử sớm hơn một đơn vị thời gian. | Mốc bằng được xử lý theo luật thời gian của domain; mốc sớm hơn bị chặn và không ghi lịch sử mới. | Có ứng viên A4, đối chiếu nhánh bằng |
| TC-F03-U03 | Unit · DT | Gọi bắt đầu với bằng chứng checklist thuộc lượt khác. | Domain từ chối và giữ nguyên liên kết bằng chứng của từng lượt. | Có ứng viên A4 |
| TC-F03-A01 | API · EP | Gọi bắt đầu với tham chiếu checklist không hợp lệ hoặc thiếu bằng chứng bắt buộc. | Trả lỗi xác thực bằng chứng, không ghi bắt đầu chuyến. | Cần bổ sung HTTP |
| TC-F03-A02 | API · DT | Manager đăng nhập nhưng bị thu hồi phân công trước lúc bấm bắt đầu. | Từ chối và trạng thái giữ nguyên. | Có ứng viên A7 mức tích hợp |
| TC-F03-S01 | System · UC | Manager mở checklist trên mobile, hoàn tất, bắt đầu và ghi kết thúc lượt khảo sát. | Trạng thái đi qua `InFlight` đến `AwaitingUpload`, giữ log và mã lượt đúng. | Có ứng viên A4, A8 cho từng phần |
| TC-F03-S02 | System · UC | Lịch hợp lệ nhưng cọc chưa đủ xác nhận. Manager hoàn tất checklist và thử bắt đầu. | Chuyến bay bị chặn; sau khi cọc thực sự được xác nhận thì đánh giá lại và chỉ mở khi đủ điều kiện. | Có ứng viên A6 mức tích hợp |

### F-04 · Upload ảnh/GNSS và dựng orthophoto (WF-03)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F04-U01 | Unit · EP | Chốt manifest với ảnh, GNSS và checksum hợp lệ cùng AttemptId. | Lưu manifest bất biến, toàn bộ media thuộc đúng lượt. | Có ứng viên A8 ở mức tích hợp |
| TC-F04-U02 | Unit · BVA | Manifest chỉ có đúng một ảnh hợp lệ và đủ log bắt buộc, rồi thử manifest không có ảnh. | Ca một ảnh đi tiếp theo giới hạn fixture; ca rỗng bị chặn. Nếu policy đặt min cao hơn, dùng min và min−1. | Cần bổ sung |
| TC-F04-U03 | Unit · DT | Phát lại gói xử lý với cùng khóa và thay nội dung/checksum. | Từ chối nội dung khác dưới cùng danh tính; không sửa manifest đã chốt. | Có ứng viên A4, A8 cho bất biến |
| TC-F04-A01 | API · EP | Gọi chốt input với ảnh thiếu đối tượng lưu trữ hoặc checksum sai. | Trả lỗi theo contract; không duyệt input và không phát job. | Có ứng viên A8 mức tích hợp |
| TC-F04-A02 | API · DT | Manager được giao lượt A thử upload vào lượt B hoặc freeze manifest của lượt B. | Từ chối quyền truy cập, không gắn chéo media. | Có ứng viên A8 mức tích hợp |
| TC-F04-S01 | System · UC | Upload ảnh và GNSS hợp lệ, freeze, duyệt input và chạy worker với bộ ảnh mẫu. | Tạo GeoTIFF có tọa độ và truy vết về đúng manifest/lượt. | Có ứng viên A8 cho phần input |
| TC-F04-S02 | System · UC | Ngắt mạng giữa hai phần upload rồi khôi phục và gửi lại phần cuối. | Tải tiếp hoặc thử lại an toàn, thông báo rõ, không tạo ảnh trùng hay job trùng. | Cần bổ sung |

### F-05 · AI nhận diện và đối chiếu trụ (WF-03)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F05-U01 | Unit · EP | Đối chiếu ứng viên Follow-up ở đúng hàng/cột và trong ngưỡng ghép, có một trụ Baseline phù hợp. | Đề xuất đúng PlantProfileId và giữ tham chiếu bằng chứng. | Cần đối chiếu test matching hiện có |
| TC-F05-U02 | Unit · BVA | Đặt ứng viên ngay trong và ngay ngoài ranh chuẩn. | Phân loại theo quy tắc gần ranh; trụ ngoài ranh bị loại khỏi số trụ tính tiền. | Cần bổ sung |
| TC-F05-U03 | Unit · DT | Có hai ứng viên tranh một ID hoặc thiếu tín hiệu GNSS/hàng cột đáng tin. | Đưa trường hợp mơ hồ vào `NeedsReview`, không tự tạo hoặc cho nghỉ hồ sơ. | Cần đối chiếu test matching hiện có |
| TC-F05-A01 | API · EP | Gửi kết quả xử lý qua điểm nhận đang dùng với ID lượt hoặc hash manifest lệch. | Từ chối kết quả, không ghi ứng viên hoặc cập nhật sai Order. | Có ứng viên A8 cho kiểm provenance |
| TC-F05-A02 | API · DT | Owner gọi thao tác nội bộ phát hành/tái xử lý kết quả AI của Manager hoặc worker. | Từ chối quyền và không đổi bản kết quả. | Cần bổ sung |
| TC-F05-S01 | System · UC | Chạy COMBO trên orthophoto mẫu có trụ và dấu hiệu bệnh đã gắn nhãn. Manager duyệt. | Có ứng viên trụ, vị trí/vùng nghi bệnh và bằng chứng trong phạm vi nhãn; Owner chỉ nhận kết quả được duyệt và mở. | Cần bổ sung |
| TC-F05-S02 | System · UC | Chạy MAP Follow-up trên Farm từng có kết quả COMBO, thêm trụ ngoài ranh và ứng viên ghép mơ hồ. | MAP trả dữ liệu bản đồ, giữ lịch sử bệnh, gắn cờ ca mơ hồ và không tính tiền trụ ngoài ranh. | Cần bổ sung |

### F-06 · Duyệt Baseline, hồ sơ trụ và xem kết quả (WF-04)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F06-U01 | Unit · EP | Áp quan sát Follow-up đã duyệt cho một hồ sơ trụ đang hoạt động. | Giữ nguyên PlantProfileId, thêm một quan sát có ngày và bằng chứng. | Cần bổ sung |
| TC-F06-U02 | Unit · BVA | Gửi thao tác duyệt với phiên bản hiện tại rồi lặp lại bằng phiên bản cũ hơn đúng một đơn vị. | Bản hiện tại áp dụng đúng một lần; bản cũ bị chặn và không ghi đè. | Cần bổ sung |
| TC-F06-U03 | Unit · DT | Đề xuất bỏ/thay trụ mà không có bằng chứng Follow-up hoặc quyết định Manager. | Giữ hồ sơ hiện tại, chưa tạo trạng thái nghỉ/thay thế. | Cần bổ sung |
| TC-F06-A01 | API · EP | Gửi quyết định duyệt cho ứng viên/kết quả có ID thuộc batch khác của cùng Farm. | Lỗi ngữ cảnh; không tạo Baseline hay cập nhật hồ sơ. | Có ứng viên A9 về cùng ngữ cảnh, cần HTTP |
| TC-F06-A02 | API · DT | Owner A thử đọc tile, lịch sử trụ và báo cáo đã phát hành của Owner B. | Từ chối cả ba bề mặt; không trả dữ liệu nội dung hoặc link tải dùng được. | Cần bổ sung |
| TC-F06-S01 | System · UC | Duyệt đơn đầu đã thu cọc, hoàn tất thanh toán, mở bản đồ cho Owner. | Có Baseline đúng Order/Mission/version, một hồ sơ hoạt động mỗi trụ và báo cáo xem được khi đủ điều kiện. | Có ứng viên A9 cho điều kiện cấp Baseline |
| TC-F06-S02 | System · UC | Follow-up có trụ thiếu, trụ thay và trụ ghép được. Manager duyệt theo bằng chứng. | Hiển thị so sánh, lưu lịch sử và giữ Baseline cũ. Hồ sơ cũ còn trong lịch sử sau khi đổi vòng đời. | Cần bổ sung |

### F-07 · Thanh toán, khiếu nại và báo cáo (WF-05)

| TC ID | Loại | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-F07-U01 | Unit · EP | Đơn MAP có 150 trụ hợp lệ (cũng kiểm nhánh zero-pole ngoại lệ), giá 1.000 đ, đã cọc 100.000 đ; thay bảng giá cấu hình hoặc sửa hóa đơn trước release. | Tổng 150.000 đ, còn 50.000 đ, dùng giá đã chụp và chỉ cấn cọc một lần; sửa count giữ nguyên đơn giá/cọc đã chụp và vô hiệu hóa checkout cũ. | Cần bổ sung cho phần correction V3-05E |
| TC-F07-U02 | Unit · BVA | Thông báo mở lúc 14:00 ngày 10/11 UTC+7. Gửi ngay trước và đúng 00:00 ngày 18/11 UTC+7. | Trước mốc còn hạn, đúng mốc hết hạn theo 7 ngày lịch. | Cần bổ sung |
| TC-F07-U03 | Unit · BVA | Order đã nhận 150.000 đ. Yêu cầu hoàn có tổng tích lũy đúng 150.000 đ rồi vượt 1 đ. | Mức đúng trần được phép khi đủ điều kiện; mức vượt bị chặn; nhận tiền của đơn khác không tăng trần đơn này. | Có ứng viên A10 về cap, cần đối chiếu loại hoàn |
| TC-F07-A01 | API · DT | Owner gửi báo sai cho Order A có mã trụ thuộc đúng Farm nhưng chỉ xuất hiện ở Order B. Sau đó gửi mẫu kiểm soát với trụ đã phát hành ở A. | Từ chối mẫu ngoài phạm vi, không tạo hồ sơ; mẫu đúng trụ đúng đơn đi tiếp khi đủ điều kiện. | Cần bổ sung hồi quy theo code đối chiếu mã trụ |
| TC-F07-A02 | API · DT | Owner hoặc Manager dùng phiên đăng nhập hợp lệ gọi API doanh thu và export của Admin. | Cả hai bề mặt từ chối; không trả số liệu hay tệp Excel. | Cần bổ sung trên hai endpoint |
| TC-F07-S01 | System · UC | Manager duyệt kết quả. Owner thử mở khi còn thiếu số dư, sau đó trả đủ qua SePay sandbox và gửi lặp webhook hợp lệ. | Kết quả bị khóa trước khi đủ tiền, sau đó mở đúng bản; một giao dịch thực chỉ được ghi có và ghi nhận doanh thu một lần. | Có ứng viên A6, A11 về thanh toán |
| TC-F07-S02 | System · UC | Owner báo sai đúng trụ trong hạn; Manager xác minh, Admin xử lý và ghi chuyển khoản có chứng từ; phát hành bản sửa được duyệt. | Có dấu vết lỗi, quyết định và chứng từ, hoàn theo phạm vi đơn, giữ bản cũ để đối soát, bản sửa là bản hiện hành. Dashboard và Excel ghi một lần theo kỳ. | Có ứng viên A10 cho domain, cần luồng xuyên suốt |

## 2. E2E theo 5 luồng

Chạy từ điểm vào thực tế trên môi trường test bằng dữ liệu seed mới. Kỹ thuật **UC** cho cả 5 ca. Kết quả mong đợi của từng bước là điều kiện để bước sau chạy.

| TC ID | Luồng | Dữ liệu và bước chạy | Kết quả mong đợi |
|---|---|---|---|
| TC-E2E-01 | WF-01 | Khách mới gửi MAP và tra mã. Admin xét duyệt, tạo tài khoản/đơn, ghi lời mời. | Tracking đúng, có một Tenant/Farm/Order; snapshot giá/cọc đúng. |
| TC-E2E-02 | WF-02 | Chốt lịch, xác nhận cọc, làm checklist, kết thúc lượt và tải ảnh. Manager đánh giá ảnh không đạt rồi mở Re-flight. | Lượt mới cùng Order, lịch sử ảnh/lượt cũ còn nguyên, không thu thêm phí. |
| TC-E2E-03 | WF-03 | Dùng bộ ảnh/GNSS của Follow-up COMBO, duyệt manifest, chạy dựng ảnh + AI và mở màn hình Manager review. | Orthophoto đúng nguồn và ứng viên trụ/bệnh truy vết được; ca không chắc chắn chờ review. |
| TC-E2E-04 | WF-04 | Dùng Farm có Baseline, duyệt trụ ghép được và thay một trụ có bằng chứng. Owner xem kết quả Follow-up đã mở. | Baseline tham chiếu đúng phiên bản, định danh trụ khớp giữ nguyên, thay trụ có lịch sử và không mất dữ liệu bệnh. |
| TC-E2E-05 | WF-05 | Đơn 150.000 đ thu cọc tháng 10, phát hành sau khi thu thêm 50.000 đ tháng 11. Có hoàn khiếu nại 20.000 đ tháng 12. Admin xem báo cáo và tải Excel cho ba kỳ. | Tháng 10 thu 100.000 đ và ghi cọc giữ theo trạng thái; tháng 11 ghi giá trị 150.000 đ và dòng tiền 50.000 đ; tháng 12 ghi hoàn 20.000 đ. Dashboard và Excel cùng kỳ, không nhân đôi cọc/bản sửa. |

## 3. API kiểm chuyển trạng thái (29 ca)

Nén từ bảng `AllowedTransitions` xuống **29 ca cốt lõi** (**22 chuyển hợp lệ có API/sự kiện V3 thật + 7 chuyển không hợp lệ bị chặn**), loại bỏ các cặp trung gian/lặp và luồng Legacy `SurveyResult` đã bị khóa với đơn V3 (`V3-T43`), bổ sung trạng thái V3 mới như `SurveyOrder.AwaitingSettlement` (sau freeze V3-05D) và `SurveyPayment.Invalidated` (khi sửa hóa đơn V3-05E).

Test level là **API Integration**, kỹ thuật **ST**. Mỗi dòng kiểm trước/sau trên đúng bản ghi, seed đủ điều kiện rồi gọi thao tác và kiểm audit/lịch sử kèm theo.

### Đơn khảo sát V3 · 8 ca (`SurveyOrder`)

| TC ID | Trước | Thao tác và điều kiện | Sau |
|---|---|---|---|
| TC-ST-ORD-01 | `AwaitingAppointment` | Owner xác nhận lịch hợp lệ khi nghĩa vụ cọc còn thiếu. | `AwaitingDeposit` |
| TC-ST-ORD-02 | `AwaitingDeposit` | SePay xác nhận khoản cọc đủ, lịch vẫn còn hiệu lực. | `ReadyForCapture` |
| TC-ST-ORD-03 | `ReadyForCapture` | Manager bắt đầu lượt với đủ checklist, cọc và phân công; xem A7. | `Capturing` |
| TC-ST-ORD-04 | `ReadyForCapture` | Đảo khoản cấp vốn làm cọc thiếu trước khi bắt đầu; xem A6/A13. | `AwaitingDeposit` |
| TC-ST-ORD-05 | `AwaitingCaptureUpload` | Freeze manifest đầy đủ ảnh/GNSS/checksum của đúng lượt sau upload; xem A8. | `AwaitingInputReview` |
| TC-ST-ORD-06 | `Processing` | Manager freeze kết quả nội bộ (V3-05D) đủ điều kiện, sẵn sàng chốt hóa đơn. | `AwaitingSettlement` |
| TC-ST-ORD-07 | `AwaitingReflightDecision` | Manager mở Re-flight giữ lịch và cọc hợp lệ, không phát sinh phí dịch vụ mới; xem A7. | `ReadyForCapture` |
| TC-ST-ORD-08 | `AwaitingSettlement` | Chốt hóa đơn, đủ tiền (`Due = 0`), release và ghi nhận doanh thu đúng một lần (V3-06). | `Completed` |

### Lượt bay · 3 ca (`CaptureAttempt`)

| TC ID | Trước | Thao tác và điều kiện | Sau |
|---|---|---|---|
| TC-ST-CAP-01 | `Planned` | Ghi checklist đủ của đúng lượt, đúng Manager; xem A4. | `PreflightReady` |
| TC-ST-CAP-02 | `PreflightReady` | Ghi bắt đầu đúng bằng chứng, có cọc và lịch hợp lệ; xem A4/A7. | `InFlight` |
| TC-ST-CAP-03 | `Processing` | Freeze kết quả nội bộ chấp nhận xử lý nguồn hợp lệ (hoặc chuyển `ReflightRequired` khi lỗi chất lượng). | `Accepted` |

### Lịch hẹn & Yêu cầu khảo sát · 4 ca (`SurveyAppointment`, `SurveyRequest`)

| TC ID | Thực thể · Trước | Thao tác và điều kiện | Sau |
|---|---|---|---|
| TC-ST-APT-01 | `SurveyAppointment` · `Proposed` | Owner đúng đơn xác nhận lịch đề xuất còn hiệu lực; xem A5. | `Confirmed` |
| TC-ST-APT-02 | `SurveyAppointment` · `Confirmed` | Owner gửi yêu cầu đổi một lịch đã xác nhận kèm lý do; xem A5. | `RescheduleRequested` |
| TC-ST-REQ-01 | `SurveyRequest` · `UnderReview` | Admin duyệt đúng phiên bản, checklist đủ → provision nguyên tử; xem A2. | `Approved` |
| TC-ST-REQ-02 | `SurveyRequest` · `UnderReview` | Admin từ chối đúng phiên bản và lưu lý do (cũng đối chiếu nhánh khách rút `Withdrawn`). | `Rejected` |

### Thanh toán & Nghĩa vụ cọc · 3 ca (`SurveyPayment`, `SurveyDepositObligation`)

| TC ID | Thực thể · Trước | Thao tác và điều kiện | Sau |
|---|---|---|---|
| TC-ST-PAY-01 | `SurveyPayment` · `Processing` | SePay webhook xác nhận đúng khoản, đúng tham chiếu; xem A6/A11. | `Paid` |
| TC-ST-PAY-02 | `SurveyPayment` · `Pending`/`Processing` | Sửa hóa đơn trước release (V3-05E) vô hiệu hóa checkout `FinalBalance` cũ, webhook trễ chỉ vào đối soát. | `Invalidated` |
| TC-ST-DEP-01 | `SurveyDepositObligation` · `Satisfied` | Đảo khoản phân bổ khiến cọc thiếu và đóng lại điều kiện bay; xem A6/A13. | `Pending` |

### Phiên bản Baseline & Khiếu nại/Hoàn tiền · 4 ca (`FarmBaseMapVersion`, `QualityRefundCase`)

| TC ID | Thực thể · Trước | Thao tác và điều kiện | Sau |
|---|---|---|---|
| TC-ST-MAP-01 | `FarmBaseMapVersion` · `Draft` | Manager duyệt bản đồ đầu nội bộ (`V3-04`), lưu nguồn Order/Mission; Owner vẫn bị khóa. | `InternalApproved` |
| TC-ST-MAP-02 | `FarmBaseMapVersion` · `InternalApproved` | Phát hành phiên bản đã duyệt khi đủ điều kiện thanh toán (và `Superseded` khi có bản thay thế). | `Published` |
| TC-ST-CLM-01 | `QualityRefundCase` · `Submitted` | Manager xác minh; Admin chấp thuận trong hạn mức trần đơn, có lý do; xem A10 *(đối chiếu V3-08 khi tách Claim/Refund)*. | `Approved` |
| TC-ST-CLM-02 | `QualityRefundCase` · `Approved` | Ghi nhận chuyển khoản hoàn đã hoàn tất, lưu chứng từ và ngày giao dịch; xem A10. | `Refunded` |

### Chuyển trạng thái bị từ chối · 7 ca

| TC ID | Luồng | Điều kiện và thao tác | Kết quả mong đợi | Đối chiếu repo |
|---|---|---|---|---|
| TC-ST-NEG-01 | WF-01 | Admin đúng quyền gọi duyệt `SurveyRequest` vẫn ở `Submitted` (chưa `UnderReview`). | API chặn vì chưa xét duyệt, giữ `Submitted`, không provision Order hay sinh lời mời. | Cần bổ sung HTTP |
| TC-ST-NEG-02 | WF-02 | Manager đúng phân công gọi hoàn tất `CaptureAttempt` đang `Planned` bằng payload hợp lệ. | Từ chối vì lượt chưa bay, không ghi giờ kết thúc hoặc reset dữ liệu lượt bay. | Có ứng viên A4 mức domain |
| TC-ST-NEG-03 | WF-03 | Manager gọi duyệt input khi `CaptureAttempt` còn `AwaitingUpload`, chưa có manifest đã tải. | Từ chối, không phát job và Order không sang chờ xử lý. | Có ứng viên A8 cho bước input |
| TC-ST-NEG-04 | WF-04 | Manager/writer gọi release hoặc gọi API Legacy (`ApproveResult`/`PublishResult`) cho đơn V3 khi chưa freeze hoặc còn thiếu tiền. | Từ chối (`InvalidTransition`/`ScopeInvalid`), giữ nguyên trạng thái và khóa kết quả với Owner. | Đối chiếu `V3-T43` |
| TC-ST-NEG-05 | WF-05 | Admin ghi nhận chuyển khoản hoàn cho hồ sơ khiếu nại còn `Submitted` (hoặc tổng hoàn vượt tiền đã thu). | Từ chối, không ghi nhận hoàn tất và không làm tăng số tiền hoàn trong báo cáo. | Có ứng viên A10 mức domain/tích hợp |
| TC-ST-NEG-06 | WF-02 | Admin/Owner hủy `SurveyOrder` khi đang có `CaptureAttempt` ở trạng thái `InFlight`. | Bị chặn (`ACTIVE_CAPTURE_IN_FLIGHT`), bảo toàn lượt bay đang hoạt động. | Có ứng viên A7 mức tích hợp |
| TC-ST-NEG-07 | WF-04 | Manager gửi freeze/finalize/correction bằng token hoặc phiên bản hóa đơn đã `Superseded`/`Invalidated` (stale revision). | Trả `409 Conflict` theo contract V3-05F, không ghi đè bản hiện hành. | Cần bổ sung test V3-05F |

## 4. Kiểm yêu cầu phi chức năng (7 ca — System level)

Theo [Test-Plans.md](Test-Plans.md), toàn bộ **7 NFR** được tính và đối soát ở test level **System** để khớp công thức cột `System` (`14 + 7 = 21` ca). Riêng `NFR3` và `NFR4` sử dụng bằng chứng chạy từ bộ API Integration/Concurrency làm minh chứng thành phần trong kịch bản System.

| TC ID | NFR và kỹ thuật | Thiết lập và thao tác | Kết quả mong đợi và bằng chứng |
|---|---|---|---|
| TC-NFR-01 | NFR1 · Hiệu năng (System) | Chạy tải cho dashboard, danh sách và các API đọc cốt lõi trên staging. Lưu cấu hình tải/máy, kích cỡ dữ liệu, thời gian upload và phiên review thực địa. | Có báo cáo p95 với mục tiêu pilot ≤ 2 s và thời gian review mục tiêu ≤ 30 phút/phiên. Lưu số liệu upload và giới hạn môi trường để tái lập. |
| TC-NFR-02 | NFR2 · Đánh giá AI (System) | Chạy trên tập giữ riêng có nhãn nhóm, cùng phần chia dữ liệu/phần cứng. Đo đếm trụ, so GNSS-only với GNSS+hàng/cột, đo pipeline COMBO trên từng lớp nghi bệnh. | Có sai số đếm và số lần đổi ID, precision/recall/F1 từng lớp. Mục tiêu pilot sai số đếm ≤ 10%, macro-F1 ≥ 0,70. Lưu dataset, seed và kết quả so sánh. |
| TC-NFR-03 | NFR3 · DT quyền và audit (System + API evidence) | Seed hai Tenant, tài khoản các vai trò, hồ sơ khiếu nại có ảnh và dữ liệu tài khoản nhận hoàn. Kiểm quyền đọc/sửa, truy cập media/signed URL, webhook đúng nguồn, đọc audit và xuất Excel. | Dữ liệu xuyên Tenant/Farm bị chặn, chỉ vai trò được cấp quyền xem chứng từ và xuất tài chính. Có audit các thao tác nghiệp vụ, không có dữ liệu thẻ lưu trong hệ thống. |
| TC-NFR-04 | NFR4 · DT toàn vẹn (System + API concurrency evidence) | Dùng hai tác vụ đồng thời xử lý cùng Order với thanh toán lặp, cùng trụ mới và cùng bản phát hành. Kiểm cả Follow-up/Re-flight, thời hạn khiếu nại và trần hoàn. | Chỉ một lần cấn cọc, phát hành và ghi nhận giá trị theo quy tắc. Một Baseline hiện hành/Farm, một hồ sơ hoạt động/trụ. Giữ đúng nguồn, version, phạm vi đơn và lịch sử. |
| TC-NFR-05 | NFR5 · Thử lỗi kết nối (System) | Ngắt mạng Web/Mobile trong lúc upload và ghi chú trên môi trường test, sau đó kết nối lại và gửi lại. | UI báo đang lỗi hoặc đang thử lại rõ ràng. Upload tiếp hoặc thử lại an toàn, không nhân đôi ảnh hoặc ghi chú; provenance còn đủ. |
| TC-NFR-06 | NFR6 · Quan sát tác vụ (System) | Cho người dùng thử lập lịch, checklist, gửi ảnh và xem tình huống lỗi bằng thiết bị test hoặc fixture được phép. Ghi số người hoàn thành, số lỗi và số lần cần hỗ trợ. | Báo cáo có tỷ lệ hoàn thành/nút thắt, có bằng chứng checklist và tài liệu xử lý tình huống an toàn theo phạm vi phiếu. |
| TC-NFR-07 | NFR7 · BVA + đối soát (System) | Seed đơn MAP/COMBO và hai giao dịch hai phía mốc 00:00 ngày đầu tháng UTC+7. Có cọc qua tháng, số dư, hoàn đã hoàn tất, khoản đang chờ, bản sửa, đơn chưa phát hành và đơn còn nợ. Chạy API doanh thu và Excel cùng bộ lọc. | Đối soát đến từng Order và tổng kỳ với dữ liệu nguồn. Giá trị dịch vụ ghi đúng một lần vào tháng phát hành. Hoàn ghi theo ngày hoàn tất; dòng tiền theo ngày giao dịch; pending bị loại. Tổng cọc giữ, còn phải thu và số đơn đã phát hành khớp Excel. |

## 5. Kịch bản UAT tính riêng (10 kịch bản)

Theo mục 8.1 và 8.3 Commitment, mỗi luồng có một demo chính và một demo ngoại lệ. Giữ mã demo DS trong file. Thực hiện bằng tài khoản demo, dữ liệu giả lập và môi trường test.

| Mã UAT | Luồng | Loại | Trình diễn | Tiêu chí đạt |
|---|---|---|---|---|
| DS-01a | WF-01 | Chính | Gửi yêu cầu MAP, tra cứu, Admin duyệt. | Có đúng một đơn với giá/cọc đã chụp. |
| DS-01b | WF-01 | Ngoại lệ | Đặt Follow-up trên Farm chưa có Baseline đã phát hành, sau đó xem Farm hợp lệ. | Farm chưa đủ điều kiện bị chặn; Farm hợp lệ tạo đơn mới trên Farm hiện có. |
| DS-02a | WF-02 | Chính | Xác nhận lịch, xác nhận cọc, hoàn tất checklist/lượt và tải dữ liệu. | Lượt chạy đúng đơn, trạng thái và ảnh đúng. |
| DS-02b | WF-02 | Ngoại lệ | Bộ ảnh không đạt, Manager yêu cầu Re-flight theo quy trình. | Giữ lượt và ảnh cũ; lượt mới nằm trong cùng Order, không thu phí dịch vụ mới. |
| DS-03a | WF-03 | Chính | Duyệt manifest và dựng bản đồ từ ảnh mẫu, chạy COMBO. | Xem orthophoto và ứng viên trụ/dấu hiệu bệnh cùng nguồn. |
| DS-03b | WF-03 | Ngoại lệ | Ngắt khi upload rồi gửi lại, xem ca ghép trụ không chắc chắn. | Không có ảnh trùng, ứng viên mơ hồ được đưa vào diện cần duyệt. |
| DS-04a | WF-04 | Chính | Duyệt kết quả, mở kết quả đã thanh toán; xem lịch sử và so sánh Follow-up. | Baseline đúng bản, giữ mã trụ đã khớp và quan sát có ngày. |
| DS-04b | WF-04 | Ngoại lệ | Xem MAP Follow-up sau COMBO và đề xuất thay trụ chưa đủ chứng cứ. | Lịch sử bệnh còn; hồ sơ trụ giữ nguyên trước khi Manager duyệt đủ chứng cứ. |
| DS-05a | WF-05 | Chính | Thanh toán số dư bằng sandbox, mở kết quả, mở báo cáo tháng và xuất Excel. | Trả đủ mới mở kết quả. API và Excel khớp kỳ và gói. |
| DS-05b | WF-05 | Ngoại lệ | Báo sai mã trụ của đơn khác, sau đó xử lý phản ánh hợp lệ và hoàn có chứng từ. | Báo sai ngoài đơn bị chặn; trụ đúng đơn được xử lý và khoản hoàn chỉ xuất hiện khi hoàn tất. |

## 6. Đối chiếu 13 tiêu chí nghiệm thu trong phiếu

| Mục trong phiếu | Tiêu chí | Ca chính |
|---:|---|---|
| 1 | Giá gói và cọc đã chụp cho đơn đầu/Follow-up | TC-F01-U02, TC-F01-S02, TC-F07-U01 |
| 2 | Đơn đầu trả phí; Follow-up dùng Tenant/Farm hiện có | TC-F01-U03, TC-F01-S02, TC-E2E-02 |
| 3 | Baseline có phiên bản; Follow-up tham chiếu đúng | TC-F06-S01, TC-F06-S02, TC-E2E-04 |
| 4 | Tính trụ trong ranh; cấn cọc đúng một lần | TC-F05-U02, TC-F07-U01, TC-F07-S01 |
| 5 | Re-flight cùng Order và không tăng phí | TC-F02-U03, TC-F02-S02, TC-E2E-02 |
| 6 | Một hồ sơ hoạt động/trụ và quan sát có bằng chứng | TC-F06-U01, TC-F06-S01, TC-NFR-04 |
| 7 | Thay đổi vòng đời có duyệt; giữ hồ sơ cũ | TC-F06-U03, TC-F06-S02, TC-E2E-04 |
| 8 | MAP giữ nguyên lịch sử bệnh | TC-F05-S02, TC-E2E-04 |
| 9 | Chỉ mở kết quả khi đã duyệt và đủ tiền | TC-F06-S01, TC-F07-S01, TC-ST-ORD-08, TC-ST-MAP-02, TC-ST-NEG-04 |
| 10 | Khiếu nại có bằng chứng trong 7 ngày sau thông báo mở | TC-F07-U02, TC-F07-A01, TC-F07-S02 |
| 11 | Hoàn đúng trần đơn, có chứng từ và audit | TC-F07-U03, TC-ST-CLM-02, TC-ST-NEG-05, TC-NFR-03 |
| 12 | Báo cáo sửa có phiên bản | TC-F07-S02, TC-ST-MAP-02, TC-ST-PAY-02 |
| 13 | Dashboard và Excel khớp giao dịch theo kỳ | TC-F07-A02, TC-E2E-05, TC-NFR-07 |

## 7. Test hiện có để đối chiếu & dọn test thừa (`backend/tests/`)

Các test dưới đây đã tìm thấy trong repo để gắn vào `TC ID` (chỉ chạy tập trung tại **V3-10**; các phase code `V3-01`–`V3-09` chỉ kiểm bằng `dotnet build backend/AgriDrone.sln --no-restore --nologo` theo quy định `AGENTS.md`).

| Mã đối chiếu | File hoặc phương thức | Phần đã có bằng chứng trong code test |
|---|---|---|
| A1 | `SurveyRequestV3IntakeTests` | Tạo yêu cầu/quote/tracking, tracking thiếu hoặc sai token, replay, Follow-up đúng hoặc sai Farm. |
| A2 | `SurveyApprovalV3Tests` | Provision khách mới, Follow-up giữ Tenant/Farm, chặn khi thiếu Baseline, phiên bản cấu hình và replay. |
| A3 | `SurveyV3ApiEndpointTests` | Route/auth và wiring với sender giả. |
| A4 | `CaptureAttemptTests` | Lịch sử chuyển trạng thái domain, Re-flight đúng đơn, điều kiện checklist và hoàn tất chuyến. |
| A5 | `V303H2AppointmentRaceTests` | Tranh chấp xác nhận/đổi lịch và từ chối người ngoài. |
| A6 | `V303H2FundingAcceptanceTests` | Replay thanh toán, sự kiện hợp lệ/sai, đảo cấp vốn và tranh chấp. |
| A7 | `V303H2CaptureTransactionRaceTests` | Thu hồi phân công hoặc trạng thái drone trong lúc bắt đầu và Re-flight kế nhiệm duy nhất. |
| A8 | `V303H2MediaSecurityAcceptanceTests` | Gắn media với đúng lượt, checksum khi freeze và duyệt đầu vào. |
| A9 | `V3BaselineDeliveryProofTests` | Bằng chứng release khớp Tenant/Farm và điều kiện chọn Baseline. |
| A10 | `QualityRefundCaseTests`, `V303H2CancellationAcceptanceTests` | Duyệt/từ chối và ghi chuyển khoản ở domain; test trần hoàn ở luồng hủy, cần chạy riêng cho khiếu nại. |
| A11 | `SurveyPaymentHandlerRegressionTests` | Điều kiện cọc, số dư bằng 0, xác nhận số dư và webhook lặp ở mức handler. |
| A12 | `FinancialReportTests` | Hoàn dư và hoàn khiếu nại trong tính toán doanh thu; cần đối chiếu API, Excel và giao dịch theo kỳ. |
| A13 | `V303H2RemediationDomainTests` | Đảo phân bổ cọc mở lại trạng thái còn thiếu và lưu sự kiện. |

### Kế hoạch dọn test thừa để giảm thời gian chạy (hiện 152 file / 867 method)
1. **Nhóm ưu tiên xóa ở `AgriDrone.IntegrationTests` (tác nhân chính làm chậm/treo khi chạy Docker Postgres):**
   - Các test migration/step cũ đã chốt vào chain canonical: `Step0RDiseaseZoneMigrationIntegrationTests`, `Step0RFarmBoundaryMigrationIntegrationTests`, `Step0RPerPoleMigrationIntegrationTests`, `Step1InfrastructureIntegrationTests`, `RegistrationV26MigrationTests`, `Phase5DatabaseFoundationIntegrationTests`, `V303B2MigrationIntegrationTests`, `MergeDepositPolicyCompatibilityTests`, `V3PrePersistenceModelCompatibilityTests`.
   - Các test step/flow cũ trùng phủ với `V303H2*`: `TwoStageMissionPreparationIntegrationTests`, `Uc03FlightEvidencePersistenceTests`.
2. **Nhóm ưu tiên xóa/gộp ở `AgriDrone.UnitTests`:**
   - Test luật cũ trái với V3 hoặc handler đã xóa: `SurveyComboDiscountPolicyTests` (V3 cấm giảm giá combo), `StreamlinedBaselineMappingCandidatesReadyV3Tests` (handler đã xóa ở V3-04), `Characterization/LegacyOperationalFlowCharacterizationTests`, `V2BusinessIntegrationContractTests`, `R2AdjustmentTests`, `SurveyRequestStep2Tests`, `SurveyApprovalStep3Tests`.
   - Gom/tỉa các file bị trùng kịch bản trong `Plants` / `Missions` / `Farms` về đúng các ca cốt lõi gắn mã `TC-*`.
3. **Nhóm cần viết bổ sung khi tới V3-06–V3-10:**
   - Test cho `V3-05D/E/F` (freeze kết quả nội bộ, sửa hóa đơn trước release, `SurveyPayment.Invalidated`, stale token `409`), `TC-F07-A01` (báo sai đúng trụ đúng đơn), `TC-NFR-07` (Dashboard–Excel), `TC-F06-S02` (lịch sử hồ sơ trụ), và `TC-E2E-01..05`.

**Thứ tự thực thi:** Bắt đầu với các ca chưa có bằng chứng xuyên suốt (`V3-05D/E/F`, `TC-F07-A01`, `TC-NFR-07`, `TC-F06-S02`, `TC-E2E-05`), gắn test hiện có vào mã `TC-*` tương ứng, và dọn các file thừa ở mục trên trước đợt chạy nghiệm thu **V3-10**. Khi hoàn thành, dùng cùng mã trong workbook và Report 5 mục 2, ghi môi trường/dữ liệu ở mục 1, lấy bảng ngưỡng và tổng hợp từ `Test Statistics` cho mục 3.

## Nguồn đối chiếu

- Hạng mục duy trì phiên đăng nhập dùng các ca kỹ thuật V3-T44–T48 tại V3-10. Số ca và demo theo Commitment dùng bảng tổng phía trên. [Ma trận nghiệm thu V3](../docs/testing/v3-business-acceptance.md#acceptance-scenarios)

- [BM01 Design Brief trong D:/FPT/Docx](../../Docx/SWP490_FA26SE218_R2_DesignBrief_Revised.docx) và [Review 2 Commitment trong D:/FPT/Docx](../../Docx/SWP490_FA26SE218_R2_Commitment.docx). Commitment ghi 90 ca và 10 demo ở mục 8.3. Thiết kế trong ma trận này dùng nghiệp vụ theo phiếu V2.6 cho những chỗ mô tả hai bản khác nhau.
- [Phiếu đăng ký FA26SE218 V2.6](Phieu_FA26SE218_V2.6.docx), mục 3.2(c)–(d).
- [Năm luồng nghiệp vụ AgriDrone](AgriDrone-Business-Workflows.md), [ma trận nghiệm thu V3](../docs/testing/v3-business-acceptance.md) và [truy vết quy tắc V3](../docs/domain/v3-traceability-matrix.md).
