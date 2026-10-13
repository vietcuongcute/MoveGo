# MoveGo API — Tài liệu mô tả đầy đủ

Tài liệu này mô tả **toàn bộ** endpoint của `MoveGo.API` (ASP.NET Core 8). Khi chạy ở môi trường Development, bạn còn có Swagger tại `/swagger` (có nút *Authorize* để nhập `X-Admin-Key`). File kiểm thử bằng curl nằm ở [`API_TEST.md`](API_TEST.md).

- [1. Quy ước chung](#1-quy-ước-chung)
- [2. Luồng đặt xe (vòng đời một đơn)](#2-luồng-đặt-xe-vòng-đời-một-đơn)
- [3. Xe & dữ liệu chung (công khai)](#3-xe--dữ-liệu-chung-công-khai)
- [4. Đặt xe (khách)](#4-đặt-xe-khách)
- [5. Chủ xe (xác thực bằng token trong link)](#5-chủ-xe-xác-thực-bằng-token-trong-link)
- [6. Thanh toán & webhook](#6-thanh-toán--webhook)
- [7. Quản trị (Admin)](#7-quản-trị-admin)
- [8. Bảng tra nhanh](#8-bảng-tra-nhanh)

---

## 1. Quy ước chung

### Base URL

| Môi trường | Base URL |
|---|---|
| Chạy local (mặc định) | `http://localhost:5101` (web MVC gọi API ở đây) |
| Chế độ nhúng (MonsterASP) | cùng domain với website, tiền tố `/api/...` |
| Ứng dụng di động | nhập trong tab *Cài đặt* (mặc định ở `MoveGo.Mobile/src/config.ts`) |

Tất cả đường dẫn dưới đây đều bắt đầu bằng `/api/...` (trừ `GET /` và `/swagger`).

### Định dạng

- Request/Response: `application/json`, tên trường **camelCase**.
- **Enum trả về dạng chuỗi** (ví dụ `"status": "AwaitingDeposit"`). Khi gửi lên, enum nhận được cả chuỗi lẫn số.
- **Ngày giờ**: chuỗi ISO 8601 theo *giờ địa phương, không có múi giờ*, ví dụ `2026-10-23T08:00:00`.
- **Tiền**: số (VND), không có dấu phân cách, ví dụ `1500000`.
- **Số điện thoại**: nhập `0912345678`, `0912 345 678`, `+84912345678` đều được; API chuẩn hoá về 10 số.

### Định dạng lỗi (ProblemDetails, RFC 7807)

Mọi lỗi đều có dạng sau; **`title` là câu tiếng Việt có thể hiển thị thẳng cho người dùng**:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Không tìm thấy xe này.",
  "status": 404,
  "traceId": "00-9f5b0a3c4e2d1a7b8c6d5e4f3a2b1c0d-1a2b3c4d5e6f7a8b-00"
}
```

| Mã | Ý nghĩa thường gặp |
|---|---|
| 400 | Dữ liệu gửi lên sai/thiếu |
| 401 | Sai `X-Admin-Key` (admin) hoặc sai khoá webhook |
| 404 | Không tìm thấy (mã đơn, xe, token sai) |
| 409 | Xung đột trạng thái (xe đã bận, đơn đã xử lý…) |
| 410 | Yêu cầu đã hết hạn |
| 429 | Vượt giới hạn tần suất / quá nhiều đơn chờ |
| 502 | Không tạo được link thanh toán |
| 503 | Admin API tắt (chưa cấu hình `Business:AdminKey`) |

### Xác thực

| Loại | Cách dùng | Áp dụng cho |
|---|---|---|
| Công khai | Không cần | Mục 3, 4, `GET /api/meta` |
| Token trong URL | Token ngẫu nhiên dài nằm trong đường dẫn (link gửi qua Zalo) | Mục 5 |
| Admin | Header `X-Admin-Key: <khoá>` (cấu hình `Business:AdminKey`; khoá dev `movego-admin-dev`) | Mục 7 |
| Webhook | `Authorization: Apikey <khoá>` / `secure-token` / `X-Webhook-Secret` / chữ ký PayOS | Mục 6 |

### Giới hạn tần suất (rate limit, theo IP, cửa sổ 1 phút)

| Chính sách | Endpoint | Giới hạn |
|---|---|---|
| `booking` | `POST /api/booking/create` | 10 lần/phút |
| `lookup` | `POST /api/booking/lookup` | 8 lần/phút |

Vượt giới hạn → `429 Too Many Requests`.

### Bảng enum

**BookingStatus** (trạng thái đơn)

| Giá trị chuỗi | Số | Ý nghĩa |
|---|---|---|
| `PendingOwner` | 0 | Chờ chủ xe xác nhận |
| `AwaitingDeposit` | 1 | Chủ xe đã nhận, chờ khách cọc 10% (đang giữ xe) |
| `Confirmed` | 2 | Đã cọc — khách thấy SĐT chủ xe |
| `Rejected` | 3 | Chủ xe từ chối |
| `Cancelled` | 4 | Đã huỷ |
| `Expired` | 5 | Hết hạn (chủ xe không trả lời / khách không cọc kịp) |
| `Completed` | 6 | Hoàn tất chuyến |

**VehicleCategory** (loại xe)

| Giá trị chuỗi | Số | Nhãn |
|---|---|---|
| `Car4Seats` | 1 | Xe 4 chỗ |
| `Car7Seats` | 2 | Xe 7 chỗ |
| `ElectricBike` | 3 | Xe máy điện |

### Quy tắc nghiệp vụ cần biết

- **Số ngày thuê** = làm tròn lên (số giờ / 24), tối thiểu 1 ngày; tối đa theo `maxRentalDays` (xem `/api/meta`).
- **Tiền cọc (hoa hồng MoveGo)** = 10% tổng tiền, làm tròn đến 1.000đ. Phần còn lại (`ownerAmount`) khách trả thẳng cho chủ xe.
- **Xe bận** chỉ khi có đơn `Confirmed`, đơn `AwaitingDeposit` còn hạn giữ chỗ, hoặc chủ xe tự khoá lịch. Đơn `PendingOwner` **không** chặn xe.
- **Mã đơn**: `MG-` + 6 ký tự từ bảng `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (bỏ I, O, 0, 1 để dễ đọc), ví dụ `MG-EK9CFV`.
- **Nội dung chuyển khoản** của đơn: `MG` + 6 ký tự (ví dụ `MGEK9CFV`) — webhook ngân hàng dùng mã này để nhận diện đơn.

---

## 2. Luồng đặt xe (vòng đời một đơn)

```
Khách                         API                              Chủ xe
 │ GET  /vehicles, /booking/available-vehicles
 │ POST /booking/quote  ───────► báo giá
 │ POST /booking/create ───────► PendingOwner ── tin Zalo (link /owner/request/{token}) ─►│
 │                                                                                        │
 │                              ◄── POST /owner/request/{token} {accept:true} ────────────┤
 │ GET /booking/{code}  ◄──────  AwaitingDeposit (kèm QR/bank, giữ xe N phút)             │
 │ chuyển khoản 10% ──► SePay/Casso ──► POST /payments/bank-webhook                       │
 │ GET /booking/{code}  ◄──────  Confirmed (kèm SĐT chủ xe)                               │
 │ trả 90% trực tiếp cho chủ xe khi nhận xe                                               │
```

Nhánh phụ: chủ xe từ chối → `Rejected`; chủ xe im lặng quá hạn → `Expired`; khách không cọc kịp → `Expired`.

---

## 3. Xe & dữ liệu chung (công khai)

### `GET /api/vehicles`

Danh sách xe đang hiển thị (không có SĐT chủ xe).

| Query | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `category` | enum | không | Lọc theo loại xe |

Trả `200` — mảng `VehicleDto`:

```json
[
  {
    "id": 1,
    "name": "VinFast VF 5 Plus",
    "brand": "VinFast",
    "category": "Car4Seats",
    "categoryLabel": "Xe 4 chỗ",
    "seats": 5,
    "pricePerDay": 900000,
    "imageUrl": "https://…/vf5.jpg",
    "rangeKm": 326,
    "description": "Xe điện nhỏ gọn, phù hợp đi phố…",
    "highlights": ["Xe điện", "Cốp rộng"],
    "isAvailable": true,
    "images": ["https://…/vf5.jpg", "https://…/vf5-2.jpg"]
  }
]
```

### `GET /api/vehicles/{id}`

Chi tiết một xe. `404` (body rỗng) nếu không tồn tại hoặc đang ẩn.

### `GET /api/vehicles/{id}/busy`

Các khoảng thời gian xe đang bận (để vẽ lịch).

| Query | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `from` | datetime | hôm nay | Bắt đầu |
| `to` | datetime | `from` + 180 ngày | Kết thúc (tối đa 400 ngày kể từ `from`) |

`400` nếu khoảng không hợp lệ. Trả `200`:

```json
[
  { "from": "2026-10-23T08:00:00", "to": "2026-10-25T08:00:00", "kind": "booked" },
  { "from": "2026-11-01T00:00:00", "to": "2026-11-03T00:00:00", "kind": "blocked" }
]
```

`kind`: `"booked"` (đã có khách) hoặc `"blocked"` (chủ xe khoá).

### `GET /api/meta`

Dữ liệu chung cho giao diện.

```json
{
  "categories": [
    { "key": "Car4Seats", "label": "Xe 4 chỗ" },
    { "key": "Car7Seats", "label": "Xe 7 chỗ" },
    { "key": "ElectricBike", "label": "Xe máy điện" }
  ],
  "commissionRate": 0.10,
  "depositHoldMinutes": 30,
  "maxRentalDays": 30,
  "stats": { "vehicles": 12, "trips": 87, "depositPercent": 10 },
  "paymentMode": "bank"
}
```

`paymentMode`: `"bank"` (chuyển khoản thật SePay/Casso), `"payos"`, hoặc `"mock"` (giả lập, dùng khi demo/dev).

### `GET /`

Trả chuỗi `MoveGo API is running` (để kiểm tra server sống).

---

## 4. Đặt xe (khách)

### `GET /api/booking/available-vehicles`

Xe **còn trống** trong khoảng ngày khách chọn.

| Query | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `start` | datetime | có | Thời điểm nhận xe |
| `end` | datetime | có | Thời điểm trả xe (phải sau `start`) |
| `category` | enum | không | Lọc loại xe |

Ví dụ: `GET /api/booking/available-vehicles?start=2026-10-23T08:00:00&end=2026-10-25T08:00:00&category=Car4Seats`

- `200`: mảng `VehicleDto` (với `isAvailable = true`).
- `400`: thiếu ngày / `end <= start` / vượt số ngày tối đa.

### `POST /api/booking/quote`

Báo giá, **không tạo đơn**.

```json
{ "vehicleId": 1, "startDate": "2026-10-23T08:00:00", "endDate": "2026-10-25T08:00:00" }
```

`200`:

```json
{ "days": 2, "pricePerDay": 900000, "totalAmount": 1800000, "commissionAmount": 180000, "ownerAmount": 1620000 }
```

Lỗi: `400` (khoảng ngày sai / quá số ngày tối đa), `404` (không thấy xe).

### `POST /api/booking/create` — *rate limit `booking`*

Khách gửi yêu cầu thuê xe. Tạo đơn `PendingOwner`, gửi tin Zalo cho chủ xe.

| Trường | Kiểu | Bắt buộc | Quy tắc |
|---|---|---|---|
| `vehicleId` | int | có | |
| `customerName` | string | có | 2–100 ký tự |
| `customerPhone` | string | có | SĐT di động hợp lệ |
| `customerNote` | string | không | Ghi chú cho chủ xe |
| `pickupAddress` | string | có | 5–200 ký tự, địa chỉ khách muốn nhận xe |
| `startDate` | datetime | có | Không được ở quá khứ (cho lệch 5 phút) |
| `endDate` | datetime | có | Sau `startDate`, tối đa `maxRentalDays` ngày |

Trả **`201 Created`** (header `Location: /api/booking/{code}`) — body là `BookingTrackDto` (xem [`GET /api/booking/{code}`](#get-apibookingcode)), `status = "PendingOwner"`.

Lỗi:

| Mã | Khi nào |
|---|---|
| 400 | Thiếu/sai tên, SĐT, địa chỉ, ngày; thời gian nhận đã qua; quá số ngày tối đa |
| 404 | Không tìm thấy xe |
| 409 | Xe đã có lịch trong khoảng đó; hoặc bạn đã gửi yêu cầu cho xe này rồi |
| 429 | Quá nhiều yêu cầu chờ xác nhận, hoặc vượt rate limit |

### `POST /api/booking/lookup` — *rate limit `lookup`*

Khách quên mã đơn → tra bằng SĐT (dùng POST để SĐT không nằm trong URL/log).

```json
{ "phone": "0912 345 678" }
```

`200` — tối đa **10 đơn gần nhất trong 180 ngày**, **không** chứa thông tin liên hệ chủ xe:

```json
[
  {
    "code": "MG-EK9CFV",
    "status": "AwaitingDeposit",
    "statusText": "Chủ xe đã đồng ý — vui lòng đặt cọc",
    "vehicleName": "VinFast VF 5 Plus",
    "vehicleImageUrl": "https://…/vf5.jpg",
    "startDate": "2026-10-23T08:00:00",
    "endDate": "2026-10-25T08:00:00",
    "totalAmount": 1800000,
    "createdAt": "2026-10-13T09:15:42"
  }
]
```

Lỗi: `400` (SĐT sai định dạng), `429`.

### `GET /api/booking/{code}`

Trạng thái đơn theo mã (`MG-XXXXXX`, không phân biệt hoa/thường). Client nên **poll** (web: vài giây/lần; app: 8 giây, 3 giây khi `AwaitingDeposit`).

`200` — `BookingTrackDto`:

```json
{
  "code": "MG-EK9CFV",
  "status": "AwaitingDeposit",
  "statusText": "Chủ xe đã đồng ý — vui lòng đặt cọc",
  "vehicleName": "VinFast VF 5 Plus",
  "category": "Car4Seats",
  "categoryLabel": "Xe 4 chỗ",
  "vehicleImageUrl": "https://…/vf5.jpg",
  "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt",
  "startDate": "2026-10-23T08:00:00",
  "endDate": "2026-10-25T08:00:00",
  "rentalDays": 2,
  "totalAmount": 1800000,
  "depositAmount": 180000,
  "ownerAmount": 1620000,
  "customerName": "Nguyễn Văn A",
  "createdAt": "2026-10-13T09:15:42",
  "holdSecondsLeft": 1620,
  "checkoutUrl": "https://img.vietqr.io/image/970422-6650123456779-compact2.png?amount=180000&addInfo=MGEK9CFV&accountName=DINH%20LE%20VIET%20CUONG",
  "qrCode": "00020101021238…6304ABCD",
  "isMockPayment": false,
  "owner": null,
  "rejectReason": null,
  "bank": {
    "bankName": "MB Bank",
    "accountNumber": "6650123456779",
    "accountName": "DINH LE VIET CUONG",
    "amount": 180000,
    "content": "MGEK9CFV"
  }
}
```

Quy tắc hiển thị trường:

| Trường | Khi nào có giá trị |
|---|---|
| `holdSecondsLeft` | `AwaitingDeposit` (số giây còn giữ xe) |
| `checkoutUrl`, `qrCode` | `AwaitingDeposit`. Bank mode: `checkoutUrl` = ảnh VietQR chính thức, `qrCode` = chuỗi VietQR EMV (client tự vẽ QR dự phòng). Mock mode: URL trang giả lập |
| `bank` | `AwaitingDeposit` **và** `paymentMode = "bank"` |
| `isMockPayment` | `true` khi đang ở chế độ giả lập |
| `owner` (`{ name, phone, zaloLink }`) | **Chỉ** khi `Confirmed` hoặc `Completed` (đã cọc) |
| `rejectReason` | `Rejected` (nếu chủ xe có nhập lý do) |

Ví dụ `owner` sau khi cọc:

```json
"owner": { "name": "Anh Minh", "phone": "0987654321", "zaloLink": "https://zalo.me/0987654321" }
```

Lỗi: `404` — không tìm thấy đơn.

---

## 5. Chủ xe (xác thực bằng token trong link)

Chủ xe không có tài khoản; mỗi link chứa một token ngẫu nhiên:

- `request/{token}`: token riêng từng đơn (gửi qua Zalo khi có đơn mới).
- `manage/{manageToken}`: token riêng từng chủ xe (cổng quản lý lịch, do admin gửi khi onboard; admin có thể đổi bằng `reset-link`).

Token sai → `404` ("Link không hợp lệ hoặc đã hết hạn.").

### `GET /api/owner/request/{token}`

Chủ xe xem yêu cầu của khách. `200` — `OwnerRequestDto`:

```json
{
  "code": "MG-EK9CFV",
  "status": "PendingOwner",
  "statusText": "Đang chờ chủ xe xác nhận",
  "canRespond": true,
  "stillAvailable": true,
  "vehicleName": "VinFast VF 5 Plus",
  "category": "Car4Seats",
  "vehicleImageUrl": "https://…/vf5.jpg",
  "customerName": "Nguyễn Văn A",
  "customerNote": "Cho mình nhận xe sớm nếu được",
  "customerPhone": null,
  "startDate": "2026-10-23T08:00:00",
  "endDate": "2026-10-25T08:00:00",
  "rentalDays": 2,
  "totalAmount": 1800000,
  "ownerAmount": 1620000,
  "commissionAmount": 180000,
  "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt",
  "depositHoldMinutes": 30,
  "manageToken": "k3J…(token cổng quản lý)"
}
```

- `customerPhone` **chỉ có sau khi khách đã cọc** (`Confirmed`/`Completed`).
- `stillAvailable = false` nghĩa là xe đã bị đơn khác chiếm trong khoảng này (không thể nhận).
- `manageToken` để trang xác nhận có nút quay về cổng quản lý.

### `POST /api/owner/request/{token}`

Chủ xe chấp nhận / từ chối.

```json
{ "accept": true }
```
```json
{ "accept": false, "reason": "Xe đang bảo dưỡng" }
```

- Chấp nhận → đơn chuyển `AwaitingDeposit`, tạo QR/link cọc, bắt đầu giữ xe `depositHoldMinutes` phút.
- Từ chối → `Rejected` (lưu `reason`).
- `200` — `OwnerRequestDto` mới.
- Lỗi: `404` token sai; `409` đơn đã được xử lý trước đó, hoặc xe đã có lịch (đơn tự đóng); `410` yêu cầu đã hết hạn; `502` không tạo được link thanh toán.

### `GET /api/owner/manage/{manageToken}`

Bảng điều khiển của chủ xe. `200` — `OwnerDashboardDto`:

```json
{
  "ownerName": "Anh Minh",
  "vehicles": [
    {
      "id": 1,
      "name": "VinFast VF 5 Plus",
      "pricePerDay": 900000,
      "isActive": true,
      "blocks": [
        { "id": 5, "startDate": "2026-11-01T00:00:00", "endDate": "2026-11-03T00:00:00", "reason": "Đi công tác" }
      ],
      "bookings": [
        {
          "code": "MG-EK9CFV",
          "status": "PendingOwner",
          "statusText": "Đang chờ chủ xe xác nhận",
          "customerName": "Nguyễn Văn A",
          "customerPhone": null,
          "startDate": "2026-10-23T08:00:00",
          "endDate": "2026-10-25T08:00:00",
          "ownerAmount": 1620000,
          "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt",
          "confirmToken": "Qx9…"
        }
      ]
    }
  ]
}
```

`confirmToken` (nếu có) dùng để mở trang `/owner/request/{confirmToken}` xử lý đơn đang chờ. `customerPhone` chỉ có khi đã cọc.

### `POST /api/owner/manage/{manageToken}/vehicles/{vehicleId}/blocks`

Khoá lịch (xe bận, ví dụ đi xa, bảo dưỡng).

```json
{ "startDate": "2026-11-01T00:00:00", "endDate": "2026-11-03T00:00:00", "reason": "Đi công tác" }
```

`200` — `OwnerBlockDto` `{ "id": 5, "startDate": "…", "endDate": "…", "reason": "Đi công tác" }`.

Lỗi: `400` (ngày không hợp lệ, đã qua, hoặc dài quá 1 năm); `404` (không phải xe của bạn); `409` (khoảng này đã có khách đặt — message nêu mã đơn).

### `DELETE /api/owner/manage/{manageToken}/blocks/{blockId}`

Gỡ khoá lịch. `200` — `true`. `404` nếu không tìm thấy.

### `POST /api/owner/manage/{manageToken}/vehicles/{vehicleId}/active?value=true|false`

Hiện/ẩn xe khỏi website. `200` — `true`. `404` nếu không phải xe của bạn.

---

## 6. Thanh toán & webhook

Chế độ thanh toán được chọn theo cấu hình, ưu tiên: **BankPayment** (chuyển khoản thật) → **PayOS** → **Mock**.

### `POST /api/payments/mock/{code}/complete`

Giả lập "khách đã cọc" → đơn chuyển `Confirmed`. **Chỉ hoạt động khi** không bật PayOS/Bank **và** (môi trường Development **hoặc** `Business:AllowMockPayment = true`); ngược lại trả `404`.

`200` — `BookingTrackDto` (đã `Confirmed`, kèm `owner`). Lỗi: `404`, `409` (đơn không ở bước chờ cọc / xe không còn trống).

### `POST /api/payments/bank-webhook`  *(chuyển khoản ngân hàng)*

Webhook "tiền về tài khoản" của **SePay** hoặc **Casso**. Cấu hình ở trang SePay/Casso, xem [`THANH_TOAN_NGAN_HANG.md`](THANH_TOAN_NGAN_HANG.md).

**Xác thực** (một trong ba, so sánh an toàn thời gian-hằng):

| Header | Dùng bởi |
|---|---|
| `Authorization: Apikey <WebhookSecret>` | SePay |
| `secure-token: <WebhookSecret>` | Casso |
| `X-Webhook-Secret: <WebhookSecret>` | Tự gọi / kiểm thử |

Khi `BankPayment:Enabled = false` hoặc chưa có `WebhookSecret` → `404`. Sai khoá → `401 { "success": false, "error": 1 }`.

**Body SePay** (1 giao dịch phẳng):

```json
{
  "id": 92704,
  "gateway": "MBBank",
  "transactionDate": "2026-10-13 10:21:05",
  "accountNumber": "6650123456779",
  "content": "MGEK9CFV",
  "transferType": "in",
  "transferAmount": 180000,
  "referenceCode": "FT26286xxxx"
}
```

**Body Casso** (`data` là object hoặc mảng):

```json
{ "error": 0, "data": [ { "id": 123, "description": "MGEK9CFV chuyen tien", "amount": 180000 } ] }
```

**Quy tắc xử lý:**

- Chỉ nhận **tiền vào** (`transferType = "in"` / `amount > 0`); SePay: `accountNumber` phải khớp tài khoản đã cấu hình.
- Tìm mã đơn trong nội dung: bỏ ký tự không phải chữ/số, viết hoa, tìm `MG` + 6 ký tự hợp lệ.
- Số tiền phải **≥ tiền cọc**. Thiếu → không xác nhận, ghi chú vào đơn cho admin.
- Đơn đang `AwaitingDeposit` → `Confirmed`. Đơn đã `Expired` mà tiền về muộn: nếu xe còn trống thì vẫn xác nhận, nếu không thì ghi `AdminNote` "CẦN HOÀN CỌC".
- **Idempotent**: gửi lại cùng giao dịch không gây tác dụng phụ.
- Với khoá hợp lệ luôn trả `200 { "success": true, "error": 0 }` (kể cả giao dịch không liên quan) để cổng không gửi lại mãi. SePay yêu cầu 200/201 + `{"success": true}` và sẽ thử lại tối đa 7 lần nếu lỗi.

### `POST /api/payments/payos-webhook`  *(PayOS, nếu dùng)*

PayOS gọi khi khách thanh toán. Body theo định dạng PayOS (`data.code`, `data.orderCode`, `signature`…). Chữ ký được kiểm bằng Checksum Key.

- Sai chữ ký → `400 { "success": false }`.
- Hợp lệ → luôn `200 { "success": true }`; nếu `data.code == "00"` thì đơn tương ứng `orderCode` được xác nhận.

---

## 7. Quản trị (Admin)

Mọi endpoint yêu cầu header:

```
X-Admin-Key: <Business:AdminKey>
```

Sai khoá → `401`. Chưa cấu hình `Business:AdminKey` (Production) → `503` ("Admin API đang tắt…").

### Chủ xe

| Method | Path | Mô tả |
|---|---|---|
| GET | `/api/admin/owners` | Danh sách chủ xe kèm link quản lý lịch |
| POST | `/api/admin/owners` | Thêm chủ xe |
| PUT | `/api/admin/owners/{id}` | Sửa chủ xe |
| POST | `/api/admin/owners/{id}/reset-link` | Cấp link quản lý mới (link cũ vô hiệu) |

**Body** (`OwnerInput`) cho POST/PUT:

```json
{ "name": "Anh Minh", "phoneNumber": "0987654321", "zaloPhone": "0987654321" }
```

`zaloPhone` có thể bỏ trống (dùng `phoneNumber`). **Response** `AdminOwnerDto`:

```json
{
  "id": 3,
  "name": "Anh Minh",
  "phoneNumber": "0987654321",
  "zaloPhone": "0987654321",
  "manageLink": "https://movego.example/owner/manage/k3J…",
  "vehicleCount": 2,
  "createdAt": "2026-10-01T10:00:00"
}
```

Lỗi: `400` (tên trống/quá 120 ký tự, SĐT/Zalo sai), `404` (không thấy chủ xe), `409` (SĐT đã thuộc chủ xe khác).

### Xe

| Method | Path | Mô tả |
|---|---|---|
| GET | `/api/admin/vehicles` | Toàn bộ xe (kể cả đang ẩn) |
| GET | `/api/admin/vehicles/{id}` | Một xe |
| POST | `/api/admin/vehicles` | Thêm xe |
| PUT | `/api/admin/vehicles/{id}` | Sửa xe |
| PUT | `/api/admin/vehicles/{id}/active` | Ẩn/hiện xe — body `{ "isActive": false }` |
| DELETE | `/api/admin/vehicles/{id}` | Xoá xe |

**Body** (`VehicleInput`) cho POST/PUT:

```json
{
  "ownerId": 3,
  "name": "VinFast VF 5 Plus",
  "brand": "VinFast",
  "category": "Car4Seats",
  "seats": 5,
  "pricePerDay": 900000,
  "pickupAddress": "Hẻm 12 Nguyễn Văn Trỗi, Đà Lạt",
  "description": "Xe điện nhỏ gọn…",
  "imageUrl": "https://…/vf5.jpg",
  "imageUrls": ["https://…/vf5.jpg", "https://…/vf5-2.jpg"],
  "rangeKm": 326,
  "highlights": ["Xe điện", "Cốp rộng"],
  "isActive": true
}
```

Quy tắc: `pricePerDay` từ **50.000đ đến 50.000.000đ**, là bội số của 1.000; `highlights` tối đa 6 mục; `imageUrls` (nếu có) dùng thay `imageUrl`, ảnh đầu là ảnh bìa; `pickupAddress` chỉ để admin ghi nhớ.

**Response** `AdminVehicleDto`:

```json
{
  "id": 1, "ownerId": 3, "ownerName": "Anh Minh",
  "name": "VinFast VF 5 Plus", "brand": "VinFast",
  "category": "Car4Seats", "categoryLabel": "Xe 4 chỗ", "seats": 5, "pricePerDay": 900000,
  "pickupAddress": "Hẻm 12 Nguyễn Văn Trỗi, Đà Lạt", "description": "Xe điện nhỏ gọn…",
  "imageUrl": "https://…/vf5.jpg", "rangeKm": 326,
  "highlights": ["Xe điện", "Cốp rộng"], "isActive": true,
  "activeBookings": 1, "images": ["https://…/vf5.jpg", "https://…/vf5-2.jpg"]
}
```

`DELETE` trả `200` với `true` (xoá hẳn) hoặc `false` (chỉ đánh dấu đã xoá vì xe có lịch sử đơn). `409` nếu xe đang có đơn chờ xử lý/đã xác nhận (hãy ẩn xe và xoá sau). Lỗi khác: `400` (dữ liệu sai), `404`.

### Lịch từng xe

| Method | Path | Mô tả |
|---|---|---|
| GET | `/api/admin/schedule` | Lịch của **tất cả** xe |
| GET | `/api/admin/vehicles/{id}/schedule` | Lịch của một xe. Tuỳ chọn `?from=&to=` (datetime) để xem một khoảng, ví dụ cả một tháng; `400` nếu `to <= from`, `404` nếu không có xe |

Dùng cho trang Admin → Xe: xe đang cho thuê hay trống, ngày nào có khách, ngày nào chủ xe khoá. Mỗi xe có `entries` gồm 90 ngày gần nhất và toàn bộ lịch sắp tới; đơn bị từ chối/huỷ/hết hạn không hiện vì không chiếm xe.

```json
{
  "vehicleId": 1,
  "vehicleName": "VinFast VF 3",
  "isActive": true,
  "rentedNow": true,
  "rentedUntil": "2026-10-15T08:00:00",
  "nextBusyFrom": "2026-10-19T08:00:00",
  "tripsDone": 0,
  "upcomingCount": 1,
  "pendingCount": 1,
  "entries": [
    {
      "kind": "booking",
      "phase": "now",
      "bookingId": 7,
      "code": "MG-EPAN7F",
      "status": "Confirmed",
      "label": "Đã cọc — khách thuê",
      "customerName": "Nguyễn Văn A",
      "customerPhone": "0912345678",
      "startDate": "2026-10-12T08:00:00",
      "endDate": "2026-10-15T08:00:00",
      "days": 3,
      "totalAmount": 1300000,
      "depositAmount": 130000,
      "ownerAmount": 1170000,
      "holdExpiresAt": null,
      "note": null
    },
    {
      "kind": "block",
      "phase": "upcoming",
      "bookingId": null, "code": null, "status": null,
      "label": "Chủ xe khoá: Bảo dưỡng định kỳ",
      "customerName": null, "customerPhone": null,
      "startDate": "2026-10-18T00:00:00",
      "endDate": "2026-10-20T23:00:00",
      "days": 3,
      "totalAmount": null, "depositAmount": null, "ownerAmount": null,
      "holdExpiresAt": null, "note": null
    }
  ]
}
```

- `kind`: `"booking"` (đơn của khách) hoặc `"block"` (chủ xe khoá).
- `phase`: `"past"` (đã qua), `"now"` (đang diễn ra), `"upcoming"` (sắp tới).
- `status` của đơn có thể là `Confirmed`, `Completed`, `AwaitingDeposit` (đang giữ chỗ, còn hạn) hoặc `PendingOwner` (chờ chủ xe, **chưa chiếm xe**).
- `rentedNow = true` chỉ khi có đơn **đã cọc** đang diễn ra; đơn chờ chủ xe/chờ cọc không tính là đang thuê.
- `nextBusyFrom`: ngày bắt đầu của lịch bận kế tiếp (đơn đã cọc, đang giữ chỗ hoặc chủ xe khoá).

### Đơn

| Method | Path | Mô tả |
|---|---|---|
| GET | `/api/admin/summary` | Tổng quan |
| GET | `/api/admin/bookings?status=` | 200 đơn mới nhất, lọc theo trạng thái |
| POST | `/api/admin/bookings/{id}/confirm-payment` | Xác nhận thủ công "đã nhận cọc" |
| POST | `/api/admin/bookings/{id}/resend-owner` | Gửi lại tin Zalo cho chủ xe |

**`GET /api/admin/summary`** (`200`):

```json
{
  "byStatus": { "PendingOwner": 2, "AwaitingDeposit": 1, "Confirmed": 5, "Expired": 3 },
  "commissionEarned": 900000,
  "needRefund": 0,
  "ownerNotNotified": 0
}
```

- `commissionEarned`: tổng hoa hồng của đơn `Confirmed` + `Completed`.
- `needRefund`: số đơn có `adminNote` (ví dụ cọc thiếu / cọc muộn cần hoàn).
- `ownerNotNotified`: số đơn `PendingOwner` mà tin Zalo chưa gửi được.

**`GET /api/admin/bookings`** — `status` tuỳ chọn (xem bảng enum). Mỗi phần tử:

```json
{
  "id": 41,
  "code": "MG-EK9CFV",
  "status": "Confirmed",
  "vehicle": "VinFast VF 5 Plus",
  "owner": "Anh Minh",
  "ownerPhone": "0987654321",
  "customerName": "Nguyễn Văn A",
  "customerPhone": "0912345678",
  "pickupAddress": "12 Trần Hưng Đạo, Đà Lạt",
  "startDate": "2026-10-23T08:00:00",
  "endDate": "2026-10-25T08:00:00",
  "rentalDays": 2,
  "totalAmount": 1800000,
  "commissionAmount": 180000,
  "ownerAmount": 1620000,
  "createdAt": "2026-10-13T09:15:42",
  "ownerNotifiedAt": "2026-10-13T09:15:44",
  "ownerRespondedAt": "2026-10-13T09:20:10",
  "paidAt": "2026-10-13T09:25:31",
  "adminNote": null
}
```

**`POST /api/admin/bookings/{id}/confirm-payment`** — dùng khi webhook chưa chạy nhưng bạn đã thấy tiền trong tài khoản. Chuyển đơn `AwaitingDeposit` → `Confirmed`. `200` — `BookingTrackDto`; `404` không thấy đơn; `409` đơn không ở bước chờ cọc / xe không còn trống.

**`POST /api/admin/bookings/{id}/resend-owner`** — gửi lại tin Zalo (chỉ với đơn `PendingOwner`). `200` — `true` nếu gửi được, `false` nếu Zalo vẫn lỗi; `404`; `409` đơn không còn chờ chủ xe.

---

## 8. Bảng tra nhanh

| # | Method | Đường dẫn | Xác thực | Ghi chú |
|---|---|---|---|---|
| 1 | GET | `/` | – | Kiểm tra API sống |
| 2 | GET | `/api/meta` | – | Loại xe, tỉ lệ cọc, thống kê, `paymentMode` |
| 3 | GET | `/api/vehicles` | – | `?category=` |
| 4 | GET | `/api/vehicles/{id}` | – | |
| 5 | GET | `/api/vehicles/{id}/busy` | – | `?from=&to=` |
| 6 | GET | `/api/booking/available-vehicles` | – | `?start=&end=&category=` |
| 7 | POST | `/api/booking/quote` | – | Báo giá |
| 8 | POST | `/api/booking/create` | – | Rate limit 10/phút/IP → 201 |
| 9 | POST | `/api/booking/lookup` | – | Rate limit 8/phút/IP |
| 10 | GET | `/api/booking/{code}` | – | Theo dõi đơn |
| 11 | GET | `/api/owner/request/{token}` | token | Chủ xe xem yêu cầu |
| 12 | POST | `/api/owner/request/{token}` | token | Chấp nhận / từ chối |
| 13 | GET | `/api/owner/manage/{manageToken}` | token | Bảng điều khiển |
| 14 | POST | `/api/owner/manage/{manageToken}/vehicles/{vehicleId}/blocks` | token | Khoá lịch |
| 15 | DELETE | `/api/owner/manage/{manageToken}/blocks/{blockId}` | token | Gỡ khoá |
| 16 | POST | `/api/owner/manage/{manageToken}/vehicles/{vehicleId}/active?value=` | token | Ẩn/hiện xe |
| 17 | POST | `/api/payments/mock/{code}/complete` | – | Chỉ dev / `AllowMockPayment` |
| 18 | POST | `/api/payments/bank-webhook` | khoá webhook | SePay / Casso |
| 19 | POST | `/api/payments/payos-webhook` | chữ ký PayOS | PayOS |
| 20 | GET | `/api/admin/owners` | X-Admin-Key | |
| 21 | POST | `/api/admin/owners` | X-Admin-Key | |
| 22 | PUT | `/api/admin/owners/{id}` | X-Admin-Key | |
| 23 | POST | `/api/admin/owners/{id}/reset-link` | X-Admin-Key | |
| 24 | GET | `/api/admin/vehicles` | X-Admin-Key | |
| 25 | GET | `/api/admin/vehicles/{id}` | X-Admin-Key | |
| 26 | POST | `/api/admin/vehicles` | X-Admin-Key | |
| 27 | PUT | `/api/admin/vehicles/{id}` | X-Admin-Key | |
| 28 | PUT | `/api/admin/vehicles/{id}/active` | X-Admin-Key | |
| 29 | DELETE | `/api/admin/vehicles/{id}` | X-Admin-Key | |
| 30 | GET | `/api/admin/summary` | X-Admin-Key | |
| 31 | GET | `/api/admin/bookings` | X-Admin-Key | `?status=` |
| 32 | POST | `/api/admin/bookings/{id}/confirm-payment` | X-Admin-Key | |
| 33 | POST | `/api/admin/bookings/{id}/resend-owner` | X-Admin-Key | |
| 34 | GET | `/api/admin/schedule` | X-Admin-Key | Lịch của tất cả xe |
| 35 | GET | `/api/admin/vehicles/{id}/schedule` | X-Admin-Key | Lịch của một xe |

Tổng cộng **35 endpoint** (+ Swagger `/swagger` khi chạy Development).

> Lưu ý bảo mật: SĐT chủ xe chỉ xuất hiện trong `BookingTrackDto.owner` **sau khi khách đã cọc**; SĐT khách chỉ xuất hiện ở phía chủ xe sau khi đã cọc; mọi lỗi đều trả về ProblemDetails với thông điệp tiếng Việt.
