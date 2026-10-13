using System.Globalization;

namespace MoveGo.Web.Models;

/// <summary>Các ô trong form tìm xe ở hero (gửi bằng GET để link chia sẻ được).</summary>
public class SearchQuery
{
    public VehicleCategory? Category { get; set; }
    public string? From { get; set; }          // yyyy-MM-dd
    public string? To { get; set; }            // yyyy-MM-dd
    public string FromTime { get; set; } = "08:00";
    public string ToTime { get; set; } = "08:00";

    public bool HasDates => !string.IsNullOrWhiteSpace(From) && !string.IsNullOrWhiteSpace(To);

    public bool TryGetRange(out DateTime start, out DateTime end, out string? error)
    {
        start = end = default;
        error = null;
        if (!HasDates) return false;

        if (!TryParse(From, FromTime, out start) || !TryParse(To, ToTime, out end))
        {
            error = "Ngày giờ bạn chọn chưa hợp lệ.";
            return false;
        }
        if (end <= start)
        {
            error = "Giờ trả xe phải sau giờ nhận xe.";
            return false;
        }
        return true;
    }

    private static bool TryParse(string? date, string time, out DateTime result) =>
        DateTime.TryParseExact($"{date} {time}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}

public class HomeViewModel
{
    public SearchQuery Search { get; set; } = new();
    public MetaDto Meta { get; set; } = MetaDto.Empty;
    public List<VehicleDto> AvailableVehicles { get; set; } = [];

    /// <summary>true khi khách đã chọn ngày → danh sách chỉ gồm xe còn trống trong khoảng đó.</summary>
    public bool HasSearch { get; set; }
    public DateTime? SearchStart { get; set; }
    public DateTime? SearchEnd { get; set; }
    public string? SearchError { get; set; }
    public string? ApiError { get; set; }
}

public class SiteOptions
{
    public string BrandName { get; set; } = "MoveGo";
    public string Hotline { get; set; } = "0979 058 531";
    public string ZaloPhone { get; set; } = "0979058531";
    public string Email { get; set; } = "hello@movego.vn";
    public string Address { get; set; } = "Đà Lạt, Việt Nam";
}

public class ApiOptions
{
    /// <summary>Địa chỉ API mà SERVER MVC gọi tới.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5100";

    /// <summary>Địa chỉ API mà TRÌNH DUYỆT của khách gọi (AJAX). Thường giống BaseUrl; khác nhau khi chạy trong Docker.</summary>
    public string PublicUrl { get; set; } = "http://localhost:5100";
}

/// <summary>Trang tra cứu đơn: giữ lại nội dung khách đã nhập khi mã sai định dạng.</summary>
public class LookupViewModel
{
    public string? Input { get; set; }
    public string? Error { get; set; }

    /// <summary>Khi khách tra bằng SĐT: danh sách đơn tìm được (null = chưa tra bằng SĐT).</summary>
    public List<BookingSummaryDto>? PhoneResults { get; set; }
}
