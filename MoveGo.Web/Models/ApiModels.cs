namespace MoveGo.Web.Models;

// Các model này phản chiếu JSON mà MoveGo.API trả về (đặt tên theo DTO bên API).
// Dự án nhỏ nên copy sang đây; dự án lớn có thể tách ra project dùng chung MoveGo.Contracts.

public enum VehicleCategory { Car4Seats = 1, Car7Seats = 2, ElectricBike = 3 }

public enum BookingStatus { PendingOwner, AwaitingDeposit, Confirmed, Rejected, Cancelled, Expired, Completed }

public record VehicleDto(
    int Id, string Name, string Brand, VehicleCategory Category, string CategoryLabel, int Seats,
    decimal PricePerDay, string? ImageUrl, int? RangeKm,
    string Description, string[] Highlights, bool IsAvailable, string[]? Images = null);

public record StatsDto(int Vehicles, int Trips, int DepositPercent);
public record CategoryDto(VehicleCategory Key, string Label);

public record MetaDto(
    List<CategoryDto> Categories,
    decimal CommissionRate, int DepositHoldMinutes, int MaxRentalDays, StatsDto Stats)
{
    public static MetaDto Empty => new([], 0.10m, 30, 30, new StatsDto(0, 0, 10));
}

public record BookingTrackDto(
    string Code, BookingStatus Status, string StatusText,
    string VehicleName, VehicleCategory Category, string CategoryLabel, string? VehicleImageUrl,
    string PickupAddress,
    DateTime StartDate, DateTime EndDate, int RentalDays,
    decimal TotalAmount, decimal DepositAmount, decimal OwnerAmount,
    string CustomerName, DateTime CreatedAt,
    int? HoldSecondsLeft,
    string? CheckoutUrl, string? QrCode, bool IsMockPayment,
    string? OwnerName, string? RejectReason,
    BankTransferInfoDto? Bank = null);

public record BankTransferInfoDto(string BankName, string AccountNumber, string AccountName, long Amount, string Content);

public record BookingSummaryDto(
    string Code, BookingStatus Status, string StatusText, string VehicleName, string? VehicleImageUrl,
    DateTime StartDate, DateTime EndDate, decimal TotalAmount, DateTime CreatedAt);

public record OwnerRequestDto(
    string Code, BookingStatus Status, string StatusText, bool CanRespond, bool StillAvailable,
    string VehicleName, VehicleCategory Category, string? VehicleImageUrl,
    string CustomerName, string? CustomerNote, string? CustomerPhone,
    DateTime StartDate, DateTime EndDate, int RentalDays,
    decimal TotalAmount, decimal OwnerAmount, decimal CommissionAmount,
    string PickupAddress, int DepositHoldMinutes,
    string? ManageToken = null,
    string? CustomerZaloLink = null);

public record OwnerBlockDto(int Id, DateTime StartDate, DateTime EndDate, string Reason);

public record OwnerBookingDto(
    string Code, BookingStatus Status, string StatusText, string CustomerName, string? CustomerPhone,
    DateTime StartDate, DateTime EndDate, decimal OwnerAmount, string PickupAddress, string? DetailToken);

public record OwnerVehicleDto(
    int Id, string Name, decimal PricePerDay, bool IsActive,
    List<OwnerBlockDto> Blocks, List<OwnerBookingDto> Bookings);

public record OwnerDashboardDto(string OwnerName, List<OwnerVehicleDto> Vehicles);
