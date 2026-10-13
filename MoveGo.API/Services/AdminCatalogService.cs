using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>
/// Quản trị danh mục: thêm/sửa chủ xe và xe. Xoá xe: xoá hẳn nếu chưa từng có đơn, ngược lại chỉ đánh dấu đã xoá để giữ lịch sử đơn.
/// Đổi giá chỉ ảnh hưởng đơn mới: đơn cũ đã lưu số tiền tại thời điểm đặt.
/// </summary>
public class AdminCatalogService(AppDbContext db, IClock clock, IOptions<BusinessOptions> biz)
{
    private const string DefaultCity = "Đà Lạt";

    private static readonly BookingStatus[] LiveStatuses =
        { BookingStatus.PendingOwner, BookingStatus.AwaitingDeposit, BookingStatus.Confirmed };

    private string ManageLink(Owner o) => $"{biz.Value.WebBaseUrl.TrimEnd('/')}/owner/manage/{o.ManageToken}";

    private AdminOwnerDto ToDto(Owner o, int vehicleCount) =>
        new(o.Id, o.Name, o.PhoneNumber, o.ZaloPhone, ManageLink(o), vehicleCount, o.CreatedAt);

    private static AdminVehicleDto ToDto(Vehicle v, int activeBookings) => new(
        v.Id, v.OwnerId, v.Owner?.Name ?? "", v.Name, v.Brand,
        v.Category, VehicleMapper.CategoryLabel(v.Category), v.Seats, v.PricePerDay,
        v.PickupAddress, v.Description, v.ImageUrl, v.RangeKm,
        v.Highlights.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        v.IsActive, activeBookings, VehicleMapper.Images(v));

    // ---------- Chủ xe ----------

    public async Task<List<AdminOwnerDto>> ListOwnersAsync(CancellationToken ct)
    {
        var owners = await db.Owners.AsNoTracking().OrderBy(o => o.Id).ToListAsync(ct);
        var counts = await db.Vehicles.AsNoTracking().Where(v => !v.IsDeleted).GroupBy(v => v.OwnerId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return owners.Select(o => ToDto(o, counts.GetValueOrDefault(o.Id))).ToList();
    }

    public async Task<ServiceResult<AdminOwnerDto>> CreateOwnerAsync(OwnerInput input, CancellationToken ct)
    {
        var parsed = ParseOwner(input);
        if (parsed.Error is not null) return ServiceResult<AdminOwnerDto>.Fail(400, parsed.Error);
        var (name, phone, zalo) = parsed.Value;

        if (await db.Owners.AnyAsync(o => o.PhoneNumber == phone, ct))
            return ServiceResult<AdminOwnerDto>.Fail(409, "Số điện thoại này đã là của một chủ xe khác.");

        var owner = new Owner
        {
            Name = name, PhoneNumber = phone, ZaloPhone = zalo,
            ManageToken = Tokens.UrlToken(), CreatedAt = clock.Now
        };
        db.Owners.Add(owner);
        await db.SaveChangesAsync(ct);
        return ServiceResult<AdminOwnerDto>.Success(ToDto(owner, 0));
    }

    public async Task<ServiceResult<AdminOwnerDto>> UpdateOwnerAsync(int id, OwnerInput input, CancellationToken ct)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (owner is null) return ServiceResult<AdminOwnerDto>.Fail(404, "Không tìm thấy chủ xe.");

        var parsed = ParseOwner(input);
        if (parsed.Error is not null) return ServiceResult<AdminOwnerDto>.Fail(400, parsed.Error);
        var (name, phone, zalo) = parsed.Value;

        if (await db.Owners.AnyAsync(o => o.PhoneNumber == phone && o.Id != id, ct))
            return ServiceResult<AdminOwnerDto>.Fail(409, "Số điện thoại này đã là của một chủ xe khác.");

        owner.Name = name; owner.PhoneNumber = phone; owner.ZaloPhone = zalo;
        await db.SaveChangesAsync(ct);
        return ServiceResult<AdminOwnerDto>.Success(ToDto(owner, await db.Vehicles.CountAsync(v => v.OwnerId == id && !v.IsDeleted, ct)));
    }

