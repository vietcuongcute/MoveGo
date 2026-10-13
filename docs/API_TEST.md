# Hướng dẫn và kết quả test API MoveGo

Tài liệu này dùng để test thủ công các API bằng **Swagger UI** và để làm báo cáo. Bảng kết quả ở mục 5 là kết quả chạy thật trên **ASP.NET Core 8 + PostgreSQL 16** ngày 13/10/2026: **69/69 trường hợp đạt**.

## 1. Chuẩn bị

```bash
docker compose up -d                   # PostgreSQL (cổng 5433)
cd MoveGo.API && dotnet run            # API tại http://localhost:5100
```

Mở **http://localhost:5100/swagger**. Lần chạy đầu API tự tạo bảng và nạp dữ liệu mẫu (4 chủ xe, 10 xe).

**Nhóm Admin cần khoá:** bấm nút **Authorize** (ổ khoá, góc trên bên phải Swagger) → nhập `movego-admin-dev` → Authorize. Các request sau đó tự gửi header `X-Admin-Key`. Không nhập hoặc nhập sai thì nhóm `/api/admin/*` trả **401**.

**Lấy token chủ xe** (các API `/api/owner/*` không đăng nhập mà dùng token trong đường dẫn):
- `manageToken` (cổng quản lý lịch): gọi `GET /api/admin/owners`, lấy phần cuối của trường `manageLink`.
- `token` của một đơn (link chủ xe bấm Chấp nhận/Từ chối): Zalo đang ở chế độ giả lập nên tin nhắn được in ra **cửa sổ console của API**, tìm dòng `confirm_link: http://localhost:5200/owner/confirm/<token>`, token là phần cuối.

## 2. Luồng chính (test theo thứ tự này)

| Bước | Request | Dữ liệu / ghi chú | Kết quả mong đợi |
|---|---|---|---|
| 1 | `GET /api/vehicles` | | 200, 10 xe |
| 2 | `GET /api/booking/available-vehicles` | `start`, `end` dạng `2026-10-23T08:00:00` | 200, danh sách xe còn trống |
| 3 | `POST /api/booking/quote` | `{"vehicleId":1,"startDate":"2026-10-23T08:00:00","endDate":"2026-10-25T08:00:00"}` | 200, tổng 1.300.000đ, cọc 10% = 130.000đ |
| 4 | `POST /api/booking/create` | `{"vehicleId":1,"startDate":"2026-10-23T08:00:00","endDate":"2026-10-25T08:00:00","customerName":"Nguyễn Văn A","customerPhone":"0912345678","pickupAddress":"12 Trần Hưng Đạo, Đà Lạt"}` | 201, có `code` (MG-XXXXXX), trạng thái `PendingOwner` |
| 5 | `GET /api/booking/{code}` | mã ở bước 4 | 200, `PendingOwner` |
| 6 | `POST /api/booking/lookup` | `{"phone":"0912 345 678"}` | 200, danh sách đơn của số này |
| 7 | `POST /api/owner/request/{token}` | `{"accept":true}` (token lấy ở console) | 200, `AwaitingDeposit` (xe được giữ 30 phút) |
| 8 | `POST /api/payments/mock/{code}/complete` | thanh toán giả lập (chưa có PayOS thật) | 200, `Confirmed` |
| 9 | `GET /api/booking/{code}` | | 200, `Confirmed` và **có SĐT chủ xe** (trước bước 8 thì chưa có) |
| 10 | `GET /api/admin/summary` | cần Authorize | 200, hoa hồng đã thu 130.000đ |

Luồng từ chối: ở bước 7 gửi `{"accept":false,"reason":"Xe đang bảo dưỡng"}` → đơn thành `Rejected`, khách xem được lý do ở bước 5.

Trạng thái đơn: `PendingOwner` (chờ chủ xe) → `AwaitingDeposit` (chờ cọc, giữ chỗ 30 phút) → `Confirmed` (đã cọc, lộ SĐT hai bên) → `Completed`; nhánh phụ `Rejected`, `Expired`, `Cancelled`.

