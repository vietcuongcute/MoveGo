using System.Globalization;
using MoveGo.Web.Models;

namespace MoveGo.Web.Helpers;

/// <summary>Hàm hỗ trợ hiển thị cho Razor view (tiền VNĐ, ngày tiếng Việt, màu/hình minh hoạ xe).</summary>
public static class ViewHelpers
{
    /// <summary>1250000 → "1.250.000đ" (không phụ thuộc culture của máy chủ).</summary>
    public static string Vnd(decimal v) =>
        string.Format(CultureInfo.InvariantCulture, "{0:N0}", v).Replace(',', '.') + "đ";

    /// <summary>1250000 → "1,25 triệu" cho chỗ hẹp.</summary>
    public static string VndShort(decimal v) =>
        v >= 1_000_000 ? (v / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + " triệu"
                       : (v / 1000m).ToString("0", CultureInfo.InvariantCulture) + "k";

    public static string DayName(DateTime d) => d.DayOfWeek switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        _ => "Chủ nhật"
    };

    /// <summary>"Thứ Hai, 20/10/2026 · 08:00"</summary>
    public static string DateTimeVn(DateTime d) => $"{DayName(d)}, {d:dd/MM/yyyy} · {d:HH:mm}";
    public static string ShortDateTimeVn(DateTime d) => $"{d:HH:mm} {d:dd/MM/yyyy}";

    /// <summary>Id của hình minh hoạ trong _ArtSprite.</summary>
    public static string ArtId(VehicleCategory c) => c switch
    {
        VehicleCategory.Car7Seats => "art-van",
        VehicleCategory.ElectricBike => "art-bike",
        _ => "art-car"
    };

    private static readonly string[] CarTones = ["#10b981", "#06b6d4", "#14b8a6", "#0ea5e9", "#22c55e"];
    private static readonly string[] BikeTones = ["#f97316", "#f59e0b", "#ec4899", "#8b5cf6"];

    /// <summary>Mỗi xe một màu riêng (ổn định theo Id) để lưới xe sinh động.</summary>
    public static string Tone(VehicleDto v) =>
        v.Category == VehicleCategory.ElectricBike ? BikeTones[v.Id % BikeTones.Length] : CarTones[v.Id % CarTones.Length];

    public static string CategoryIcon(VehicleCategory c) => c switch
    {
        VehicleCategory.ElectricBike => "fa-motorcycle",
        VehicleCategory.Car7Seats => "fa-van-shuttle",
        _ => "fa-car-side"
    };

    /// <summary>Giờ hiện tại theo múi giờ Việt Nam (UTC+7), không phụ thuộc múi giờ của máy chủ.</summary>
    public static DateTime VnNow() => DateTime.UtcNow.AddHours(7);

    /// <summary>Màu + nhãn hiển thị cho trạng thái đơn trong trang chủ xe.</summary>
    public static (string Cls, string Icon) StatusBadge(BookingStatus s) => s switch
    {
        BookingStatus.PendingOwner => ("bg-amber-50 text-amber-700", "fa-hourglass-half"),
        BookingStatus.AwaitingDeposit => ("bg-cyan-50 text-cyan-700", "fa-qrcode"),
        BookingStatus.Confirmed => ("bg-emerald-50 text-emerald-700", "fa-circle-check"),
        BookingStatus.Completed => ("bg-emerald-50 text-emerald-700", "fa-flag-checkered"),
        BookingStatus.Rejected => ("bg-red-50 text-red-600", "fa-circle-xmark"),
        _ => ("bg-slate-100 text-slate-500", "fa-clock")
    };
}