    /// <summary>Đổi link quản lý lịch khi chủ xe lộ link: link cũ vô hiệu ngay.</summary>
    public async Task<ServiceResult<AdminOwnerDto>> ResetOwnerTokenAsync(int id, CancellationToken ct)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (owner is null) return ServiceResult<AdminOwnerDto>.Fail(404, "Không tìm thấy chủ xe.");
        owner.ManageToken = Tokens.UrlToken();
        await db.SaveChangesAsync(ct);
        return ServiceResult<AdminOwnerDto>.Success(ToDto(owner, await db.Vehicles.CountAsync(v => v.OwnerId == id && !v.IsDeleted, ct)));
    }

    private static ServiceResult<(string Name, string Phone, string Zalo)> ParseOwner(OwnerInput input)
    {
        static ServiceResult<(string, string, string)> Bad(string m) => ServiceResult<(string, string, string)>.Fail(400, m);

        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return Bad("Vui lòng nhập tên chủ xe.");
        if (name.Length > 120) return Bad("Tên chủ xe tối đa 120 ký tự.");

        var phone = PhoneNumbers.Normalize(input.PhoneNumber);
        if (phone is null) return Bad("Số điện thoại chủ xe không hợp lệ (cần 10 số, ví dụ 0912345678).");

        var zalo = phone;
        if (!string.IsNullOrWhiteSpace(input.ZaloPhone))
        {
            zalo = PhoneNumbers.Normalize(input.ZaloPhone)!;
            if (zalo is null) return Bad("Số Zalo không hợp lệ.");
        }
        return ServiceResult<(string, string, string)>.Success((name, phone, zalo));
    }

    // ---------- Xe ----------

    public async Task<List<AdminVehicleDto>> ListVehiclesAsync(CancellationToken ct)
    {
        var vehicles = await db.Vehicles.AsNoTracking().Include(v => v.Owner).Where(v => !v.IsDeleted)
            .OrderBy(v => v.Id).ToListAsync(ct);
        var live = await LiveCountsAsync(ct);
        return vehicles.Select(v => ToDto(v, live.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<ServiceResult<AdminVehicleDto>> GetVehicleAsync(int id, CancellationToken ct)
    {
        var v = await db.Vehicles.AsNoTracking().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (v is null) return ServiceResult<AdminVehicleDto>.Fail(404, "Không tìm thấy xe.");
        return ServiceResult<AdminVehicleDto>.Success(ToDto(v, (await LiveCountsAsync(ct)).GetValueOrDefault(id)));
    }

    public async Task<ServiceResult<AdminVehicleDto>> CreateVehicleAsync(VehicleInput input, CancellationToken ct)
    {
        var vehicle = new Vehicle();
        var error = await ApplyAsync(vehicle, input, isNew: true, ct);
        if (error is not null) return ServiceResult<AdminVehicleDto>.Fail(400, error);

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return await GetVehicleAsync(vehicle.Id, ct);
    }

    public async Task<ServiceResult<AdminVehicleDto>> UpdateVehicleAsync(int id, VehicleInput input, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (vehicle is null) return ServiceResult<AdminVehicleDto>.Fail(404, "Không tìm thấy xe.");

        var error = await ApplyAsync(vehicle, input, isNew: false, ct);
        if (error is not null) return ServiceResult<AdminVehicleDto>.Fail(400, error);

        await db.SaveChangesAsync(ct);
        return await GetVehicleAsync(id, ct);
    }

    /// <summary>Ẩn/hiện xe trên web. Đơn đang chạy vẫn giữ nguyên, chỉ không nhận thêm khách mới.</summary>
    public async Task<ServiceResult<AdminVehicleDto>> SetActiveAsync(int id, bool isActive, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (vehicle is null) return ServiceResult<AdminVehicleDto>.Fail(404, "Không tìm thấy xe.");
        vehicle.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return await GetVehicleAsync(id, ct);
    }

    /// <summary>
    /// Xoá xe. Còn đơn đang chạy thì từ chối. Chưa từng có đơn: xoá hẳn khỏi DB (kèm lịch khoá).
    /// Đã có đơn cũ: chỉ đánh dấu IsDeleted + ẩn, để báo cáo hoa hồng và lịch sử đơn vẫn đúng.
    /// Trả về true nếu đã xoá hẳn (khi đó nơi gọi có thể dọn file ảnh).
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteVehicleAsync(int id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.Include(v => v.Unavailabilities)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (vehicle is null) return ServiceResult<bool>.Fail(404, "Không tìm thấy xe.");

        if (await db.Bookings.AnyAsync(b => b.VehicleId == id && LiveStatuses.Contains(b.Status), ct))
            return ServiceResult<bool>.Fail(409, "Xe đang có đơn chờ xử lý hoặc đã xác nhận nên chưa thể xoá. Hãy ẩn xe và xoá sau khi các đơn này hoàn tất.");

        var hasHistory = await db.Bookings.AnyAsync(b => b.VehicleId == id, ct);
        if (hasHistory)
        {
            vehicle.IsDeleted = true;
            vehicle.IsActive = false;
            db.VehicleUnavailabilities.RemoveRange(vehicle.Unavailabilities);
        }
        else db.Vehicles.Remove(vehicle);

        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(!hasHistory);
    }

    // ---------- Lịch từng xe ----------

    /// <summary>
    /// Lịch cho trang quản trị: với mỗi xe, các đơn đã cọc/đang giữ chỗ/đang chờ chủ xe và các khoảng chủ xe khoá.
    /// Mặc định lấy lịch sử 90 ngày gần đây và toàn bộ lịch sắp tới; truyền from/to để xem một khoảng khác (ví dụ một tháng cụ thể). Đơn bị từ chối/huỷ/hết hạn không hiện (không chiếm xe).
    /// </summary>
    public async Task<List<AdminVehicleScheduleDto>> GetSchedulesAsync(int? vehicleId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var now = clock.Now;
        var since = from ?? now.AddDays(-90);
        var until = to ?? DateTime.MaxValue;

        var vehicles = await db.Vehicles.AsNoTracking().Where(v => !v.IsDeleted && (vehicleId == null || v.Id == vehicleId))
            .OrderBy(v => v.Id).ToListAsync(ct);
        var ids = vehicles.Select(v => v.Id).ToList();

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.VehicleId) && b.EndDate >= since && b.StartDate < until
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed
                    || b.Status == BookingStatus.PendingOwner
                    || (b.Status == BookingStatus.AwaitingDeposit && (b.HoldExpiresAt == null || b.HoldExpiresAt > now))))
            .ToListAsync(ct);
        var blocks = await db.VehicleUnavailabilities.AsNoTracking()
            .Where(u => ids.Contains(u.VehicleId) && u.EndDate >= since && u.StartDate < until).ToListAsync(ct);

        static string PhaseOf(DateTime start, DateTime end, DateTime now) =>
            end <= now ? "past" : start <= now ? "now" : "upcoming";

        var result = new List<AdminVehicleScheduleDto>();
        foreach (var v in vehicles)
        {
            var entries = new List<AdminScheduleEntryDto>();

            foreach (var b in bookings.Where(b => b.VehicleId == v.Id))
            {
                var label = b.Status switch
                {
                    BookingStatus.Confirmed or BookingStatus.Completed => "Đã cọc — khách thuê",
                    BookingStatus.AwaitingDeposit => "Đang giữ chỗ, chờ khách cọc",
                    _ => "Chờ chủ xe xác nhận (chưa chiếm xe)"
                };
                entries.Add(new("booking", PhaseOf(b.StartDate, b.EndDate, now), b.Id, b.Code, b.Status, label,
                    b.CustomerName, b.CustomerPhone, b.StartDate, b.EndDate, b.RentalDays,
                    b.TotalAmount, b.CommissionAmount, b.OwnerAmount, b.HoldExpiresAt, b.AdminNote));
            }
            foreach (var u in blocks.Where(u => u.VehicleId == v.Id))
            {
                entries.Add(new("block", PhaseOf(u.StartDate, u.EndDate, now), null, null, null,
                    string.IsNullOrWhiteSpace(u.Reason) ? "Chủ xe khoá lịch" : "Chủ xe khoá: " + u.Reason,
                    null, null, u.StartDate, u.EndDate,
                    Math.Max(1, (int)Math.Ceiling((u.EndDate - u.StartDate).TotalDays)), null, null, null, null, null));
            }

            entries = entries.OrderBy(e => e.StartDate).ThenBy(e => e.EndDate).ToList();

            // "Đang cho thuê" = có đơn ĐÃ CỌC đang diễn ra; đơn chờ chủ xe/chờ cọc không tính là đang thuê.
            var current = entries.Where(e => e.Kind == "booking" && e.Phase == "now"
                && e.Status is BookingStatus.Confirmed or BookingStatus.Completed).ToList();
            var busy = entries.Where(e => e.Phase != "past" && (e.Kind == "block"
                || e.Status is BookingStatus.Confirmed or BookingStatus.AwaitingDeposit)).ToList();

            result.Add(new AdminVehicleScheduleDto(
                v.Id, v.Name, v.IsActive,
                RentedNow: current.Count > 0,
                RentedUntil: current.Count > 0 ? current.Max(e => e.EndDate) : null,
                NextBusyFrom: busy.Where(e => e.Phase == "upcoming").Select(e => (DateTime?)e.StartDate).Min(),
                TripsDone: entries.Count(e => e.Kind == "booking" && e.Phase == "past"
                    && e.Status is BookingStatus.Confirmed or BookingStatus.Completed),
                UpcomingCount: entries.Count(e => e.Kind == "booking" && e.Phase == "upcoming"
                    && e.Status is BookingStatus.Confirmed or BookingStatus.AwaitingDeposit),
                PendingCount: entries.Count(e => e.Status == BookingStatus.PendingOwner && e.Phase != "past"),
                Entries: entries));
        }
        return result;
    }

    private async Task<Dictionary<int, int>> LiveCountsAsync(CancellationToken ct) =>
        await db.Bookings.AsNoTracking().Where(b => LiveStatuses.Contains(b.Status))
            .GroupBy(b => b.VehicleId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    /// <summary>Kiểm tra dữ liệu rồi gán vào xe. Trả về thông báo lỗi tiếng Việt hoặc null nếu hợp lệ.</summary>
    private async Task<string?> ApplyAsync(Vehicle v, VehicleInput i, bool isNew, CancellationToken ct)
    {
        var name = i.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return "Vui lòng nhập tên xe.";
        if (name.Length > 120) return "Tên xe tối đa 120 ký tự.";

        var brand = i.Brand?.Trim() ?? "";
        if (brand.Length is 0 or > 60) return "Hãng xe là bắt buộc, tối đa 60 ký tự.";

        if (i.Category is not { } category || !Enum.IsDefined(category)) return "Vui lòng chọn loại xe.";

        if (i.Seats is not { } seats || seats is < 1 or > 16) return "Số chỗ ngồi phải từ 1 đến 16.";

        if (i.PricePerDay is not { } price || price < 50_000m || price > 50_000_000m)
            return "Giá thuê một ngày phải từ 50.000đ đến 50.000.000đ.";
        if (price % 1000m != 0) return "Giá thuê nên tròn đến nghìn đồng (ví dụ 650000).";

        var address = i.PickupAddress?.Trim() ?? "";
        if (address.Length > 200) return "Nơi để xe tối đa 200 ký tự.";

        var description = i.Description?.Trim() ?? "";
        if (description.Length > 1000) return "Mô tả tối đa 1000 ký tự.";

        // Bộ ảnh: ưu tiên ImageUrls (ảnh đầu là ảnh bìa); không có thì dùng ImageUrl đơn lẻ (tương thích bản cũ).
        var images = (i.ImageUrls ?? (string.IsNullOrWhiteSpace(i.ImageUrl) ? [] : [i.ImageUrl]))
            .Select(x => x?.Trim() ?? "").Where(x => x.Length > 0).Distinct().ToList();
        if (images.Count > VehicleMapper.MaxImages) return $"Mỗi xe tối đa {VehicleMapper.MaxImages} ảnh.";
        foreach (var image in images)
        {
            if (image.Length > 500) return "Link ảnh tối đa 500 ký tự.";
            if (image.Contains('|')) return "Link ảnh không được chứa ký tự |.";
            var okImage = image.StartsWith('/') && !image.StartsWith("//")
                          || Uri.TryCreate(image, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
            if (!okImage) return "Link ảnh phải bắt đầu bằng https:// (hoặc để trống để dùng hình minh hoạ).";
        }
        var gallery = string.Join("|", images);
        if (gallery.Length > 2000) return "Danh sách ảnh quá dài, hãy bớt ảnh hoặc dùng link ngắn hơn.";

        int? range = i.RangeKm;
        if (category == VehicleCategory.ElectricBike)
        {
            if (range is not null && (range < 1 || range > 1000)) return "Quãng đường mỗi lần sạc phải từ 1 đến 1000 km.";
        }
        else range = null;

        var highlights = (i.Highlights ?? new()).Select(h => h.Replace("|", " ").Trim())
            .Where(h => h.Length > 0).ToList();
        if (highlights.Count > 6) return "Tối đa 6 điểm nổi bật.";
        var joined = string.Join("|", highlights);
        if (joined.Length > 300) return "Các điểm nổi bật quá dài (tối đa 300 ký tự tính chung).";

        if (isNew || i.OwnerId is not null)
        {
            if (i.OwnerId is not { } ownerId) return "Vui lòng chọn chủ xe.";
            if (!await db.Owners.AnyAsync(o => o.Id == ownerId, ct)) return "Không tìm thấy chủ xe đã chọn.";
            v.OwnerId = ownerId;
        }

        v.Name = name; v.Brand = brand; v.Category = category; v.Seats = seats; v.PricePerDay = price;
        // Hiện chỉ cho thuê ở Đà Lạt; khách tự nhập địa chỉ nhận xe khi đặt.
        v.PickupLocation = DefaultCity; v.PickupAddress = address; v.Description = description;
        v.ImageUrl = images.FirstOrDefault(); v.Gallery = gallery; v.RangeKm = range; v.Highlights = joined;
        if (i.IsActive is { } active) v.IsActive = active;
        return null;
    }
}
