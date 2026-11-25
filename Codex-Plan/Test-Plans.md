Kiểm thử bắt buộc theo test level
Số test case tối thiểu tính cho từng test level, trên các mục nhóm đã cam kết: tính năng bắt buộc, main workflow, chuyển trạng thái của thực thể chính và NFR. Con số được ghi cố định trong Review 2 Commitment; Commitment có thể đặt mức cao hơn theo quy mô đề tài, không thấp hơn. Chọn level thấp nhất chứng minh được hành vi; tính năng có quy tắc nằm ở tầng API thì kiểm quy tắc bằng API test thay cho unit test, tổng số không đổi.

Test level	Kiểm gì	Tối thiểu	Không yêu cầu	Dòng trong Commitment	Chạy và ghi ở đâu
Unit test
level Component	Hàm hiện thực quy tắc nghiệp vụ, phép tính hoặc kiểm tra dữ liệu của tính năng, tách khỏi cơ sở dữ liệu và giao diện	3 cho mỗi tính năng bắt buộc: 1 case normal, 1 boundary, 1 abnormal của quy tắc	CRUD đơn thuần, getter/setter, mã sinh tự động; ngưỡng code coverage cho toàn dự án	Feature tests	Tự động trong CI. Thiết kế ở workbook Unit Test; mỗi test có một dòng level Component ở sheet Test Cases
API integration test
level Integration	API chạy cùng cơ sở dữ liệu của môi trường test: từ chối dữ liệu sai, phân quyền, chuyển trạng thái, tích hợp với dịch vụ ngoài	2 cho mỗi tính năng bắt buộc: 1 dữ liệu sai hoặc vi phạm quy tắc bị từ chối, 1 không đủ quyền.
1 cho mỗi chuyển trạng thái của thực thể chính, kể cả chuyển không hợp lệ bị từ chối	Mọi tổ hợp tham số; gọi dịch vụ bên thứ ba thật (dùng mock hoặc sandbox)	Feature tests; State transition tests	Tự động trong CI. Sheet Test Cases, level Integration
System test theo use case / user story
level System	Tính năng nhìn từ người dùng, qua giao diện, theo use case hoặc user story và tiêu chí chấp nhận của nó; các NFR đã chốt	2 cho mỗi tính năng bắt buộc: luồng chính và 1 luồng thay thế hoặc ngoại lệ của use case.
1 cho mỗi NFR đã chốt, đo đúng chỉ số và ngưỡng	Test cho từng màn hình và cho mọi luồng thay thế; trình duyệt và thiết bị ngoài môi trường đã khai báo; load test và security test ngoài NFR đã chốt	Feature tests; NFR	Tự động hoặc thủ công, ghi kết quả theo vòng. Sheet Test Cases, level System; kết quả NFR ở Report 5 mục 5
E2E workflow test
level System	Một main workflow trọn vẹn, từ hành động đầu tiên của actor tới kết quả nghiệp vụ, qua nhiều actor và nhiều tính năng	1 cho mỗi main workflow: kịch bản chính, chạy trên bản triển khai với dữ liệu mẫu	E2E cho kịch bản ngoại lệ (đã có trong UAT) và cho từng tính năng riêng lẻ	End-to-end workflow test	Nên tự động. Sheet Test Cases, Type = E2E workflow
UAT
level Acceptance	Kịch bản demo trên staging đã khai báo, dữ liệu mẫu nạp bằng script trong repo; người sở hữu workflow thực hiện, hoặc người dùng đại diện khi đề tài có khách hàng	2 kịch bản cho mỗi main workflow: 1 chính và 1 ngoại lệ, có minh chứng. Không tính vào số test case	Nghiệm thu chính thức với khách hàng khi đề tài không có khách hàng; khi đó ghi rõ người tham gia và hạn chế	Demo scenarios	Thủ công trên staging. Sheet Demo Scenarios; Report 5 mục 6
Tính số test case tối thiểu theo test level
Tính năng bắt buộc
15
Main workflow
5
Chuyển trạng thái
12
NFR đã chốt
4
15 × 7 + 5 + 12 + 4 = 126 test case
Unit test
45
API integration test
42
System test theo use case
34
E2E workflow test
5
UAT
10
kịch bản, không tính vào tổng
Tính năng × 7 (3 unit + 2 API + 2 system) + workflow + chuyển trạng thái + NFR. Con số trong Commitment không thấp hơn kết quả này.

Unit test và API test chạy tự động trong CI và chiếm phần lớn số test case, nên ngưỡng 50% tự động đạt được mà không cần tự động hoá test qua giao diện. Sheet Commitment của workbook Test Cases kiểm từng mục cam kết có đủ số test từng loại; sheet Test Statistics so số tối thiểu với số thực tế theo từng level.

