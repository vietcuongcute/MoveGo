using MoveGo.API.Models;

namespace MoveGo.API.Dtos;

public class OwnerInput
{
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    /// <summary>SĐT Zalo nhận tin. Để trống thì dùng luôn SĐT chính.</summary>
    public string? ZaloPhone { get; set; }
}

public class VehicleInput
{
    public int? OwnerId { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public VehicleCategory? Category { get; set; }
    public int? Seats { get; set; }
    public decimal? PricePerDay { get; set; }
    /// <summary>Nơi chủ xe thường để xe (không bắt buộc, chỉ để admin ghi nhớ).</summary>
    public string? PickupAddress { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>Bộ ảnh (ảnh đầu tiên là ảnh bìa). Nếu có thì dùng thay cho ImageUrl.</summary>
    public List<string>? ImageUrls { get; set; }
    public int? RangeKm { get; set; }
    /// <summary>Mỗi điểm nổi bật một phần tử (tối đa 6).</summary>
    public List<string>? Highlights { get; set; }
    public bool? IsActive { get; set; }
}

public class SetActiveRequest
{
    public bool IsActive { get; set; }
}

public record AdminOwnerDto(
    int Id, string Name, string PhoneNumber, string ZaloPhone, string ManageLink, int VehicleCount, DateTime CreatedAt);

/// <summary>Bản dành cho quản trị: có cả địa chỉ nhận xe cụ thể và xe đang ẩn.</summary>
public record AdminVehicleDto(
    int Id, int OwnerId, string OwnerName, string Name, string Brand,
    VehicleCategory Category, string CategoryLabel, int Seats, decimal PricePerDay,
    string PickupAddress, string Description, string? ImageUrl,
    int? RangeKm, string[] Highlights, bool IsActive, int ActiveBookings, string[] Images);

/// <summary>Một dòng trong lịch của xe: đơn của khách hoặc khoảng chủ xe tự khoá.</summary>
/// <param name="Kind">"booking" (đơn của khách) hoặc "block" (chủ xe khoá).</param>
/// <param name="Phase">"past" (đã qua), "now" (đang diễn ra), "upcoming" (sắp tới).</param>
public record AdminScheduleEntryDto(
    string Kind, string Phase, int? BookingId, string? Code, BookingStatus? Status, string Label,
    string? CustomerName, string? CustomerPhone, DateTime StartDate, DateTime EndDate, int Days,
    decimal? TotalAmount, decimal? DepositAmount, decimal? OwnerAmount, DateTime? HoldExpiresAt, string? Note);

/// <summary>Lịch của một xe cho trang quản trị: đang cho thuê hay trống, ngày bận, ngày chủ xe khoá.</summary>
public record AdminVehicleScheduleDto(
    int VehicleId, string VehicleName, bool IsActive, bool RentedNow, DateTime? RentedUntil,
    DateTime? NextBusyFrom, int TripsDone, int UpcomingCount, int PendingCount,
    List<AdminScheduleEntryDto> Entries);
