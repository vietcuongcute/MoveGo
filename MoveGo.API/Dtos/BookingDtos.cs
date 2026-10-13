using MoveGo.API.Models;

namespace MoveGo.API.Dtos;

public class CreateBookingRequest
{
    public int VehicleId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerNote { get; set; }
    /// <summary>Địa chỉ khách muốn nhận xe (bắt buộc).</summary>
    public string? PickupAddress { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public record OwnerContactDto(string Name, string Phone, string ZaloLink);

/// <summary>Trạng thái đơn dành cho KHÁCH. SĐT chủ xe chỉ có khi đã cọc (Confirmed/Completed). PickupAddress là địa chỉ chính khách đã nhập.</summary>
public record BookingTrackDto(
    string Code, BookingStatus Status, string StatusText,
    string VehicleName, VehicleCategory Category, string CategoryLabel, string? VehicleImageUrl,
    string PickupAddress,
    DateTime StartDate, DateTime EndDate, int RentalDays,
    decimal TotalAmount, decimal DepositAmount, decimal OwnerAmount,
    string CustomerName, DateTime CreatedAt,
    int? HoldSecondsLeft,
    string? CheckoutUrl, string? QrCode, bool IsMockPayment,
    OwnerContactDto? Owner, string? RejectReason);

/// <summary>Thông tin đơn dành cho CHỦ XE (qua link Zalo). Không có SĐT khách cho tới khi khách cọc.</summary>
public record OwnerRequestDto(
    string Code, BookingStatus Status, string StatusText, bool CanRespond, bool StillAvailable,
    string VehicleName, VehicleCategory Category, string? VehicleImageUrl,
    string CustomerName, string? CustomerNote, string? CustomerPhone,
    DateTime StartDate, DateTime EndDate, int RentalDays,
    decimal TotalAmount, decimal OwnerAmount, decimal CommissionAmount,
    string PickupAddress, int DepositHoldMinutes);

public class OwnerRespondRequest
{
    public bool Accept { get; set; }
    public string? Reason { get; set; }
}

// ---------- Cổng chủ xe: quản lý lịch ----------
public record OwnerBlockDto(int Id, DateTime StartDate, DateTime EndDate, string Reason);

public record OwnerBookingDto(
    string Code, BookingStatus Status, string StatusText, string CustomerName, string? CustomerPhone,
    DateTime StartDate, DateTime EndDate, decimal OwnerAmount, string PickupAddress, string? ConfirmToken);

public record OwnerVehicleDto(
    int Id, string Name, decimal PricePerDay, bool IsActive,
    List<OwnerBlockDto> Blocks, List<OwnerBookingDto> Bookings);

public record OwnerDashboardDto(string OwnerName, List<OwnerVehicleDto> Vehicles);

public class CreateBlockRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Reason { get; set; }
}


/// <summary>Khách tra cứu bằng số điện thoại đã dùng khi đặt.</summary>
public class PhoneLookupRequest
{
    public string? Phone { get; set; }
}

/// <summary>Dòng tóm tắt một đơn trong kết quả tra cứu theo SĐT (cố ý KHÔNG có thông tin liên hệ chủ xe).</summary>
public record BookingSummaryDto(
    string Code, BookingStatus Status, string StatusText, string VehicleName, string? VehicleImageUrl,
    DateTime StartDate, DateTime EndDate, decimal TotalAmount, DateTime CreatedAt);
