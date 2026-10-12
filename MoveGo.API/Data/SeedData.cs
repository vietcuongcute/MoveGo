using Microsoft.EntityFrameworkCore;
using MoveGo.API.Models;
using MoveGo.API.Services;

namespace MoveGo.API.Data;

/// <summary>
/// Dữ liệu mẫu để chạy thử. SĐT chủ xe là SĐT GIẢ (đầu 01 không tồn tại) nên không lo gửi nhầm tin cho người lạ.
/// Khi chạy thật: tắt "Seed:Enabled" và nhập chủ xe/xe thật vào database.
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db, IClock clock)
    {
        if (await db.Owners.AnyAsync()) return;   // đã có dữ liệu thì thôi
        var today = clock.Now.Date;

        Owner NewOwner(string name, string phone) => new()
        {
            Name = name,
            PhoneNumber = phone,
            ZaloPhone = phone,
            ManageToken = Tokens.UrlToken(),
            CreatedAt = clock.Now
        };

        var minh = NewOwner("Anh Minh", "0100000001");
        var lan = NewOwner("Chị Lan", "0100000002");
        var hung = NewOwner("Anh Hùng", "0100000003");
        var thao = NewOwner("Chị Thảo", "0100000004");
        db.Owners.AddRange(minh, lan, hung, thao);

        Vehicle V(Owner o, string name, string brand, VehicleCategory cat, int seats, decimal price, string city,
                  string address, int? range, string desc, string highlights) => new()
                  {
                      Owner = o,
                      Name = name,
                      Brand = brand,
                      Category = cat,
                      Seats = seats,
                      PricePerDay = price,
                      PickupLocation = city,
                      PickupAddress = address,
                      RangeKm = range,
                      Description = desc,
                      Highlights = highlights
                  };

        var vehicles = new List<Vehicle>
        {
            V(minh, "VinFast VF 3", "VinFast", VehicleCategory.Car4Seats, 4, 650_000, "TP. Hồ Chí Minh",
              "Quận 7, TP. Hồ Chí Minh", 210, "Mini SUV điện nhỏ gọn, cực hợp đi phố và đỗ xe.", "Nhỏ gọn dễ đỗ|Đi đến 210 km/lần sạc|Điều hoà mát"),
            V(minh, "VinFast VF 5 Plus", "VinFast", VehicleCategory.Car4Seats, 5, 850_000, "TP. Hồ Chí Minh",
              "Quận 7, TP. Hồ Chí Minh", 326, "SUV hạng A rộng rãi, tiết kiệm, đi tỉnh thoải mái.", "Màn hình 8\"|Đi đến 326 km/lần sạc|Cốp rộng"),
            V(lan, "VinFast VF 6", "VinFast", VehicleCategory.Car4Seats, 5, 1_050_000, "Hà Nội",
              "Cầu Giấy, Hà Nội", 399, "SUV hạng B êm ái, nhiều tính năng an toàn.", "Cảm biến an toàn|Đi đến 399 km/lần sạc|Nội thất rộng"),
            V(lan, "VinFast VF 8", "VinFast", VehicleCategory.Car4Seats, 5, 1_400_000, "Hà Nội",
              "Cầu Giấy, Hà Nội", 471, "SUV điện hạng D mạnh mẽ, đi xa không lo hết pin.", "Đi đến 471 km/lần sạc|Ghế da|Màn hình 15.6\""),
            V(hung, "VinFast VF 9", "VinFast", VehicleCategory.Car7Seats, 7, 1_900_000, "Đà Nẵng",
              "Sơn Trà, Đà Nẵng", 438, "SUV điện 7 chỗ sang trọng cho cả gia đình.", "7 chỗ rộng rãi|Đi đến 438 km/lần sạc|Ghế thương gia"),
            V(hung, "VinFast VF 7", "VinFast", VehicleCategory.Car4Seats, 5, 1_250_000, "Đà Nẵng",
              "Hải Châu, Đà Nẵng", 450, "SUV điện phong cách thể thao, lái rất sướng.", "Thiết kế thể thao|Đi đến 450 km/lần sạc|Cốp rộng"),
            V(thao, "Limo Green 7 chỗ", "VinFast", VehicleCategory.Car7Seats, 7, 1_500_000, "TP. Hồ Chí Minh",
              "Quận 1, TP. Hồ Chí Minh", 450, "MPV điện 7 chỗ chuyên đi nhóm, sân bay, du lịch.", "7 chỗ|Cửa lùa tiện lợi|Chạy êm"),
            V(thao, "VinFast Evo 200", "VinFast", VehicleCategory.ElectricBike, 2, 180_000, "TP. Hồ Chí Minh",
              "Quận 3, TP. Hồ Chí Minh", 200, "Xe máy điện đi phố, không cần đổ xăng.", "Đi đến 200 km/lần sạc|Cốp rộng|Tặng 2 mũ bảo hiểm"),
            V(thao, "VinFast Feliz S", "VinFast", VehicleCategory.ElectricBike, 2, 220_000, "Hà Nội",
              "Đống Đa, Hà Nội", 198, "Xe máy điện thời trang, êm và tiết kiệm.", "Đi đến 198 km/lần sạc|Đèn LED|Cốp rộng"),
            V(hung, "VinFast Klara S", "VinFast", VehicleCategory.ElectricBike, 2, 200_000, "Đà Nẵng",
              "Mỹ Khê, Đà Nẵng", 194, "Xe máy điện cổ điển, dạo biển Đà Nẵng cực chill.", "Đi đến 194 km/lần sạc|Phong cách Ý|Tặng 2 mũ bảo hiểm"),
        };
        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync();

        // Vài lịch bận để xem lịch trống hoạt động ngay ở bước 3
        var vf5 = vehicles[1];
        var vf8 = vehicles[3];
        db.VehicleUnavailabilities.Add(new VehicleUnavailability
        {
            VehicleId = vf5.Id,
            StartDate = today.AddDays(5),
            EndDate = today.AddDays(7).AddHours(23),
            Reason = "Bảo dưỡng định kỳ"
        });
        db.Bookings.Add(new Booking
        {
            Code = "MG-DEMO01",
            VehicleId = vf8.Id,
            CustomerName = "Khách mẫu",
            CustomerPhone = "0100000099",
            StartDate = today.AddDays(2).AddHours(8),
            EndDate = today.AddDays(4).AddHours(8),
            RentalDays = 2,
            PricePerDayAtBooking = vf8.PricePerDay,
            TotalAmount = vf8.PricePerDay * 2,
            CommissionAmount = vf8.PricePerDay * 2 * 0.1m,
            OwnerAmount = vf8.PricePerDay * 2 * 0.9m,
            Status = BookingStatus.Confirmed,
            OwnerToken = Tokens.UrlToken(),
            CreatedAt = clock.Now,
            PaidAt = clock.Now,
            OwnerRespondedAt = clock.Now
        });
        await db.SaveChangesAsync();
    }
}