## 3. Quy tắc nghiệp vụ nên nêu trong báo cáo
- Server tự tính tiền, không tin số tiền từ client. Cọc = 10% tổng, làm tròn đến 1.000đ, đây là hoa hồng của MoveGo; 90% còn lại khách trả trực tiếp cho chủ xe.
- Đơn mới gửi chưa khoá lịch xe; chỉ khi chủ xe đồng ý thì xe được giữ 30 phút cho khách đặt cọc. Hết hạn mà chưa cọc thì đơn tự chuyển `Expired`, xe tự mở lại.
- SĐT chủ xe và SĐT khách chỉ lộ sau khi khách đã cọc.
- Chống spam: tạo đơn tối đa 10 lần/phút/IP, tra cứu theo SĐT tối đa 8 lần/phút/IP (vượt thì trả 429); một SĐT không gửi trùng yêu cầu cho cùng một xe.
- Nhóm `/api/admin/*` bảo vệ bằng header `X-Admin-Key`; các API chủ xe dùng token ngẫu nhiên 256-bit trong link.

## 4. Mã trạng thái HTTP dùng trong API
200/201 thành công · 400 dữ liệu sai · 401 thiếu/sai khoá admin · 404 không tìm thấy · 409 xung đột (xe đã có lịch, đơn đã xử lý, xe đang có đơn) · 429 gọi quá nhanh.

## 5. Bảng kết quả test (69 trường hợp)

