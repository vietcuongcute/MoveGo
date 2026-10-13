# MoveGo Mobile (iOS demo)

App khách hàng của MoveGo viết bằng **Expo / React Native (TypeScript)**, gọi thẳng `MoveGo.API`.
Chạy trên iPhone qua app **Expo Go** — không cần tài khoản Apple Developer, không cần Xcode.

Có 3 tab: **Xe** (danh sách + lọc loại xe) → **Chi tiết xe** (lịch bận, chọn ngày, báo giá, form đặt) ·
**Tra cứu** (mã đơn hoặc SĐT, đơn gần đây) → **Đơn** (tiến trình, đếm ngược giữ xe, đặt cọc, SĐT chủ xe) ·
**Cài đặt** (địa chỉ API).

---

## Cài vào iPhone — 5 bước

### Bước 0 — Chuẩn bị (làm 1 lần)
| Trên Mac | Trên iPhone |
|---|---|
| Cài Node.js 20+ (`brew install node` hoặc tải ở nodejs.org) | Tải **Expo Go** trên App Store (miễn phí) |
| Đã chạy được API như README gốc (.NET 8 + Docker) | iPhone và Mac **cùng một Wi‑Fi** |

> **Tài khoản Expo (miễn phí):** Expo Go bản mới bắt buộc đăng nhập, nếu không sẽ báo *"You need to be signed in to Expo Go and Expo CLI"*. Tạo tài khoản ở https://expo.dev/signup, chạy `npx expo login` trên Mac và đăng nhập **cùng tài khoản** trong Expo Go (tab Settings → Log in).

> Expo Go phải là bản mới nhất (project dùng Expo SDK 57). Nếu app báo "SDK không tương thích" → cập nhật Expo Go trên App Store.

### Bước 1 — Chạy API cho điện thoại gọi được
iPhone không hiểu `localhost` (đó là chính iPhone), nên API phải lắng nghe trên mạng LAN:

```bash
docker compose up -d                        # PostgreSQL (nếu chưa chạy)
cd MoveGo.API
dotnet run --urls http://0.0.0.0:5100       # thay vì dotnet run thường
```
Lần đầu macOS hỏi *"Allow incoming network connections?"* → bấm **Allow**.

Lấy IP của Mac:
```bash
ipconfig getifaddr en0        # vd 192.168.1.23   (nếu rỗng thử en1)
```
Kiểm tra: mở Safari **trên iPhone** vào `http://192.168.1.23:5100/api/vehicles` — thấy JSON danh sách xe là OK.

> Đã up website lên host (MonsterASP)? Khỏi cần bước này — dùng luôn địa chỉ host làm địa chỉ API (vd `http://ten-ban.runasp.net`).

### Bước 2 — Chạy app
```bash
cd MoveGo.Mobile
npm install
npx expo start
```
Terminal hiện một **mã QR**.

### Bước 3 — Mở trên iPhone
Mở app **Camera** của iPhone → quét QR → bấm thông báo "Open in Expo Go". Lần đầu app tải bundle mất ~10–30 giây.

### Bước 4 — Trỏ app tới API
Vào tab **Cài đặt** → nhập `http://192.168.1.23:5100` (IP Mac của bạn) → **Lưu và kiểm tra kết nối**. Thấy ✅ là xong, sang tab **Xe** kéo xuống để tải lại.

(Muốn khỏi nhập: sửa `DEFAULT_API_URL` trong `src/config.ts` thành địa chỉ của bạn — dùng khi app chưa lưu địa chỉ nào.)

---

## Kịch bản demo (≈ 3 phút)
1. Tab **Xe** → chọn *VinFast VF 3* → chọn số ngày → xem báo giá → nhập tên, SĐT, **địa chỉ nhận xe ở Đà Lạt** → **Gửi yêu cầu**.
2. Màn hình đơn hiện "⏳ Chờ chủ xe". Lúc này đóng vai chủ xe: mở `http://localhost:5200/admin` (khoá `movego-admin-dev`) hoặc xem terminal đang chạy API (tin Zalo giả lập in ra **link của chủ xe**) → mở link → **Đồng ý**.
3. Trên iPhone, đơn tự chuyển sang "💳 Chờ đặt cọc" (tự cập nhật ~8 giây, hoặc kéo xuống) với đồng hồ giữ xe 30 phút.
4. Bấm **Thanh toán cọc (giả lập)** → trạng thái **Đã xác nhận**, hiện **tên + SĐT chủ xe**, nút **Gọi** / **Nhắn Zalo**; 90% còn lại trả trực tiếp cho chủ xe.
5. Tab **Tra cứu** → nhập SĐT vừa đặt → ra danh sách đơn.

## Xử lý sự cố
| Hiện tượng | Cách xử lý |
|---|---|
| "Không kết nối được máy chủ" | Sai IP/cổng, khác Wi‑Fi, hoặc API đang chạy bằng `localhost`. Chạy lại với `--urls http://0.0.0.0:5100`, thử mở link ở Safari iPhone trước. |
| Safari iPhone cũng không vào được | Tắt VPN; Wi‑Fi công ty/trường thường chặn thiết bị nói chuyện với nhau → dùng hotspot 4G của iPhone cho cả Mac, hoặc dùng địa chỉ host MonsterASP. |
| Quét QR không mở được | Dùng `npx expo start --tunnel` (cần mạng internet; lần đầu hỏi cài `@expo/ngrok`). Lưu ý tunnel chỉ đưa *app* qua mạng, còn API vẫn cần đúng địa chỉ ở Cài đặt. |
| "You need to be signed in to Expo Go and Expo CLI" | Chạy `npx expo login` trên Mac và đăng nhập cùng tài khoản trong Expo Go, rồi bấm Try again. |
| "Project is incompatible with this version of Expo Go" | Cập nhật Expo Go trong App Store. |
| Xe không có ảnh, hiện biểu tượng 🚗 | Bình thường — xe chưa có ảnh. Thêm ảnh trong Admin của website, app tự hiển thị. |
| Sửa code nhưng app không đổi | Lắc iPhone → **Reload**, hoặc ở terminal bấm `r`. |

## Giới hạn của bản demo
- Chạy trong **Expo Go**, chưa phải app cài độc lập trên App Store/TestFlight (cái đó cần tài khoản Apple Developer 99$/năm + `eas build`).
- Tin Zalo là **giả lập**. Thanh toán: nếu API bật `BankPayment` (xem `docs/THANH_TOAN_NGAN_HANG.md`) app hiện **mã QR + số tài khoản** thật và tự xác nhận trong ~3 giây sau khi tiền về; nếu chưa bật thì dùng nút "giả lập" (chỉ ở môi trường Development).
- Chủ xe/admin dùng website, app này chỉ dành cho khách thuê.

## Cấu trúc
```
App.tsx                 thanh tab + ngăn xếp màn hình
src/api.ts              gọi API, kiểu dữ liệu, lỗi tiếng Việt, lưu địa chỉ API + đơn gần đây
src/screens/            Vehicles · VehicleDetail · Lookup · Order · Settings
src/ui.tsx, theme.ts    thành phần giao diện dùng chung
```
