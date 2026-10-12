using System.ComponentModel.DataAnnotations;

namespace MoveGo.API.Models;

public class Vehicle
{
    public int Id { get; set; }

    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(60)] public string Brand { get; set; } = "";

    public VehicleCategory Category { get; set; }
    public int Seats { get; set; }

    /// <summary>Giá thuê 1 ngày (24 giờ), đơn vị VNĐ.</summary>
    public decimal PricePerDay { get; set; }

    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;

    /// <summary>false = chủ xe tạm ngưng cho thuê, không hiện lên web.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thành phố / khu vực nhận xe (dùng để lọc).</summary>
    [MaxLength(60)] public string PickupLocation { get; set; } = "";

    /// <summary>Địa chỉ nhận xe cụ thể, chỉ hiện cho khách SAU KHI đã cọc.</summary>
    [MaxLength(200)] public string PickupAddress { get; set; } = "";

    [MaxLength(1000)] public string Description { get; set; } = "";

    /// <summary>Ảnh thật của xe. Để trống thì web hiển thị hình minh hoạ SVG.</summary>
    [MaxLength(500)] public string? ImageUrl { get; set; }

    /// <summary>Quãng đường 1 lần sạc (km), dành cho xe điện.</summary>
    public int? RangeKm { get; set; }

    /// <summary>Các điểm nổi bật, ngăn cách bằng dấu |</summary>
    [MaxLength(300)] public string Highlights { get; set; } = "";

    public List<Booking> Bookings { get; set; } = new();
    public List<VehicleUnavailability> Unavailabilities { get; set; } = new();
}