| # | Chức năng | Request | Dữ liệu gửi | Mong đợi | Thực tế | KQ | Ghi chú |
|---|---|---|---|---|---|---|---|
| 1 | Danh sách xe | `GET /api/vehicles` |  | 200 | 200 | PASS | 10 xe |
| 2 | Lọc xe máy điện | `GET /api/vehicles?category=ElectricBike` |  | 200 | 200 | PASS | 3 xe |
| 3 | Chi tiết xe | `GET /api/vehicles/1` |  | 200 | 200 | PASS | VinFast VF 3 |
| 4 | Chi tiết xe không tồn tại | `GET /api/vehicles/9999` |  | 404 | 404 | PASS | Not Found |
| 5 | Lịch bận của xe | `GET /api/vehicles/1/busy` |  | 200 | 200 | PASS | 0 khoảng bận |
| 6 | Lịch bận: khoảng ngày sai | `GET /api/vehicles/1/busy?from=2026-12-10&to=2026-12-01` |  | 400 | 400 | PASS | Khoảng ngày không hợp lệ. |
| 7 | Thông tin chung (meta) | `GET /api/meta` |  | 200 | 200 | PASS | hoa hồng 0.1 |
| 8 | Xe còn trống theo ngày | `GET /api/booking/available-vehicles?start=2026-10-23T08:00:00&end=2026-10-25T08:00:00` |  | 200 | 200 | PASS | 10 xe |
| 9 | Xe còn trống: thiếu ngày | `GET /api/booking/available-vehicles` |  | 400 | 400 | PASS | Thiếu ngày nhận hoặc ngày trả xe. |
| 10 | Xe còn trống: trả trước nhận | `GET /api/booking/available-vehicles?start=2026-10-25T08:00:00&end=2026-10-23T08:00:00` |  | 400 | 400 | PASS | Ngày trả xe phải sau ngày nhận xe. |
| 11 | Báo giá 2 ngày | `POST /api/booking/quote` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00"}` | 200 | 200 | PASS | tổng 1,300,000đ, cọc 130,000đ |
| 12 | Báo giá: xe không tồn tại | `POST /api/booking/quote` | `{"vehicleId": 9999, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00"}` | 404 | 404 | PASS | Không tìm thấy xe. |
| 13 | Báo giá: ngày sai | `POST /api/booking/quote` | `{"vehicleId": 1, "startDate": "2026-10-25T08:00:00", "endDate": "2026-10-23T08:00:00"}` | 400 | 400 | PASS | Khoảng ngày thuê không hợp lệ. |
| 14 | Tạo đơn hợp lệ | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0912345678", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 201 | 201 | PASS | mã MG-F4VQC5, PendingOwner |
| 15 | Tạo đơn: trùng yêu cầu cùng xe | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0912345678", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 409 | 409 | PASS | Bạn đã gửi yêu cầu cho xe này rồi, chủ xe đang xem xét. |
| 16 | Tạo đơn: thiếu tên | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "", "customerPhone": "0912345678", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 400 | 400 | PASS | Vui lòng nhập họ tên (2–100 ký tự). |
| 17 | Tạo đơn: SĐT sai | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "12345", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 400 | 400 | PASS | Số điện thoại không hợp lệ (ví dụ: 0912345678). |
| 18 | Tạo đơn: thiếu địa chỉ nhận xe | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0912345678", "pickupAddress": ""}` | 400 | 400 | PASS | Vui lòng nhập địa chỉ nhận xe (5–200 ký tự), ví dụ: 12 Trần Hưng Đạo, Đà Lạt. |
| 19 | Tạo đơn: ngày nhận đã qua | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-10T08:00:00", "endDate": "2026-10-12T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0911111111", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 400 | 400 | PASS | Thời gian nhận xe đã qua, vui lòng chọn lại. |
| 20 | Tạo đơn: xe không tồn tại | `POST /api/booking/create` | `{"vehicleId": 9999, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0911111112", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 404 | 404 | PASS | Không tìm thấy xe này. |
| 21 | Tạo đơn: thuê quá 30 ngày | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-12-12T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0911111113", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 400 | 400 | PASS | Mỗi đơn tối đa 30 ngày. |
| 22 | Xem đơn theo mã | `GET /api/booking/MG-F4VQC5` |  | 200 | 200 | PASS | PendingOwner |
| 23 | Xem đơn: mã không tồn tại | `GET /api/booking/MG-ZZZZZZ` |  | 404 | 404 | PASS | Không tìm thấy đơn này. |
| 24 | Tra cứu theo SĐT (có đơn) | `POST /api/booking/lookup` | `{"phone": "0912 345 678"}` | 200 | 200 | PASS | 1 đơn |
| 25 | Tra cứu theo SĐT (+84) | `POST /api/booking/lookup` | `{"phone": "+84912345678"}` | 200 | 200 | PASS | 1 đơn |
| 26 | Tra cứu theo SĐT (không có đơn) | `POST /api/booking/lookup` | `{"phone": "0987654321"}` | 200 | 200 | PASS | 0 đơn |
| 27 | Tra cứu theo SĐT sai định dạng | `POST /api/booking/lookup` | `{"phone": "123"}` | 400 | 400 | PASS | Số điện thoại chưa đúng. Hãy nhập số di động 10 số, ví dụ 0912 345 678. |
| 28 | Chủ xe xem yêu cầu | `GET /api/owner/request/I8xFoJzbSNYtsBmiMrsYMPlHHbMHa6zw-z96EyL3AkE` |  | 200 | 200 | PASS | PendingOwner, canRespond=True |
| 29 | Chủ xe xem: token sai | `GET /api/owner/request/token-sai` |  | 404 | 404 | PASS | Link không hợp lệ hoặc đã hết hạn. |
| 30 | Thanh toán giả lập khi chưa được chấp nhận | `POST /api/payments/mock/MG-F4VQC5/complete` |  | 409 | 409 | PASS | Đơn này chưa ở bước chờ đặt cọc. |
| 31 | Chủ xe chấp nhận | `POST /api/owner/request/I8xFoJzbSNYtsBmiMrsYMPlHHbMHa6zw-z96EyL3AkE` | `{"accept": true}` | 200 | 200 | PASS | AwaitingDeposit |
| 32 | Chủ xe trả lời lần 2 | `POST /api/owner/request/I8xFoJzbSNYtsBmiMrsYMPlHHbMHa6zw-z96EyL3AkE` | `{"accept": false}` | 409 | 409 | PASS | Đơn này đã được xử lý trước đó. |
| 33 | Khách xem đơn sau khi chấp nhận | `GET /api/booking/MG-F4VQC5` |  | 200 | 200 | PASS | AwaitingDeposit, giữ chỗ 1799s, chưa lộ SĐT chủ xe=True |
| 34 | Thanh toán cọc (giả lập) | `POST /api/payments/mock/MG-F4VQC5/complete` |  | 200 | 200 | PASS | Confirmed |
| 35 | Khách xem đơn sau khi cọc | `GET /api/booking/MG-F4VQC5` |  | 200 | 200 | PASS | Confirmed, SĐT chủ xe=0100000001 |
| 36 | Xe đã có lịch → không đặt trùng | `POST /api/booking/create` | `{"vehicleId": 1, "startDate": "2026-10-24T08:00:00", "endDate": "2026-10-26T08:00:00", "customerName": "Nguyễn Văn A", "customerPhone": "0922222222", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 409 | 409 | PASS | Rất tiếc, xe đã có lịch trong khoảng thời gian này. Vui lòng chọn ngày hoặc xe khác. |
| 37 | Tạo đơn thứ 2 (để từ chối) | `POST /api/booking/create` | `{"vehicleId": 2, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00", "customerName": "Trần Thị B", "customerPhone": "0933333333", "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt"}` | 201 | 201 | PASS | MG-5SDRLY |
| 38 | Chủ xe từ chối kèm lý do | `POST /api/owner/request/Nk19VhW6zx1l3DJMftVUsG7mjlqBWKoA5p2moYIKHDs` | `{"accept": false, "reason": "Xe đang bảo dưỡng"}` | 200 | 200 | PASS | Rejected |
| 39 | Khách thấy lý do từ chối | `GET /api/booking/MG-5SDRLY` |  | 200 | 200 | PASS | Rejected: Xe đang bảo dưỡng |
| 40 | Cổng quản lý của chủ xe | `GET /api/owner/manage/IZwCl7nQtnJPj1QhjgU764Jb872bfnQhNKkXpBjDO3I` |  | 200 | 200 | PASS | Anh Minh: 2 xe |
| 41 | Chủ xe khoá lịch | `POST /api/owner/manage/IZwCl7nQtnJPj1QhjgU764Jb872bfnQhNKkXpBjDO3I/vehicles/1/blocks` | `{"startDate": "2026-11-12T08:00:00", "endDate": "2026-11-14T08:00:00", "reason": "Đi công tác"}` | 200 | 200 | PASS | block #2 |
| 42 | Khoá lịch: ngày sai | `POST /api/owner/manage/IZwCl7nQtnJPj1QhjgU764Jb872bfnQhNKkXpBjDO3I/vehicles/1/blocks` | `{"startDate": "2026-11-14T08:00:00", "endDate": "2026-11-12T08:00:00", "reason": "x"}` | 400 | 400 | PASS | Khoảng ngày khoá không hợp lệ. |
| 43 | Xe bận đúng khoảng đã khoá | `GET /api/vehicles/1/busy` |  | 200 | 200 | PASS | 2 khoảng bận (có khoảng khoá) |
| 44 | Gỡ khoá lịch | `DELETE /api/owner/manage/IZwCl7nQtnJPj1QhjgU764Jb872bfnQhNKkXpBjDO3I/blocks/2` |  | 200 | 200 | PASS | True |
| 45 | Ẩn xe (chủ xe) | `POST /api/owner/manage/IZwCl7nQtnJPj1QhjgU764Jb872bfnQhNKkXpBjDO3I/vehicles/1/active?value=false` |  | 200 | 200 | PASS |  |
| 46 | Hiện lại xe (chủ xe) | `POST /api/owner/manage/IZwCl7nQtnJPj1QhjgU764Jb872bfnQhNKkXpBjDO3I/vehicles/1/active?value=true` |  | 200 | 200 | PASS |  |
| 47 | Cổng quản lý: token sai | `GET /api/owner/manage/token-sai` |  | 404 | 404 | PASS | Link quản lý không hợp lệ. |
| 48 | Admin không có khoá | `GET /api/admin/owners` |  | 401 | 401 | PASS | Unauthorized |
| 49 | Admin sai khoá | `GET /api/admin/owners` |  | 401 | 401 | PASS | Unauthorized |
| 50 | Admin: danh sách chủ xe | `GET /api/admin/owners` |  | 200 | 200 | PASS | 4 chủ xe |
| 51 | Admin: thêm chủ xe | `POST /api/admin/owners` | `{"name": "Anh Nam", "phoneNumber": "0944444444"}` | 200 | 200 | PASS | id 5 |
| 52 | Admin: thêm chủ xe SĐT sai | `POST /api/admin/owners` | `{"name": "X", "phoneNumber": "1"}` | 400 | 400 | PASS | Số điện thoại chủ xe không hợp lệ (cần 10 số, ví dụ 0912345678). |
| 53 | Admin: sửa chủ xe | `PUT /api/admin/owners/5` | `{"name": "Anh Nam (đã sửa)", "phoneNumber": "0944444444"}` | 200 | 200 | PASS | Anh Nam (đã sửa) |
| 54 | Admin: cấp lại link chủ xe | `POST /api/admin/owners/5/reset-link` |  | 200 | 200 | PASS | đã cấp link mới |
| 55 | Admin: thêm xe | `POST /api/admin/vehicles` | `{"ownerId": 5, "name": "Xe thử nghiệm", "brand": "VinFast", "category": "Car4Seats", "seats": 4, "pricePerDay": 700000, "pickupAddress": "Đà Lạt", "description": "Xe tạo khi test", "highlights": ["Điều hoà"], "isActive": true, "imageUrls": []}` | 200 | 200 | PASS | id 11 |
| 56 | Admin: thêm xe giá sai | `POST /api/admin/vehicles` | `{"ownerId": 5, "name": "Xe thử nghiệm", "brand": "VinFast", "category": "Car4Seats", "seats": 4, "pricePerDay": 100, "pickupAddress": "Đà Lạt", "description": "Xe tạo khi test", "highlights": ["Điều hoà"], "isActive": true, "imageUrls": []}` | 400 | 400 | PASS | Giá thuê một ngày phải từ 50.000đ đến 50.000.000đ. |
| 57 | Admin: chi tiết xe | `GET /api/admin/vehicles/11` |  | 200 | 200 | PASS | Xe thử nghiệm |
| 58 | Admin: sửa xe + gắn 2 ảnh | `PUT /api/admin/vehicles/11` | `{"ownerId": 5, "name": "Xe thử nghiệm", "brand": "VinFast", "category": "Car4Seats", "seats": 4, "pricePerDay": 750000, "pickupAddress": "Đà Lạt", "description": "Xe tạo khi test", "highlights": ["Điều hoà"], "isActive": true, "imageUrls": ["/uploads/vehicles/a.jpg", "/uploads/vehicles/b.jpg"]}` | 200 | 200 | PASS | 750,000đ, 2 ảnh |
| 59 | Admin: ẩn xe | `PUT /api/admin/vehicles/11/active` | `{"isActive": false}` | 200 | 200 | PASS |  |
| 60 | Admin: danh sách xe | `GET /api/admin/vehicles` |  | 200 | 200 | PASS | 11 xe |
| 61 | Admin: xoá xe chưa có đơn | `DELETE /api/admin/vehicles/11` |  | 200 | 200 | PASS | xoá hẳn=True |
| 62 | Admin: xoá xe đang có đơn | `DELETE /api/admin/vehicles/1` |  | 409 | 409 | PASS | Xe đang có đơn chờ xử lý hoặc đã xác nhận nên chưa thể xoá. Hãy ẩn xe và xoá sau khi các đơn này hoàn tất. |
| 63 | Admin: tổng quan | `GET /api/admin/summary` |  | 200 | 200 | PASS | hoa hồng 410,000đ |
| 64 | Admin: danh sách đơn | `GET /api/admin/bookings` |  | 200 | 200 | PASS | 3 đơn |
| 65 | Admin: lọc đơn theo trạng thái | `GET /api/admin/bookings?status=Confirmed` |  | 200 | 200 | PASS | 2 đơn |
| 66 | Admin: gửi lại tin cho chủ xe | `POST /api/admin/bookings/4/resend-owner` |  | 200 | 200 | PASS |  |
| 67 | Webhook PayOS khi chưa bật PayOS | `POST /api/payments/payos-webhook` |  | 400 | 400 | PASS |  |
| 68 | Giới hạn tạo đơn (10 lần/phút/IP) | `POST /api/booking/create` | `12 lần liên tiếp (dữ liệu sai để không tạo đơn)` | 429 | 429 | PASS | lần 1–10: 400 (thiếu tên), từ lần 11: 429 |
| 69 | Giới hạn tra cứu SĐT (8 lần/phút/IP) | `POST /api/booking/lookup` | `12 lần liên tiếp` | 429 | 429 | PASS | lần 1–8: 200, từ lần 9: 429 |
