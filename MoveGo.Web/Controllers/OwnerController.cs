using Microsoft.AspNetCore.Mvc;
using MoveGo.Web.Services;

namespace MoveGo.Web.Controllers;

/// <summary>Trang dành cho chủ xe — mở bằng link bí mật gửi qua Zalo.</summary>
public class OwnerController(IMoveGoApi api) : Controller
{
    // ---------- Xác nhận đơn ----------
    [HttpGet("owner/confirm/{token}")]
    public async Task<IActionResult> Confirm(string token, CancellationToken ct)
    {
        Response.Headers["X-Robots-Tag"] = "noindex";
        return View(await api.GetOwnerRequestAsync(token, ct));
    }

    [HttpPost("owner/confirm/{token}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(string token, string decision, string? reason, CancellationToken ct)
    {
        var accept = decision == "accept";
        var result = await api.OwnerRespondAsync(token, accept, reason, ct);

        TempData[result.Ok ? "Flash" : "FlashError"] = result.Ok
            ? (accept ? "Cảm ơn bạn! Khách có thời gian để đặt cọc. Sau khi khách cọc, Move-Go sẽ gửi số điện thoại khách qua Zalo."
                      : "Đã từ chối yêu cầu. Move-Go sẽ báo lại cho khách.")
            : result.Error;

        return RedirectToAction(nameof(Confirm), new { token });
    }

    // ---------- Quản lý lịch ----------
    [HttpGet("owner/manage/{token}")]
    public async Task<IActionResult> Manage(string token, CancellationToken ct)
    {
        Response.Headers["X-Robots-Tag"] = "noindex";
        return View(await api.GetOwnerDashboardAsync(token, ct));
    }

    [HttpPost("owner/manage/{token}/block"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlock(string token, int vehicleId, string from, string to, string? reason, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var start) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var endDay))
        {
            TempData["FlashError"] = "Vui lòng chọn khoảng ngày cần khoá.";
            return RedirectToAction(nameof(Manage), new { token });
        }

        // Khoá trọn ngày: từ 00:00 ngày đầu đến hết ngày cuối.
        var result = await api.AddBlockAsync(token, vehicleId, start.Date, endDay.Date.AddDays(1), reason, ct);
        TempData[result.Ok ? "Flash" : "FlashError"] = result.Ok ? "Đã khoá lịch. Khách sẽ không thấy xe trống trong khoảng này." : result.Error;
        return RedirectToAction(nameof(Manage), new { token });
    }

    [HttpPost("owner/manage/{token}/block/{blockId:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBlock(string token, int blockId, CancellationToken ct)
    {
        var result = await api.RemoveBlockAsync(token, blockId, ct);
        TempData[result.Ok ? "Flash" : "FlashError"] = result.Ok ? "Đã mở lại lịch." : result.Error;
        return RedirectToAction(nameof(Manage), new { token });
    }

    [HttpPost("owner/manage/{token}/vehicle/{vehicleId:int}/active"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string token, int vehicleId, bool active, CancellationToken ct)
    {
        var result = await api.SetActiveAsync(token, vehicleId, active, ct);
        TempData[result.Ok ? "Flash" : "FlashError"] = result.Ok
            ? (active ? "Xe đã được bật cho thuê trở lại." : "Đã tạm ngưng cho thuê xe này.")
            : result.Error;
        return RedirectToAction(nameof(Manage), new { token });
    }
}
