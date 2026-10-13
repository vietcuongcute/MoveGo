# MoveGo: nền tảng kết nối thuê ô tô và xe máy điện

MoveGo hiện cho thuê tại **Đà Lạt** và chỉ là **trung gian**. Khách chọn xe, chủ xe đồng ý, khách cọc **10%** (đây là hoa hồng của MoveGo).
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
- Cọc online (hoa hồng MoveGo) = 10% tổng, làm tròn đến 1.000đ. Phần còn lại trả thẳng cho chủ xe.

Xe nào còn trống: một xe **bận** khi có đơn đã cọc, hoặc đơn đang giữ chỗ chưa hết hạn, hoặc chủ xe tự khoá ngày.
Hai khoảng trùng nhau khi `aStart < bEnd && aEnd > bStart` (trả xe 08:00 và nhận xe 08:00 cùng ngày không tính là trùng).
Đơn mới gửi (chưa được chủ xe đồng ý) **không** khoá lịch để không ai gửi yêu cầu ảo chiếm hết xe.

## Địa chỉ nhận xe

Không còn "điểm nhận xe" cố định. Khi đặt, khách tự nhập **địa chỉ muốn nhận xe** (khách sạn, nhà…); chủ xe thấy địa chỉ này trên trang xác nhận và trong tin Zalo (khoá `pickup_address` trong dữ liệu mẫu tin nhắn, nhớ thêm biến này vào template ZNS), rồi hai bên tự thống nhất giờ giao.

## Chạy trên máy (macOS)

Cần: .NET SDK 8, Docker, và `dotnet tool install --global dotnet-ef --version "8.*"`.

```bash
docker compose up -d                 # PostgreSQL, cổng 5433 trên máy bạn
# muốn nạp lại 10 xe mẫu ở Đà Lạt: docker compose down -v rồi docker compose up -d
cd MoveGo.API && dotnet run          # tự tạo/cập nhật bảng, nạp 10 xe mẫu ở Đà Lạt; terminal này in tin Zalo giả lập
# mở terminal khác:
cd MoveGo.Web && dotnet run          # http://localhost:5200
```

Chạy thử trọn luồng không cần Zalo hay PayOS thật:

1. Vào http://localhost:5200, bấm **Xe cho thuê** (hoặc **Đặt xe**), chọn ngày rồi bấm **Thuê xe** ở xe bạn thích, nhập **địa chỉ nhận xe ở Đà Lạt**, nhập tên và SĐT, gửi yêu cầu, rồi bấm **Theo dõi đơn của tôi** để mở trang theo dõi đơn.
2. Terminal của API in ra tin Zalo giả lập, trong đó có dòng `confirm_link`. Mở link đó (đóng vai chủ xe) và bấm **Đồng ý cho thuê**.
3. Quay lại trang theo dõi đơn: xuất hiện mã QR và đồng hồ đếm ngược 30 phút. Bấm **Giả lập đã thanh toán** (nút hiện khi PayOS đang tắt, và chỉ hoạt động khi API chạy ở môi trường `Development`).
4. Đơn chuyển sang **Đã xác nhận**, hiện SĐT chủ xe và địa chỉ nhận xe. Terminal in thêm hai tin Zalo giả lập.
5. Chủ xe có link quản lý lịch riêng (in ra terminal API lúc khởi động, dạng `/owner/manage/<token>`) để khoá ngày xe bận.

## Các trang web

| Địa chỉ | Nội dung |
|---|---|
| `/` | Trang chủ: giới thiệu, ô tìm xe, vài xe nổi bật |
| `/xe` | Xe cho thuê: tìm theo ngày giờ, lọc loại xe, bấm Thuê xe |
| `/tinh-gia` | Tính giá: nhập ngày để xem tổng tiền, tiền cọc 10% và phần trả chủ xe |
| `/quy-trinh` | Quy trình thuê xe từng bước |
| `/hoi-dap` | Hỏi đáp |
| `/tra-cuu` | Khách tra cứu bằng mã đơn (MG-XXXXXX) hoặc số điện thoại đã đặt; nhớ 5 đơn gần nhất trên thiết bị |
| `/booking/<mã>` | Khách theo dõi đơn |
| `/owner/...` | Trang chủ xe xác nhận đơn và quản lý lịch |
| `/admin` | Quản trị (đăng nhập bằng khoá admin): tổng quan, đơn, thêm/sửa xe, thêm chủ xe |

## Trang quản trị

Mở http://localhost:5200/admin. Khi chạy ở `Development`, khoá admin mặc định là `movego-admin-dev` (đặt trong `MoveGo.API/appsettings.Development.json`).

- **Chủ xe**: thêm người, sao chép link quản lý lịch gửi cho họ qua Zalo, cấp link mới nếu họ lỡ lộ link.
- **Xe**: thêm xe (chọn chủ xe, giá, ảnh, điểm nổi bật), sửa, ẩn/hiện trên web hoặc xoá. Đổi giá chỉ áp dụng cho đơn mới.
- **Xoá xe**: xe chưa từng có đơn thì xoá hẳn (kèm file ảnh đã tải lên). Xe đã có lịch sử đơn chỉ bị đánh dấu đã xoá và ẩn để số liệu hoa hồng không sai. Xe đang có đơn chờ xử lý hoặc đã xác nhận thì chưa xoá được.
- **Ảnh xe**: mỗi xe tối đa 8 ảnh (nên 5–6 ảnh từ ngoài vào trong), ảnh đầu tiên là ảnh bìa phủ kín khung thẻ xe; bấm ★ để đổi ảnh bìa. Tải JPG/PNG/WebP tối đa 5 MB mỗi ảnh, lưu ở `MoveGo.Web/wwwroot/uploads/vehicles/` (đã gitignore), hoặc dán link ảnh có sẵn. Khách bấm vào ảnh trên thẻ xe để xem cả bộ ảnh (mũi tên, vuốt, phím ← →).
- **Đơn**: lọc theo trạng thái, xem SĐT hai bên, gửi lại tin Zalo cho chủ xe khi lần đầu bị lỗi.

