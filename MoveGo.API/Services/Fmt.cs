using System.Globalization;
using System.Text.RegularExpressions;

namespace MoveGo.API.Services;

/// <summary>Định dạng hiển thị kiểu Việt Nam, không phụ thuộc culture của máy chủ.</summary>
public static class Fmt
{
    public static string Money(decimal v) =>
        string.Format(CultureInfo.InvariantCulture, "{0:N0}", v).Replace(',', '.') + "đ";

    public static string DateTimeVn(DateTime d) => d.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
}

public static partial class PhoneNumbers
{
    // Di động VN: 10 số, đầu 03/05/07/08/09
    [GeneratedRegex(@"^0[35789]\d{8}$")]
    private static partial Regex VnMobile();

    /// <summary>"+84 912.345.678" → "0912345678". Trả về null nếu không hợp lệ.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("+84")) digits = "0" + digits[3..];
        else if (digits.StartsWith("84") && digits.Length == 11) digits = "0" + digits[2..];
        return VnMobile().IsMatch(digits) ? digits : null;
    }

    /// <summary>"0912345678" → "84912345678" (định dạng Zalo yêu cầu).</summary>
    public static string ToZaloFormat(string phone)
    {
        var p = phone.Trim().Replace(" ", "");
        if (p.StartsWith("+")) p = p[1..];
        if (p.StartsWith("0")) p = "84" + p[1..];
        return p;
    }
}
