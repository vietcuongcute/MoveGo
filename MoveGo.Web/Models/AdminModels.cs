namespace MoveGo.Web.Models;

// Phản chiếu JSON của /api/admin trong MoveGo.API.

public record AdminOwnerDto(
    int Id, string Name, string PhoneNumber, string ZaloPhone, string ManageLink, int VehicleCount, DateTime CreatedAt);

public record AdminVehicleDto(
    int Id, int OwnerId, string OwnerName, string Name, string Brand,
    VehicleCategory Category, string CategoryLabel, int Seats, decimal PricePerDay,
    string PickupAddress, string Description, string? ImageUrl,
    int? RangeKm, string[] Highlights, bool IsActive, int ActiveBookings, string[]? Images = null);

public record AdminSummaryDto(Dictionary<string, int> ByStatus, decimal CommissionEarned, int NeedRefund, int OwnerNotNotified);

public record AdminBookingDto(
    int Id, string Code, string Status, string Vehicle, string Owner, string OwnerPhone,
    string CustomerName, string CustomerPhone, string PickupAddress, DateTime StartDate, DateTime EndDate, int RentalDays,
    decimal TotalAmount, decimal CommissionAmount, decimal OwnerAmount,
    DateTime CreatedAt, DateTime? OwnerNotifiedAt, DateTime? OwnerRespondedAt, DateTime? PaidAt, string? AdminNote);

public class AdminOverviewViewModel
{
    public AdminSummaryDto Summary { get; set; } = new([], 0, 0, 0);
    public List<AdminBookingDto> Bookings { get; set; } = [];
    public string? StatusFilter { get; set; }
}

/// <summary>Các ô trong form thêm/sửa xe. Mọi trường đều nullable để API là nơi kiểm tra duy nhất.</summary>
public class VehicleForm
{
    public int Id { get; set; }
    public int? OwnerId { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public VehicleCategory Category { get; set; } = VehicleCategory.Car4Seats;
    public int? Seats { get; set; } = 4;
    public decimal? PricePerDay { get; set; }
    public string? PickupAddress { get; set; }
    public string? Description { get; set; }
    /// <summary>Bộ ảnh hiện có (ảnh đầu tiên là ảnh bìa). Mỗi ảnh là một input ẩn tên Images.</summary>
    public List<string> Images { get; set; } = [];
    /// <summary>Link ảnh dán thêm (không bắt buộc).</summary>
    public string? NewImageUrl { get; set; }
    public int? RangeKm { get; set; }
    /// <summary>Mỗi dòng một điểm nổi bật.</summary>
    public string? HighlightsText { get; set; }
    public bool IsActive { get; set; } = true;

    public List<AdminOwnerDto> Owners { get; set; } = [];
    public string? Error { get; set; }

    public bool IsNew => Id == 0;

    public static VehicleForm From(AdminVehicleDto v) => new()
    {
        Id = v.Id, OwnerId = v.OwnerId, Name = v.Name, Brand = v.Brand, Category = v.Category, Seats = v.Seats,
        PricePerDay = v.PricePerDay, PickupAddress = v.PickupAddress,
        Description = v.Description, Images = (v.Images is { Length: > 0 } ? v.Images : string.IsNullOrWhiteSpace(v.ImageUrl) ? [] : [v.ImageUrl]).ToList(), RangeKm = v.RangeKm,
        HighlightsText = string.Join("\n", v.Highlights), IsActive = v.IsActive
    };
}

public class OwnerForm
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ZaloPhone { get; set; }
}

public class AdminOwnersViewModel
{
    public List<AdminOwnerDto> Owners { get; set; } = [];
    public OwnerForm Form { get; set; } = new();
    public string? Error { get; set; }
}
