using Microsoft.AspNetCore.Mvc;
using MoveGo.Web.Models;
using MoveGo.Web.Services;

namespace MoveGo.Web.Controllers;

public class BookingController(IMoveGoApi api) : Controller
{
    // Mã đơn do API sinh: "MG-" + 6 ký tự, bỏ O/0/I/1 cho dễ đọc (xem Tokens.BookingCode).
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Chấp nhận "mg-k7p2qx", "MG K7P2QX", "k7p2qx"... và trả về "MG-K7P2QX". Sai định dạng thì trả null.</summary>
    public static string? NormalizeCode(string? input)
    {
        var s = new string((input ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        var body = s.Length == 8 && s.StartsWith("MG", StringComparison.Ordinal) ? s[2..] : s.Length == 6 ? s : null;
        return body is not null && body.All(c => CodeAlphabet.Contains(c)) ? "MG-" + body : null;
    }

    /// <summary>Chuỗi toàn chữ số (cho phép +, khoảng trắng, chấm, gạch) dài từ 9 số trở lên → coi là số điện thoại.</summary>
    public static bool LooksLikePhone(string? input)
    {
        var s = (input ?? "").Trim();
        if (s.Length == 0 || !s.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '.' or '-' or '(' or ')')) return false;
        return s.Count(char.IsAsciiDigit) >= 9;
    }

    /// <summary>
    /// Trang tra cứu đơn: khách nhập MÃ ĐƠN (chuyển sang trang theo dõi) hoặc SỐ ĐIỆN THOẠI đã đặt (hiện danh sách đơn).
    /// </summary>
    [HttpGet("tra-cuu")]
    public async Task<IActionResult> Lookup(string? ma, CancellationToken ct)
    {
        Response.Headers["X-Robots-Tag"] = "noindex";
        if (string.IsNullOrWhiteSpace(ma)) return View(new LookupViewModel());

        if (LooksLikePhone(ma))
        {
            var res = await api.FindBookingsByPhoneAsync(ma.Trim(), ct);
            return View(new LookupViewModel
            {
                Input = ma.Trim(),
                Error = res.Ok ? null : res.Error,
                PhoneResults = res.Ok ? res.Value ?? [] : null
            });
        }

        var code = NormalizeCode(ma);
        if (code is null)
            return View(new LookupViewModel
            {
                Input = ma.Trim(),
                Error = "Chưa nhận ra mã đơn hoặc số điện thoại. Mã đơn có dạng MG-XXXXXX (không có chữ O, I và số 0, 1); số điện thoại gồm 10 số."
            });
        return RedirectToAction(nameof(Track), new { code });
    }

    /// <summary>Trang theo dõi đơn của khách: /booking/MG-K7P2QX</summary>
    [HttpGet("booking/{code}")]
    public async Task<IActionResult> Track(string code, CancellationToken ct)
    {
        var result = await api.GetBookingAsync(code, ct);
        Response.Headers["X-Robots-Tag"] = "noindex";
        return View(result);
    }
}
