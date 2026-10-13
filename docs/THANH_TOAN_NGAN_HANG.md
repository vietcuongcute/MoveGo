# Nhận cọc bằng QR ngân hàng của bạn (tự nhận diện tiền về)

Khách quét **mã VietQR** (số tiền + nội dung điền sẵn) → tiền vào **tài khoản của bạn** → dịch vụ **SePay** (hoặc **Casso**) thấy biến động số dư và gọi **webhook** về MoveGo → đơn **tự chuyển "Đã xác nhận"** và hai bên nhận số điện thoại của nhau. Không cần PayOS, không cần trung gian giữ tiền.

```
Khách quét QR ─► Ngân hàng ─► tài khoản của bạn
                                   │ (SePay/Casso theo dõi số dư)
                                   ▼
                POST /api/payments/bank-webhook  (kèm khoá bí mật)
                                   ▼
        MoveGo đọc nội dung "MGxxxxxx" + kiểm tra đủ tiền → Đã xác nhận
```
Mã QR do MoveGo tự sinh (chuẩn VietQR/NAPAS) từ **số tài khoản của bạn**, nên không cần tải ảnh QR lên: ảnh QR tĩnh không mang được *số tiền* và *mã đơn*, mà hai thứ đó là cái giúp hệ thống biết "ai vừa trả cho đơn nào".

## 1. Cấu hình MoveGo (3 phút)
Trong `appsettings.Production.json` (host) hoặc `MoveGo.API/appsettings.Development.json` (chạy máy bạn):

```json
"BankPayment": {
  "Enabled": true,
  "BankBin": "970422",
  "BankName": "MB Bank",
  "AccountNumber": "0123456789",
  "AccountName": "NGUYEN VAN A",
  "WebhookSecret": "<chuỗi bí mật bạn tự đặt, ví dụ 32 ký tự ngẫu nhiên>"
}
```
- `AccountName`: đúng tên chủ tài khoản, **in hoa, không dấu**.
- `WebhookSecret`: tự đặt, và dán **y hệt** vào SePay ở bước 2. Để trống thì webhook bị từ chối hoàn toàn.
- Khi bật `BankPayment`, các cấu hình `PayOs` bị bỏ qua.

Mã BIN thường gặp: MB `970422` · Vietcombank `970436` · Techcombank `970407` · ACB `970416` · BIDV `970418` · VietinBank `970415` · VPBank `970432` · TPBank `970423` · Sacombank `970403` · Agribank `970405` · OCB `970448` · VIB `970441` · MSB `970426`. Bank khác: tra "mã BIN ngân hàng" hoặc xem https://api.vietqr.io/v2/banks.

## 2. Nối SePay (miễn phí gói cơ bản, giới hạn số giao dịch/tháng — xem sepay.vn)
1. Đăng ký tại https://my.sepay.vn → **Ngân hàng → Thêm tài khoản** → làm theo hướng dẫn liên kết tài khoản của bạn (tuỳ ngân hàng: xác thực OTP/app ngân hàng).
2. Vào **Tích hợp WebHooks → Thêm webhooks**:
   - **Gọi đến URL**: `https://<tên-miền-của-bạn>/api/payments/bank-webhook`
   - **Kiểu chứng thực**: `API Key` → nhập đúng `WebhookSecret` ở bước 1 (SePay sẽ gửi header `Authorization: Apikey <khoá>`).
   - **Sự kiện**: *Có tiền vào*. Chọn đúng tài khoản ngân hàng ở bước 1.
3. Bấm **Kiểm tra** (nếu có) hoặc tự chuyển 1 khoản nhỏ để thử.

> **Casso** (casso.vn) cũng dùng được: tạo webhook, URL như trên, header bảo mật `secure-token` = `WebhookSecret`. MoveGo tự nhận ra định dạng của SePay hay Casso.

## 3. Webhook cần địa chỉ công khai
SePay/Casso gọi từ internet nên **`localhost` không nhận được**. Có 2 cách:
- **Đã up host (MonsterASP)**: dùng luôn `http(s)://<ten-site>.runasp.net/api/payments/bank-webhook`. Gói Free của MonsterASP không có HTTPS; nếu SePay từ chối URL `http://` thì dùng cách dưới.
- **Demo từ máy bạn**: mở đường hầm HTTPS, ví dụ `cloudflared tunnel --url http://localhost:5100` (hoặc `ngrok http 5100`) → lấy link `https://xxxx.trycloudflare.com` làm URL webhook: `https://xxxx.trycloudflare.com/api/payments/bank-webhook`.

## 4. Cách hệ thống nhận diện & chống gian lận
- Nội dung chuyển khoản là **`MG` + 6 ký tự mã đơn** (vd `MGK7P2QX`); nằm sẵn trong QR. Ngân hàng đổi chữ thường, bỏ dấu gạch, thêm chữ khác vẫn nhận ra.
- Chỉ nhận **tiền vào**, đúng **số tài khoản của bạn**, **đủ tiền cọc** (thừa thì vẫn nhận). Chuyển thiếu → đơn **không** xác nhận, admin thấy ghi chú "CHUYỂN THIẾU".
- Webhook sai khoá → `401`. Webhook gửi lặp → bỏ qua, không xác nhận hai lần.
- Khách chuyển **sau khi hết hạn giữ chỗ**: nếu xe còn trống đơn vẫn được xác nhận, nếu không thì admin thấy ghi chú **CẦN HOÀN TIỀN**.
- Mỗi giao dịch được ghi vào **Ghi chú** của đơn trong Admin (kèm mã giao dịch của cổng).

## 5. Phương án dự phòng khi demo
Webhook chưa chạy mà khách đã chuyển? Mở **Admin → Tổng quan & đơn**, ở đơn *Chờ đặt cọc* bấm **"Đã nhận cọc"** (sau khi tự kiểm tra tiền trong app ngân hàng) → đơn xác nhận ngay. Cũng có API `POST /api/admin/bookings/{id}/confirm-payment` (header `X-Admin-Key`).

Chưa có tài khoản SePay kịp? Đặt `BankPayment.Enabled=false` và `Business.AllowMockPayment=true` để dùng nút **"Giả lập đã thanh toán"** (không có tiền thật; nhớ tắt khi chạy thật).

## 6. Thử trên máy bạn không cần SePay
Giả lập "tiền về" bằng `curl` (thay mã đơn, khoá và số tiền cho đúng):
```bash
curl -X POST http://localhost:5100/api/payments/bank-webhook \
  -H "Authorization: Apikey <WebhookSecret>" -H "Content-Type: application/json" \
  -d '{"id":1,"gateway":"MBBank","accountNumber":"0123456789","content":"MGK7P2QX","transferType":"in","transferAmount":65000}'
```
