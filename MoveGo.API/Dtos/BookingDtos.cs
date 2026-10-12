using MoveGo.API.Models;

namespace MoveGo.API.Dtos;

public class CreateBookingRequest
{
    public int VehicleId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerNote { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public record OwnerContactDto(string Name, string Phone, string ZaloLink);

/// <summary>
/// Trạng thái đơn dành cho KHÁCH.
/// SĐT chủ xe và địa chỉ nhận xe cụ thể CHỈ có khi đã cọc (Confirmed/Completed), còn lại luôn là null.
/// </summary>
public record BookingTrackDto(
    string Code, BookingStatus Status, string StatusText,
    string VehicleName, VehicleCategory Category, string CategoryLabel, string? VehicleImageUrl,
    string PickupLocation, string? PickupAddress,
    DateTime StartDate, DateTime EndDate, int RentalDays,
    decimal TotalAmount, decimal DepositAmount, decimal OwnerAmount,
    string CustomerName, DateTime CreatedAt,
    int? HoldSecondsLeft,
    OwnerContactDto? Owner, string? RejectReason);
