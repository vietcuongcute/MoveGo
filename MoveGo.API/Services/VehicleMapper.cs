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

    public const int MaxImages = 8;

    /// <summary>Danh sách ảnh của xe: ưu tiên Gallery, nếu trống thì dùng ImageUrl cũ.</summary>
    public static string[] Images(Vehicle v)
    {
        var list = v.Gallery.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (list.Length == 0 && !string.IsNullOrWhiteSpace(v.ImageUrl)) return [v.ImageUrl];
        return list;
    }

    /// <summary>Không bao giờ trả thông tin chủ xe (SĐT) ra ngoài.</summary>
    public static VehicleDto ToDto(Vehicle v, bool isAvailable = true) => new(
        v.Id, v.Name, v.Brand, v.Category, CategoryLabel(v.Category), v.Seats,
        v.PricePerDay, v.ImageUrl, v.RangeKm, v.Description,
        v.Highlights.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        isAvailable, Images(v));
}