## Các API chính

| Endpoint | Việc |
|---|---|
| `GET /api/vehicles`, `GET /api/vehicles/{id}/busy` | Danh sách xe, các khoảng xe đang bận |
| `GET /api/booking/available-vehicles?start=&end=&category=` | Xe còn trống |
| `POST /api/booking/quote` | Báo giá |
| `POST /api/booking/create` | Khách gửi yêu cầu (giới hạn 10 lần/phút/IP) |
| `POST /api/booking/lookup` | Tra đơn theo SĐT đã đặt (tối đa 10 đơn/180 ngày, giới hạn 8 lần/phút/IP) |
| `GET /api/booking/{code}` | Khách xem trạng thái đơn |
| `GET/POST /api/owner/request/{token}` | Chủ xe xem và đồng ý/từ chối (link trong Zalo) |
| `GET /api/owner/manage/{manageToken}` và các `blocks` | Chủ xe quản lý lịch |
| `POST /api/payments/mock/{code}/complete` | Giả lập khách đã cọc (chỉ Development, PayOS tắt) |
| `POST /api/payments/payos-webhook` | PayOS báo thanh toán (kiểm tra chữ ký) |
| `GET /api/admin/summary`, `bookings`, `POST bookings/{id}/resend-owner` | Tổng quan, danh sách đơn, gửi lại Zalo cho chủ xe |
| `GET/POST /api/admin/owners`, `PUT owners/{id}`, `POST owners/{id}/reset-link` | Quản lý chủ xe, cấp lại link quản lý lịch |
| `GET/POST /api/admin/vehicles`, `PUT vehicles/{id}`, `PUT vehicles/{id}/active`, `DELETE vehicles/{id}` | Thêm, sửa, ẩn/hiện, xoá xe (xe có lịch sử đơn chỉ đánh dấu đã xoá) |

Mọi API admin cần header `X-Admin-Key` (cấu hình `Business:AdminKey`).

Cấu hình nghiệp vụ nằm ở mục `Business` trong `MoveGo.API/appsettings.json`
(tỉ lệ hoa hồng, số phút giữ xe, số giờ chủ xe được trả lời, số ngày thuê tối đa, địa chỉ website).

## Trước khi chạy thật

Zalo ZNS và PayOS được viết theo tài liệu chính thức nhưng **chưa được thử với tài khoản thật**. Mọi thứ khác đã chạy được ở chế độ giả lập.

1. **Zalo ZNS**: cần Zalo Official Account, đăng ký ứng dụng, và 5 template tin nhắn được Zalo duyệt (khoá: `owner_new_request`, `customer_accepted`, `customer_rejected`, `owner_confirmed`, `customer_confirmed`). Điền vào mục `Zalo` rồi đặt `Enabled: true`. Token tự gia hạn và lưu ở `App_Data/` (đã gitignore).
2. **PayOS**: tạo kênh thanh toán, điền `ClientId`, `ApiKey`, `ChecksumKey` vào mục `PayOs`, đặt `Enabled: true`, và đăng ký webhook trỏ về `https://<tên-miền>/api/payments/payos-webhook`.
3. **Khoá admin**: đổi `Business:AdminKey` thành chuỗi dài, ngẫu nhiên (đặt bằng biến môi trường `Business__AdminKey`). Để trống thì API admin tự tắt. Trang `/admin` chỉ nên chạy qua HTTPS.
   **Bí mật** (khoá API, mật khẩu database, `AdminKey`) đặt bằng biến môi trường hoặc `appsettings.Production.json`, **không commit lên GitHub**.
4. **Hoàn cọc** khi khách cọc nhưng xe không còn trống: hệ thống ghi chú vào đơn (`AdminNote`, xem ở `/api/admin/summary`), việc chuyển tiền hoàn bạn làm thủ công.
5. Ảnh xe tải lên nằm trên ổ đĩa của máy chủ web, nhớ sao lưu cùng database (hoặc chuyển sang kho lưu trữ đám mây khi chạy nhiều máy chủ).
6. Nên chạy HTTPS, đặt giới hạn truy cập và sao lưu database. Nên tham khảo thêm về đăng ký kinh doanh, thuế và điều khoản sử dụng trước khi thu tiền thật.

## Cấu trúc

```
MoveGo.API/   Models, Data (DbContext, dữ liệu mẫu, Migrations), Services (tính tiền, xe trống,
              vòng đời đơn, Zalo, PayOS, worker hết hạn), Controllers, Dtos
MoveGo.Web/   Controllers, Services/MoveGoApi.cs (gọi API qua IHttpClientFactory), Views (Razor),
              Styles/tailwind.css (nguồn), wwwroot (css đã build, js, font, logo)
```

Sửa giao diện Tailwind: `cd MoveGo.Web && npm install && npm run css:watch` (chỉ cần khi sửa `Styles/tailwind.css` hoặc class trong view).
