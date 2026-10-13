using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>
/// Quản trị danh mục: thêm/sửa chủ xe và xe. Không xoá hẳn (đơn cũ còn tham chiếu), chỉ ẩn xe khỏi web.
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
        v.IsActive, activeBookings);

    // ---------- Chủ xe ----------

    public async Task<List<AdminOwnerDto>> ListOwnersAsync(CancellationToken ct)
    {
        var owners = await db.Owners.AsNoTracking().OrderBy(o => o.Id).ToListAsync(ct);
        var counts = await db.Vehicles.AsNoTracking().GroupBy(v => v.OwnerId)
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
        return ServiceResult<AdminOwnerDto>.Success(ToDto(owner, await db.Vehicles.CountAsync(v => v.OwnerId == id, ct)));
    }

    /// <summary>Đổi link quản lý lịch khi chủ xe lộ link: link cũ vô hiệu ngay.</summary>
    public async Task<ServiceResult<AdminOwnerDto>> ResetOwnerTokenAsync(int id, CancellationToken ct)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (owner is null) return ServiceResult<AdminOwnerDto>.Fail(404, "Không tìm thấy chủ xe.");
        owner.ManageToken = Tokens.UrlToken();
        await db.SaveChangesAsync(ct);
        return ServiceResult<AdminOwnerDto>.Success(ToDto(owner, await db.Vehicles.CountAsync(v => v.OwnerId == id, ct)));
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
        var vehicles = await db.Vehicles.AsNoTracking().Include(v => v.Owner)
            .OrderBy(v => v.Id).ToListAsync(ct);
        var live = await LiveCountsAsync(ct);
        return vehicles.Select(v => ToDto(v, live.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<ServiceResult<AdminVehicleDto>> GetVehicleAsync(int id, CancellationToken ct)
    {
        var v = await db.Vehicles.AsNoTracking().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id, ct);
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
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null) return ServiceResult<AdminVehicleDto>.Fail(404, "Không tìm thấy xe.");

        var error = await ApplyAsync(vehicle, input, isNew: false, ct);
        if (error is not null) return ServiceResult<AdminVehicleDto>.Fail(400, error);

        await db.SaveChangesAsync(ct);
        return await GetVehicleAsync(id, ct);
    }

    /// <summary>Ẩn/hiện xe trên web. Đơn đang chạy vẫn giữ nguyên, chỉ không nhận thêm khách mới.</summary>
    public async Task<ServiceResult<AdminVehicleDto>> SetActiveAsync(int id, bool isActive, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null) return ServiceResult<AdminVehicleDto>.Fail(404, "Không tìm thấy xe.");
        vehicle.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return await GetVehicleAsync(id, ct);
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

        string? image = string.IsNullOrWhiteSpace(i.ImageUrl) ? null : i.ImageUrl.Trim();
        if (image is not null)
        {
            if (image.Length > 500) return "Link ảnh tối đa 500 ký tự.";
            var okImage = image.StartsWith('/') && !image.StartsWith("//")
                          || Uri.TryCreate(image, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
            if (!okImage) return "Link ảnh phải bắt đầu bằng https:// (hoặc để trống để dùng hình minh hoạ).";
        }

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
        v.ImageUrl = image; v.RangeKm = range; v.Highlights = joined;
        if (i.IsActive is { } active) v.IsActive = active;
        return null;
    }
}
