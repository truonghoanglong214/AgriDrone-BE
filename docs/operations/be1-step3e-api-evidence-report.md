# BE1 Step 3E — Approval API và OpenAPI Evidence

> Evidence này phản ánh checkpoint trước amendment. R2 ngày 2026-10-18 đã bổ
> sung conditional initial state, versioned compatibility response và test;
> xem `be1-step3-r2-rebaseline-report.md` để biết trạng thái hiện hành.

Ngày chốt: 2026-10-11  
Trạng thái: `CodeComplete — AwaitingFinalTest`

## HTTP surface

- Route: `POST /api/system-admin/survey-requests/{requestId}/approve`.
- Authorization: controller dùng class-level policy `SystemAdmin`; OpenAPI kế thừa
  Bearer security toàn document và công bố `401`/`403`.
- Header: `Idempotency-Key` bắt buộc, được trim/validate bằng policy HTTP dùng chung,
  chiều dài tối đa 100 ký tự.
- Body: selected SystemManager profile là optional theo request kind; `reason`,
  `checklist` và `expectedVersion` là bắt buộc. `expectedVersion` phải lớn hơn 0.
- Controller chỉ chuyển HTTP DTO thành `SurveyRequestReviewChecklistInput`, gửi
  `ApproveSurveyRequestCommand` qua MediatR và map `Result`; không truy cập DbContext,
  repository hoặc tự quản lý transaction.

## Response và error contract

Success trả `200 OK` để first execution và exact idempotent replay có cùng HTTP
semantics. Payload gồm:

- Survey Request ID/number/status/version và approval review ID;
- Survey Order ID/number/status;
- Tenant, Farm và SystemManager profile IDs;
- nullable primary assignment, owner invitation và previous compatible order IDs;
- baseline requirement snapshot và approval timestamp.

Request/order status được map sang uppercase wire token ổn định. Payload không chứa
invitation token, password, applicant name/email/phone, review reason hoặc notes.
Các response được công bố: `200`, `400`, `401`, `403`, `404`, `409`, `422`.

## Runtime OpenAPI evidence

Snapshot `docs/openapi/be1-surveys.openapi.json` được sinh trực tiếp từ
`GET /swagger/v1/swagger.json`, không chỉnh JSON bằng tay. Kết quả kiểm tra runtime:

```text
OpenAPI: 3.0.4
Survey Request paths: 8
Approve route present: true
Bearer security inherited: true
Idempotency-Key required: true
Request body required: true
Required body fields: checklist, expectedVersion, reason
Responses: 200, 400, 401, 403, 404, 409, 422
Response contains secret/PII fields: false
```

## Verification và release gate còn lại

- API project build: pass, 0 warning, 0 error.
- `dotnet build backend/AgriDrone.sln --no-restore`: pass, 0 warning, 0 error.
- `dotnet ef migrations has-pending-model-changes ...`: không có pending model
  changes. Cảnh báo local `dotnet-ef` 8.0.22 cũ hơn runtime 10.0.10 không làm
  thay đổi kết quả model check.
- Không viết/chạy unit, PostgreSQL hoặc HTTP tests trong Step 3 theo plan. Atomic
  rollback trên PostgreSQL thật, concurrent retry và authorization HTTP vẫn là
  release gate bắt buộc tại Final Test Phase mục 27 trước khi chuyển Step 3 sang
  `Done`.

Step 3E không thêm database schema. Migration
`AddSurveyApprovalIdempotency` thuộc checkpoint 3D và vẫn là schema prerequisite
cho endpoint approval.
