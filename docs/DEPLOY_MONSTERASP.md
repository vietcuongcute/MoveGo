# Đưa MoveGo lên MonsterASP.NET (gói Free, 1 website)

Gói Free chỉ có **1 website**, nên MoveGo được build ở chế độ **gộp**: API chạy ngay trong website (cùng domain, không cần cổng/site thứ hai).
Chạy dev trên máy vẫn như cũ (2 dự án riêng).

> Gói Free không có HTTPS → địa chỉ demo là `http://ten-site.runasp.net`.

## 1. Chuẩn bị database PostgreSQL (chọn 1 trong 2)

**Cách A – PostgreSQL ngay trong MonsterASP (ưu tiên nếu panel cho tạo ở gói của bạn)**
Panel → *Databases* → tạo database kiểu **PostgreSQL** → ghi lại host, tên DB, user, mật khẩu. Chuỗi kết nối:

```
Host=<host trong panel>;Port=5432;Database=<db>;Username=<user>;Password=<mật khẩu>
```
(Xem mục *Connect* của PostgreSQL trong help.monsterasp.net nếu panel yêu cầu thêm SSL: thêm `;SSL Mode=Require;Trust Server Certificate=true`.)

**Cách B – Neon (neon.tech, miễn phí)**
Tạo project → ở nút *Connect* **tắt "Pooled connection"** (cần kết nối trực tiếp để chạy migration) → copy chuỗi dạng
`postgresql://user:pass@ep-xxx.neon.tech/neondb?sslmode=require` và dán **nguyên văn** vào cấu hình (app tự hiểu dạng này).
Lưu ý: nếu host MonsterASP chặn cổng ra 5432 thì Neon không kết nối được → dùng Cách A.

## 2. Sửa `appsettings.Production.json` (trong thư mục gói `MoveGo/`)

```json
{
  "ConnectionStrings": { "Default": "<chuỗi kết nối ở bước 1>" },
  "Business": {
    "AdminKey": "<mật khẩu admin bạn tự đặt, dài và khó đoán>",
    "WebBaseUrl": "http://<ten-site>.runasp.net"
  },
  "Seed": { "Enabled": true }
}
```
- `AdminKey` để trống thì trang admin bị khoá.
- `Seed.Enabled = true` nạp 4 chủ xe + 10 xe mẫu ở lần chạy đầu (số điện thoại chủ xe là số giả).

## 3. Upload
1. Panel → *Websites* → website của bạn → chọn phiên bản **.NET 8 hoặc mới hơn**.
2. Dừng website (Stop), upload **toàn bộ nội dung** thư mục `MoveGo/` (không phải cả thư mục cha) vào thư mục gốc của site bằng FTP/SFTP (FileZilla) hoặc File Manager. Phải thấy `web.config` và `MoveGo.Web.dll` nằm ngay thư mục gốc.
3. Start website.

## 4. Kiểm tra
- `http://<ten-site>.runasp.net/healthz` → hiện `ok` (hiện `db-error` = sai chuỗi kết nối hoặc DB chặn kết nối; xem `logs/stdout_*.log`).
- Trang chủ, `/xe` hiện 10 xe; `/admin/login` đăng nhập bằng `AdminKey`.
- Lỗi 500.30 / 500.31: sai phiên bản .NET trong panel hoặc thiếu file; đọc `logs/stdout_*.log` (tắt `stdoutLogEnabled` trong `web.config` sau khi xong).

## 5. Chạy kịch bản demo
1. Khách: `/xe` → chọn xe + ngày + địa chỉ nhận xe → Gửi yêu cầu → ghi lại **mã đơn** (MG-XXXXXX).
2. Chủ xe (Zalo chưa nối nên không có tin nhắn): vào **Admin → Chủ xe**, copy **link quản lý** của chủ xe sở hữu xe đó, mở ở tab khác → đơn đang chờ → **Trả lời → Chấp nhận**.
3. Khách: `/tra-cuu` nhập mã đơn **hoặc số điện thoại** đã đặt → trang theo dõi → bấm **"Giả lập đã thanh toán"** (chế độ thử nghiệm, chưa có PayOS thật) → hiện số điện thoại chủ xe.
4. Admin → *Tổng quan & đơn* thấy hoa hồng 10%.
5. Ảnh xe: Admin → **Xe** → Sửa → tải ảnh lên (ảnh được lưu trong `wwwroot/uploads/vehicles` trên host, nên không mất khi restart).

## Lưu ý
- Mở site trước giờ demo 1–2 phút để khởi động và nối DB (lần đầu chậm hơn).
- Thanh toán PayOS và tin Zalo vẫn là bản giả lập (không có tiền/tin thật).
- Đổi `AdminKey` khác mặc định dev (`movego-admin-dev`) trước khi để site công khai.

## Build lại gói trên máy bạn
```bash
dotnet publish MoveGo.Web -c Release -p:EmbedApi=true -o publish
```
rồi bỏ `package*.json`, `appsettings.Development.json` khỏi thư mục `publish/` và thêm `appsettings.Production.json` như trên.
