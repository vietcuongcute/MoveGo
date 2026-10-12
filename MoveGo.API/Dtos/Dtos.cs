using MoveGo.API.Models;

namespace MoveGo.API.Dtos;

/// <summary>Thông tin xe trả cho giao diện. KHÔNG có SĐT chủ xe và địa chỉ nhận xe cụ thể.</summary>
public record VehicleDto(
    int Id, string Name, string Brand,
    VehicleCategory Category, string CategoryLabel, int Seats,
    decimal PricePerDay, string PickupLocation, string? ImageUrl,
    int? RangeKm, string Description, string[] Highlights, bool IsAvailable);

/// <summary>Một khoảng thời gian xe đang bận. Kind: "booked" (đã có khách) hoặc "blocked" (chủ xe khoá).</summary>
public record BusyRangeDto(DateTime From, DateTime To, string Kind);

public record StatsDto(int Vehicles, int Locations, int Trips, int DepositPercent);

public record CategoryDto(VehicleCategory Key, string Label);

/// <summary>Dữ liệu chung cho giao diện: địa điểm, loại xe, tỉ lệ cọc, thống kê thật.</summary>
public record MetaDto(
    List<string> Locations, List<CategoryDto> Categories,
    decimal CommissionRate, int DepositHoldMinutes, int MaxRentalDays, StatsDto Stats);

public class QuoteRequest
{
    public int VehicleId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}