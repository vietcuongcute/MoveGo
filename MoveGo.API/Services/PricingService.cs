using Microsoft.Extensions.Options;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>Kết quả báo giá. Khách cọc đúng CommissionAmount; OwnerAmount trả trực tiếp cho chủ xe.</summary>
public record Quote(int Days, decimal PricePerDay, decimal TotalAmount, decimal CommissionAmount, decimal OwnerAmount);

/// <summary>
/// Quy tắc tính tiền, NGUỒN SỰ THẬT DUY NHẤT. Giao diện (JS) chỉ tính ước lượng để hiển thị,
/// khi tạo đơn server luôn tính lại bằng class này.
/// </summary>
public class PricingService(IOptions<BusinessOptions> options)
{
    private readonly BusinessOptions _opt = options.Value;

    /// <summary>Thuê theo block 24 giờ, tối thiểu 1 ngày. 25 giờ tính 2 ngày.</summary>
    public static int CountDays(DateTime start, DateTime end) =>
        Math.Max(1, (int)Math.Ceiling((end - start).TotalHours / 24d));

    /// <summary>Làm tròn đến 1.000đ.</summary>
    public static decimal RoundVnd(decimal value) =>
        Math.Round(value / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;

    public Quote Calculate(decimal pricePerDay, DateTime start, DateTime end)
    {
        var days = CountDays(start, end);
        var total = pricePerDay * days;
        var commission = RoundVnd(total * _opt.CommissionRate);
        return new Quote(days, pricePerDay, total, commission, total - commission);
    }
}