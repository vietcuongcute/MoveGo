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
    public string? PickupLocation { get; set; }
    public string? PickupAddress { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
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
    string PickupLocation, string PickupAddress, string Description, string? ImageUrl,
    int? RangeKm, string[] Highlights, bool IsActive, int ActiveBookings);
