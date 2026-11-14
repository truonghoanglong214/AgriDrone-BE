# V3 — FE và online Manager Mobile handoff

Date: **2026-11-10**. Status: **Target UX/API requirements, not live API documentation**. [Rules](../../Codex-Plan/Business-Rules-v3.md), [API intent](../openapi/survey-business-v3.openapi-intent.md), [contracts](../contracts/survey-business-v3.md), [acceptance](../testing/v3-business-acceptance.md).

## 1. Booking và catalogue

- Catalogue hai lựa chọn MAP và COMBO; Combo bao gồm mapping và suspected screening, không disease-only/Harvest/free Baseline/Follow-up price tier.
- Hiển thị rate và chung deposit từ response backend. Demo đã chốt 1.000/2.000 VND/trụ, deposit 100.000; chưa thay runtime config nên không hardcode hoặc hiển thị như API đã trả giá mới.
- First request public có tracking code/status tối thiểu. Existing Owner chọn existing Farm với released Baseline để Follow-up, hoặc request Farm mới trong Tenant hiện tại.
- Follow-up có thể chọn khác service trước đó; không tạo Tenant/Farm mới. Hiển thị Baseline reference và giá/cọc snapshot của Order mới.

## 2. Order/payment timeline

```text
Approved booking → appointment → deposit → capture/processing → Manager review
→ finalized Total/balance → released result + notice → history/claim
```

UI tách trạng thái lịch/capture/review/payment/release. Không hướng dẫn “trả đủ để mới bay Plant Health” hoặc bắt hai commercial appointments cho first Combo.

Payment initiation không gửi amount/currency/client Confirm; lấy checkout/QR/reference từ backend. Hiển thị pending/confirmed/mismatch/reconciliation rõ. Số tiền cuối theo verified in-boundary poles của Order × unit-price snapshot, không tự lấy current inventory trên màn hình tính lại.

Nếu Total nhỏ hơn tiền đã nhận: Due=0, có refundable excess và status riêng; khách xem reviewed result khi backend release, không cần tạo claim/chờ refund xong. Complaint refund về sau không tạo nợ mới hoặc nút “trả lại balance”.

## 3. Locked/released content

- Trước release có status/invoice/payment/refund metadata; invoice phải cho xem verified billable count, đơn giá, Total và Due để kiểm tra tiền. Ngoài billing metadata đó không preview per-pole details/geometry/disease counts, new profile/timeline/tiles/report/comparison/media qua endpoint khác.
- Existing released history không bị khóa vì đang mua Order mới. Nội dung của Order mới vẫn locked ở mọi projection.
- Released result hiển thị exact version, observation date, source/Baseline reference, PublishedAt/claim deadline, preliminary suspected limitation cho Combo.
- MAP disease panel ghi “Không khảo sát bệnh trong lần này”; không Healthy, clear disease hay dùng 0 findings như 0 bệnh.
- Mapping comparison giữa MAP/COMBO nếu backend xác nhận compatible; disease comparison chỉ giữa Combo, hiển thị basis/reason khi không tương thích.
- Withdrawal hiển thị version đã thu hồi và reviewed replacement; không giữ erroneous content như current trong cache/download. Lịch sử khiếu nại vẫn được truy cập theo quyền.

## 4. Manager online field work

- Assigned Farm/Order tasks, appointment/checklist/notes, imagery/GNSS upload và processing progress hoạt động online-only.
- Không offline cache/task mutation queue hoặc offline task-completion UI. Mất mạng hiển thị lỗi/trạng thái chưa lưu; upload có retry/resume rõ, không giả thành success.
- Same retry operation giữ ID; actual new operation có ID mới, version conflict cần reload/review. Revoked assignment không được tiếp tục mutation.
- Preflight/deposit readiness từ backend; balance không là capture gate. First capture không chờ bản đồ orthophoto sinh từ chuyến bay đó.
- Re-flight chất lượng chọn parent capture/reason và approval, cùng Order, không tạo checkout/deposit mới. Media/GNSS cũ giữ lại cho evidence.

## 5. Cancellation

Trước capture hoàn đủ. Weather/safety chưa phù hợp đổi lịch không mất cọc, khách dừng trước capture được hoàn đủ. No-show trước capture được một free reschedule; không tự tạo penalty.

Sau capture đạt, khách đổi ý hủy: backend/Admin xác minh evidence, giữ tối đa deposit, hoàn tiền đã nhận vượt cọc và không thu balance chưa thanh toán. Đội không thể bàn giao và dừng đơn: hoàn đủ. UI lấy quyết định/refund obligation từ backend, không tự suy “đã có ảnh là mất cọc”.

## 6. Claim/refund/correction

- Notice trong ứng dụng tạo cùng release là mốc chuẩn. Hiển thị deadline cụ thể từ backend theo UTC+7; không tự cộng 168 giờ hoặc chờ khách mở email.
- Ví dụ notice 10/11/2026 → nhận claim đến hết 17/11. Claim cần affected revision, error type, dated evidence. Claim gửi đúng hạn tiếp tục xử lý khi deadline đã qua.
- Manager verification và Admin refund proposal là hai bước khác. Owner chấp nhận đúng proposal version/amount; thay đề xuất phải accept lại.
- Hiển thị refund pending/processing/completed và authorized proof, không coi thao tác bấm/upload là transfer đã hoàn tất.
- Substantive corrected report có version/notice/window mới riêng; cosmetic edit không reset. Cap hoàn toàn Order, không reset theo số claim/report versions.

## 7. Admin financial views

Shared month/year/service filter cho dashboard và Excel. Hiển thị service value/service refunds/net revenue, receipts/all cash refunds/net cash, held deposits/refundable excess/cancellation retention/outstanding balances/pending items. Mốc tháng UTC+7, không flight date. Giải thích overpayment return không giảm service revenue chưa từng nhận.

Chỉ Admin xem/export. Không đưa bank details/proof URLs nhạy cảm vào export thông thường. Correction không sale lần hai; pending refund chưa là cash outflow.

## 8. Integration gate

Current JSON snapshots và handoff 2026-11-04 chỉ là baseline. Frontend triển khai v3 khi có actual reviewed API/schema implementation; không đổi enum/route client theo tài liệu target rồi giả định backend đã hỗ trợ. Các repository FE/Mobile không hiện diện trong workspace này chưa được sửa; tài liệu này là handoff yêu cầu cho chúng.