Ngưỡng tại Hội đồng 1.1
Bốn dòng đầu là điều kiện của syllabus SEP490 để nhóm được bảo vệ ở vòng đầu; không đạt một trong các điều kiện này thì nhóm chờ vòng bảo vệ thứ hai; riêng trường hợp có report dưới 2 điểm thì nhóm làm lại đồ án. Các dòng còn lại là ngưỡng kiểm thử của bộ hướng dẫn, đặt cùng mức 75% với điều kiện hoàn thành của syllabus; Commitment có thể đặt mức cao hơn. Bảng ngưỡng của sheet Test Statistics tính sẵn cột Đạt cho từng tiêu chí.

Tiêu chí	Ngưỡng	Cách kiểm tra	Căn cứ trong hồ sơ
Use case hoàn thành
syllabus	Ít nhất 75% số use case, chức năng, màn hình đã nộp ở Report 3; và ít nhất 20 use case mức trung bình	Demo; đối chiếu test luồng chính của từng tính năng	Sheet Commitment và Test Statistics
Defect
syllabus	Không quá 3 lỗi logic rõ ràng (Major) và không quá 1 lỗi show-stopper (Critical: người dùng không hoàn thành được use case)	Demo; đối chiếu defect list	Report 5 mục 4; issue tracker
Tự thực hiện
syllabus	Nhóm chứng minh được tự làm report và sản phẩm	Mỗi thành viên mở repo, chỉ ra và giải thích phần mình sở hữu	Repo, lịch sử commit
Điểm quá trình
syllabus	Mỗi report từ 2 điểm; điểm quá trình từ 5 điểm	Giảng viên hướng dẫn chấm bảy report	Phiếu điểm quá trình
Kịch bản demo	Ít nhất 75% kịch bản chính đạt; ít nhất 50% kịch bản ngoại lệ đạt	Demo theo kịch bản; dữ liệu đầu vào có thể được thay	Sheet Demo Scenarios; Report 5 mục 6
Số test case	Không thấp hơn con số đã cam kết	Đối chiếu Report 5 với Commitment	Sheet Commitment
Tỷ lệ test đạt	Ít nhất 75% tổng số test case; ít nhất 75% tính năng bắt buộc có test luồng chính đạt	Chạy lại bộ test tự động; đối chiếu test report	Sheet Test Statistics và Test Runs; Report 5 mục 3
Tự động hoá	Ít nhất 50% test case chạy tự động trong CI	Chạy CI trên bản nộp	Sheet Test Statistics; lịch sử CI
Syllabus không quy định cách đếm use case hoàn thành: bộ hướng dẫn dùng test luồng chính đạt trên bản nộp làm căn cứ, hội đồng xác nhận qua demo. Sau buổi bảo vệ, một thành viên không đạt khi không demo được chức năng hay use case nào, hoặc không chứng minh được phần mình tự làm.

Quy tắc đếm test case
Được đếm
Mỗi test case có một TC ID, kiểm một mục cam kết (cột Covers) và tính cho một thành viên (cột Owner).
Loại test (cột Type) đúng với loại của mục cam kết: bốn loại Feature cho tính năng (luồng chính, luồng thay thế, dữ liệu hoặc quy tắc, ngoại lệ hoặc phân quyền), E2E cho workflow, state transition cho chuyển trạng thái, NFR cho NFR.
Có kết quả mong đợi kiểm được và có kết quả chạy trên bản nộp.
Unit test được đếm khi có một dòng trong workbook Test Cases với level Component.
Không được đếm
Test case không gắn với mục nào trong Commitment, hoặc có Type = Other.
Hai test case chỉ khác dữ liệu trong cùng một lớp tương đương: tính là một.
Test không có kết quả mong đợi kiểm được, hoặc bị bỏ qua khi chạy.
Kịch bản demo: ghi ở sheet Demo Scenarios, có ngưỡng riêng.
Giữ đúng phạm vi
Con số cam kết không thấp hơn kết quả công thức; không cần cam kết cao hơn nhiều so với công thức.
Test thêm để trong repo và chạy trong CI; không cần đưa từng test vào workbook.
Kết quả ghi theo vòng; chạy lại sau khi sửa lỗi thì ghi vòng mới, vòng cũ giữ nguyên.
Mã dùng chung với Commitment: F-xx, WF-xx, DS-xx, NFR-xx; TR-xx cho chuyển trạng thái; cột Related ghi FR, BR, UC của Report 3.
Test run và test report
Số liệu chỉ có giá trị khi gắn với một lần chạy trên đúng phiên bản nộp. Mỗi lần chạy được ghi lại, và toàn bộ bộ test chạy lại được bằng một lệnh.

