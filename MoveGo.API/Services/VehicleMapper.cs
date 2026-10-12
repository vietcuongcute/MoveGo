using MoveGo.API.Dtos;
using MoveGo.API.Models;

namespace MoveGo.API.Services;

public static class VehicleMapper
{
    public static string CategoryLabel(VehicleCategory c) => c switch
    {
        VehicleCategory.Car4Seats => "Ô tô 4 chỗ",
        VehicleCategory.Car7Seats => "Ô tô 7 chỗ",
        VehicleCategory.ElectricBike => "Xe máy điện",
        _ => c.ToString()
    };

    /// <summary>Không bao giờ trả thông tin chủ xe (SĐT) hay địa chỉ nhận xe cụ thể ra ngoài.</summary>
    public static VehicleDto ToDto(Vehicle v, bool isAvailable = true) => new(
        v.Id, v.Name, v.Brand, v.Category, CategoryLabel(v.Category), v.Seats,
        v.PricePerDay, v.PickupLocation, v.ImageUrl, v.RangeKm, v.Description,
        v.Highlights.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        isAvailable);
}