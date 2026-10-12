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