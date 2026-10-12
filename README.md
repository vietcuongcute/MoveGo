# Move-Go: nền tảng kết nối thuê ô tô và xe máy điện

Move-Go chỉ là **trung gian**. Khách chọn xe, chủ xe đồng ý, khách cọc **10%** (đây là hoa hồng của Move-Go).
Phần còn lại, cùng mọi thoả thuận khác như giao xe tận nơi hay phụ phí, **khách và chủ xe tự làm việc với nhau**.
Hệ thống không thu thêm khoản nào khác.

| Dự án | Vai trò | Địa chỉ |
|---|---|---|
| `MoveGo.API` | ASP.NET Core 8 Web API, EF Core, PostgreSQL | http://localhost:5100 (Swagger: `/swagger`) |
| `MoveGo.Web` | ASP.NET Core 8 MVC, giao diện Tailwind, gọi API | http://localhost:5200 |

## Luồng hoạt động

```
Khách chọn xe + ngày  ─►  Gửi yêu cầu (PendingOwner)  ─►  Zalo tới chủ xe kèm link xác nhận
        chủ xe bấm Đồng ý  ─►  AwaitingDeposit: xe được giữ 30 phút cho khách, khách quét QR cọc 10%
        khách cọc xong     ─►  Confirmed: hai bên nhận SĐT của nhau, trả 90% trực tiếp cho chủ xe
        chủ xe Từ chối / quá hạn  ─►  Rejected / Expired: xe tự mở lại
```

Quy tắc tính tiền (`Services/PricingService.cs`, server luôn tự tính, không tin số liệu từ trình duyệt):

- Số ngày = làm tròn lên theo block 24 giờ, tối thiểu 1 ngày (25 giờ tính 2 ngày).
- Tổng tiền = giá/ngày × số ngày.
- Cọc online (hoa hồng Move-Go) = 10% tổng, làm tròn đến 1.000đ. Phần còn lại trả thẳng cho chủ xe.

Xe nào còn trống: một xe **bận** khi có đơn đã cọc, hoặc đơn đang giữ chỗ chưa hết hạn, hoặc chủ xe tự khoá ngày.
Hai khoảng trùng nhau khi `aStart < bEnd && aEnd > bStart` (trả xe 08:00 và nhận xe 08:00 cùng ngày không tính là trùng).
Đơn mới gửi (chưa được chủ xe đồng ý) **không** khoá lịch để không ai gửi yêu cầu ảo chiếm hết xe.

## Chạy trên máy (macOS)

Cần: .NET SDK 8, Docker, và `dotnet tool install --global dotnet-ef --version "8.*"`.

```bash
docker compose up -d                 # PostgreSQL, cổng 5433 trên máy bạn
cd MoveGo.API && dotnet run          # tự tạo bảng, nạp 10 xe mẫu; terminal này in tin Zalo giả lập
# mở terminal khác:
cd MoveGo.Web && dotnet run          # http://localhost:5200
```

Chạy thử trọn luồng không cần Zalo hay PayOS thật:

1. Vào http://localhost:5200, bấm **Thuê xe**, chọn ngày, nhập tên và SĐT, gửi yêu cầu, rồi bấm **Theo dõi đơn của tôi** để mở trang theo dõi đơn.
2. Terminal của API in ra tin Zalo giả lập, trong đó có dòng `confirm_link`. Mở link đó (đóng vai chủ xe) và bấm **Đồng ý cho thuê**.
3. Quay lại trang theo dõi đơn: xuất hiện mã QR và đồng hồ đếm ngược 30 phút. Bấm **Giả lập đã thanh toán** (nút này chỉ có khi chạy `Development` với PayOS tắt).
4. Đơn chuyển sang **Đã xác nhận**, hiện SĐT chủ xe và địa chỉ nhận xe. Terminal in thêm hai tin Zalo giả lập.
5. Chủ xe có link quản lý lịch riêng (in ra terminal API lúc khởi động, dạng `/owner/manage/<token>`) để khoá ngày xe bận.

## Các API chính

| Endpoint | Việc |
|---|---|
| `GET /api/vehicles`, `GET /api/vehicles/{id}/busy` | Danh sách xe, các khoảng xe đang bận |
| `GET /api/booking/available-vehicles?start=&end=&category=&location=` | Xe còn trống |
| `POST /api/booking/quote` | Báo giá |
| `POST /api/booking/create` | Khách gửi yêu cầu (giới hạn 10 lần/phút/IP) |
| `GET /api/booking/{code}` | Khách xem trạng thái đơn |
| `GET/POST /api/owner/request/{token}` | Chủ xe xem và đồng ý/từ chối (link trong Zalo) |
| `GET /api/owner/manage/{manageToken}` và các `blocks` | Chủ xe quản lý lịch |
| `POST /api/payments/mock/{code}/complete` | Giả lập khách đã cọc (chỉ Development, PayOS tắt) |
| `POST /api/payments/payos-webhook` | PayOS báo thanh toán (kiểm tra chữ ký) |
| `GET /api/admin/...` | Quản trị, cần header `X-Admin-Key` (cấu hình `Business:AdminKey`) |

Cấu hình nghiệp vụ nằm ở mục `Business` trong `MoveGo.API/appsettings.json`
(tỉ lệ hoa hồng, số phút giữ xe, số giờ chủ xe được trả lời, số ngày thuê tối đa, địa chỉ website).

## Trước khi chạy thật

Zalo ZNS và PayOS được viết theo tài liệu chính thức nhưng **chưa được thử với tài khoản thật**. Mọi thứ khác đã chạy được ở chế độ giả lập.

1. **Zalo ZNS**: cần Zalo Official Account, đăng ký ứng dụng, và 5 template tin nhắn được Zalo duyệt (khoá: `owner_new_request`, `customer_accepted`, `customer_rejected`, `owner_confirmed`, `customer_confirmed`). Điền vào mục `Zalo` rồi đặt `Enabled: true`. Token tự gia hạn và lưu ở `App_Data/` (đã gitignore).
2. **PayOS**: tạo kênh thanh toán, điền `ClientId`, `ApiKey`, `ChecksumKey` vào mục `PayOs`, đặt `Enabled: true`, và đăng ký webhook trỏ về `https://<tên-miền>/api/payments/payos-webhook`.
3. **Bí mật** (khoá API, mật khẩu database, `AdminKey`) đặt bằng biến môi trường hoặc `appsettings.Production.json`, **không commit lên GitHub**.
4. **Hoàn cọc** khi khách cọc nhưng xe không còn trống: hệ thống ghi chú vào đơn (`AdminNote`, xem ở `/api/admin/summary`), việc chuyển tiền hoàn bạn làm thủ công.
5. Nên chạy HTTPS, đặt giới hạn truy cập và sao lưu database. Nên tham khảo thêm về đăng ký kinh doanh, thuế và điều khoản sử dụng trước khi thu tiền thật.

## Cấu trúc

```
MoveGo.API/   Models, Data (DbContext, dữ liệu mẫu, Migrations), Services (tính tiền, xe trống,
              vòng đời đơn, Zalo, PayOS, worker hết hạn), Controllers, Dtos
MoveGo.Web/   Controllers, Services/MoveGoApi.cs (gọi API qua IHttpClientFactory), Views (Razor),
              Styles/tailwind.css (nguồn), wwwroot (css đã build, js, font, logo)
```

Sửa giao diện Tailwind: `cd MoveGo.Web && npm install && npm run css:watch` (chỉ cần khi sửa `Styles/tailwind.css` hoặc class trong view).
