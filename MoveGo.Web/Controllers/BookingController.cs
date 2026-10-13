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

    /// <summary>Trang tra cứu đơn: khách nhập mã đơn rồi chuyển sang trang theo dõi.</summary>
    [HttpGet("tra-cuu")]
    public IActionResult Lookup(string? ma)
    {
        Response.Headers["X-Robots-Tag"] = "noindex";
        if (string.IsNullOrWhiteSpace(ma)) return View(new LookupViewModel());
        var code = NormalizeCode(ma);
        if (code is null)
            return View(new LookupViewModel
            {
                Input = ma.Trim(),
                Error = "Mã đơn chưa đúng. Mã có dạng MG-XXXXXX (6 ký tự gồm chữ và số, không có chữ O, I và số 0, 1)."
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
