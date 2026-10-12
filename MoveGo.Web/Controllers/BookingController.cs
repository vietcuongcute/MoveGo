using Microsoft.AspNetCore.Mvc;
using MoveGo.Web.Services;

namespace MoveGo.Web.Controllers;

public class BookingController(IMoveGoApi api) : Controller
{
    /// <summary>Trang theo dõi đơn của khách: /booking/MG-K7P2QX</summary>
    [HttpGet("booking/{code}")]
    public async Task<IActionResult> Track(string code, CancellationToken ct)
    {
        var result = await api.GetBookingAsync(code, ct);
        Response.Headers["X-Robots-Tag"] = "noindex";
        return View(result);
    }
}