Hồ sơ	Nội dung	Dùng để
Sheet Commitment	Các mục cam kết với số test tối thiểu, thực tế và đã đạt của từng mục	Đối chiếu số test case; tìm mục chưa có đủ test
Sheet Test Cases	Từng test case: mục được kiểm, loại, level, các bước, dữ liệu, kết quả mong đợi, owner, kết quả từng vòng	Chạy lại một test bất kỳ; biết test nào thuộc về ai
Sheet Test Runs	Từng lần chạy: ngày, phiên bản (tag hoặc commit), môi trường, tổng số, đạt, không đạt, link CI; lệnh chạy toàn bộ	Chứng minh kết quả là của bản nộp; chạy lại để so sánh
Sheet Test Statistics	Bảng ngưỡng và bảng theo loại, level, workflow (đúng các dòng của Completion Report), thành viên	Report 5 mục 3; phần test case của Completion Report
Report 5	Test case trọng tâm (mục 2), kết quả và kết luận (mục 3), defect (mục 4), NFR (mục 5), demo và validation (mục 6), ma trận truy vết (mục 7)	Tài liệu kiểm thử của đề tài; số liệu khớp workbook và Completion Report
Ba góc nhìn
Nhóm sinh viên
Khi lập Commitment
Liệt kê tính năng bắt buộc, main workflow, chuyển trạng thái và NFR; tính số test case tối thiểu theo từng test level.
Viết tiêu chí chấp nhận sao cho chuyển được thành test có kết quả đạt hoặc không đạt rõ ràng.
Khi thực hiện
Viết test cùng lúc với tính năng; test tự động chạy trong CI ở mỗi pull request.
Ghi kết quả test thủ công theo vòng, có ngày và người chạy.
Trước Hội đồng 1.1
Chạy toàn bộ test bằng một lệnh trên bản gắn tag; cập nhật Test Runs, Test Statistics, Report 5 và Completion Report với cùng số liệu.
Mọi kịch bản demo chạy được trên staging với dữ liệu mẫu.
Mỗi thành viên chạy và giải thích được test của phần mình sở hữu.
GVHD
Trước khi đồng ý Commitment
Số test case cam kết không thấp hơn kết quả công thức.
Mỗi tính năng bắt buộc có tiêu chí chấp nhận đo được; mỗi main workflow có kịch bản chính và ngoại lệ.
Mỗi test case và mỗi kịch bản demo có một người phụ trách.
Trong quá trình
Xem cột Covered của sheet Commitment và lịch sử CI để biết mục nào chưa có test.
Theo dõi test không đạt kéo dài và các defect Critical (show-stopper), Major (lỗi logic) đang mở.
Trước Hội đồng 1.1
Bảng ngưỡng của sheet Test Statistics không còn dòng chưa đạt.
Số liệu của Report 5, workbook và Completion Report khớp nhau và khớp lần chạy CI mới nhất.
GV phản biện và Hội đồng 1.1
Tại Review 2
Chốt tính năng bắt buộc, main workflow, kịch bản demo, số test case và ngưỡng; có thể đặt mức cao hơn mức tối thiểu theo quy mô đề tài.
Tại Hội đồng 1.1
Nhóm chạy bộ test và CI trên bản nộp; số liệu được đối chiếu với Report 5 và Completion Report.
Ít nhất hai main workflow được chọn ngẫu nhiên để demo kịch bản chính và ngoại lệ trên staging; dữ liệu đầu vào có thể được thay.
Mỗi thành viên mở repo, chỉ ra phần mình sở hữu và chạy test liên quan.
Khi từng workflow được thẩm định theo tiêu chí C1–C8, tiêu chí C8 Testing & Traceability dựa vào bảng theo workflow của sheet Test Statistics, sheet Commitment và ma trận ở Report 5 mục 7.
Kết luận
Từng tiêu chí trong bảng ngưỡng được ghi Đạt hoặc Không đạt, kèm căn cứ.

## Kiểm thử kỹ thuật V3-10 — Duy trì phiên đăng nhập

- **API Integration:** Kiểm refresh đúng scope, rotation đồng thời, sử dụng lại token cũ, logout và thu hồi theo trạng thái tài khoản/quyền.
- **System:** Kiểm reload, JWT hết hạn, nhiều tab, lỗi mạng và domain cookie trên môi trường FE/API đích.
- Theo dõi V3-T44–T48 trong đợt nghiệm thu V3-10. Tổng ca theo Commitment lấy theo danh mục đã chốt. [Ma trận nghiệm thu V3](../docs/testing/v3-business-acceptance.md#acceptance-scenarios)
