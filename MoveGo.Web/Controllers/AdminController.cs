using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using MoveGo.Web.Models;
using MoveGo.Web.Services;

namespace MoveGo.Web.Controllers;

/// <summary>
/// Trang quản trị cho bạn (người vận hành MoveGo). Đăng nhập bằng khoá admin (Business:AdminKey bên API).
/// Khoá được mã hoá bằng Data Protection rồi để trong cookie HttpOnly hết hạn sau 8 giờ;
/// mọi thao tác thật sự đều do API kiểm tra lại khoá, nên web không tự quyết quyền gì.
/// </summary>
[Route("admin")]
public class AdminController(IAdminApi api, IDataProtectionProvider dp, IWebHostEnvironment env) : Controller
{
    private const string CookieName = "mg_admin";
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private ITimeLimitedDataProtector Protector => dp.CreateProtector("MoveGo.Web.AdminKey").ToTimeLimitedDataProtector();

    private string AdminKey { get; set; } = "";

    // ---------- Chặn người chưa đăng nhập ----------
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers["Cache-Control"] = "no-store";
        ViewData["NoIndex"] = true;

        var action = context.RouteData.Values["action"]?.ToString();
        if (action is nameof(Login) or nameof(LoginPost)) return;

        var key = ReadKey();
        if (key is null) { context.Result = RedirectToAction(nameof(Login)); return; }
        AdminKey = key;
    }

    private string? ReadKey()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var cookie) || string.IsNullOrEmpty(cookie)) return null;
        try { return Protector.Unprotect(cookie); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    /// <summary>API từ chối khoá (đã đổi khoá hoặc hết hạn) → quay lại trang đăng nhập.</summary>
    private IActionResult? KickIfUnauthorized(int status)
    {
        if (status != 401) return null;
        Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/admin" });
        TempData["FlashError"] = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult Flash(bool ok, string okMessage, string? error, string action, object? route = null)
    {
        TempData[ok ? "Flash" : "FlashError"] = ok ? okMessage : error;
        return RedirectToAction(action, route);
    }

    // ---------- Đăng nhập ----------
    [HttpGet("login")]
    public IActionResult Login() => ReadKey() is not null ? RedirectToAction(nameof(Index)) : View();

    [HttpPost("login"), ValidateAntiForgeryToken, EnableRateLimiting("admin-login")]
    public async Task<IActionResult> LoginPost(string? key, CancellationToken ct)
    {
        key = key?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            TempData["FlashError"] = "Vui lòng nhập khoá quản trị.";
            return RedirectToAction(nameof(Login));
        }

        var check = await api.GetSummaryAsync(key, ct);
        if (!check.Ok)
        {
            TempData["FlashError"] = check.Status == 401 ? "Khoá quản trị chưa đúng." : check.Error;
            return RedirectToAction(nameof(Login));
        }

        Response.Cookies.Append(CookieName, Protector.Protect(key, TimeSpan.FromHours(8)), new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = Request.IsHttps, Path = "/admin",
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/admin" });
        return RedirectToAction(nameof(Login));
    }

    // ---------- Tổng quan + đơn ----------
    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, CancellationToken ct)
    {
        var summary = await api.GetSummaryAsync(AdminKey, ct);
        if (KickIfUnauthorized(summary.Status) is { } kick) return kick;
        var bookings = await api.GetBookingsAsync(AdminKey, status, ct);

        ViewData["AdminError"] = summary.Error ?? bookings.Error;
        return View(new AdminOverviewViewModel
        {
            Summary = summary.Value ?? new([], 0, 0, 0),
            Bookings = bookings.Value ?? [],
            StatusFilter = status
        });
    }

    [HttpPost("don/{id:int}/gui-lai"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOwner(int id, CancellationToken ct)
    {
        var r = await api.ResendOwnerAsync(AdminKey, id, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;
        var sent = r.Ok && r.Value;
        TempData[sent ? "Flash" : "FlashError"] = r.Ok
            ? (sent ? "Đã gửi lại tin Zalo cho chủ xe." : "Zalo chưa nhận tin, xem log của API để biết lý do.")
            : r.Error;
        return RedirectToAction(nameof(Index));
    }

    // ---------- Xe ----------
    [HttpGet("xe")]
    public async Task<IActionResult> Vehicles(CancellationToken ct)
    {
        var r = await api.GetVehiclesAsync(AdminKey, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;
        ViewData["AdminError"] = r.Error;
        return View(r.Value ?? []);
    }

    [HttpGet("xe/moi")]
    public async Task<IActionResult> NewVehicle(CancellationToken ct) =>
        await FormViewAsync(new VehicleForm(), ct);

    [HttpGet("xe/{id:int}")]
    public async Task<IActionResult> EditVehicle(int id, CancellationToken ct)
    {
        var r = await api.GetVehicleAsync(AdminKey, id, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;
        if (!r.Ok) return Flash(false, "", r.Error, nameof(Vehicles));
        return await FormViewAsync(VehicleForm.From(r.Value!), ct);
    }

    [HttpPost("xe/luu"), ValidateAntiForgeryToken, RequestSizeLimit(MaxImageBytes + 512 * 1024)]
    public async Task<IActionResult> SaveVehicle([Bind(Prefix = "")] VehicleForm form, IFormFile? imageFile, CancellationToken ct)
    {
        if (imageFile is { Length: > 0 })
        {
            var (url, error) = await SaveImageAsync(imageFile, ct);
            if (error is not null) { form.Error = error; return await FormViewAsync(form, ct); }
            form.ImageUrl = url;
        }

        var r = await api.SaveVehicleAsync(AdminKey, form, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;
        if (!r.Ok) { form.Error = r.Error; return await FormViewAsync(form, ct); }

        return Flash(true, form.IsNew ? $"Đã thêm xe “{r.Value!.Name}”." : $"Đã lưu xe “{r.Value!.Name}”.", null, nameof(Vehicles));
    }

    [HttpPost("xe/{id:int}/an-hien"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVehicle(int id, bool active, CancellationToken ct)
    {
        var r = await api.SetVehicleActiveAsync(AdminKey, id, active, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;
        return Flash(r.Ok, active ? "Xe đã hiện lại trên web." : "Đã ẩn xe khỏi web. Đơn đang chạy vẫn giữ nguyên.", r.Error, nameof(Vehicles));
    }

    private async Task<IActionResult> FormViewAsync(VehicleForm form, CancellationToken ct)
    {
        var owners = await api.GetOwnersAsync(AdminKey, ct);
        if (KickIfUnauthorized(owners.Status) is { } kick) return kick;

        form.Owners = owners.Value ?? [];
        form.Error ??= owners.Error;
        return View("VehicleForm", form);
    }

    /// <summary>Lưu ảnh xe vào wwwroot/uploads/vehicles. Kiểm tra cả đuôi lẫn nội dung thật của file.</summary>
    private async Task<(string? Url, string? Error)> SaveImageAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > MaxImageBytes) return (null, "Ảnh quá lớn, tối đa 5 MB.");

        var head = new byte[12];
        await using (var s = file.OpenReadStream())
        {
            var read = await s.ReadAsync(head, ct);
            if (read < 12) return (null, "File ảnh không hợp lệ.");
        }

        string? ext = null;
        if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) ext = ".jpg";
        else if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) ext = ".png";
        else if (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' &&
                 head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P') ext = ".webp";
        if (ext is null) return (null, "Chỉ nhận ảnh JPG, PNG hoặc WebP.");

        var dir = Path.Combine(env.WebRootPath, "uploads", "vehicles");
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(dir, name)))
            await file.CopyToAsync(fs, ct);

        return ($"/uploads/vehicles/{name}", null);
    }

    // ---------- Chủ xe ----------
    [HttpGet("chu-xe")]
    public async Task<IActionResult> Owners(int? edit, CancellationToken ct)
    {
        var r = await api.GetOwnersAsync(AdminKey, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;

        var vm = new AdminOwnersViewModel { Owners = r.Value ?? [], Error = r.Error };
        if (edit is { } id && vm.Owners.FirstOrDefault(o => o.Id == id) is { } o)
            vm.Form = new OwnerForm { Id = o.Id, Name = o.Name, PhoneNumber = o.PhoneNumber, ZaloPhone = o.ZaloPhone };
        return View(vm);
    }

    [HttpPost("chu-xe/luu"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOwner([Bind(Prefix = "")] OwnerForm form, CancellationToken ct)
    {
        var r = await api.SaveOwnerAsync(AdminKey, form, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;

        if (!r.Ok)
        {
            var list = await api.GetOwnersAsync(AdminKey, ct);
            return View(nameof(Owners), new AdminOwnersViewModel { Owners = list.Value ?? [], Form = form, Error = r.Error });
        }
        return Flash(true, form.Id == 0
            ? $"Đã thêm chủ xe {r.Value!.Name}. Gửi link quản lý lịch cho họ qua Zalo."
            : $"Đã lưu chủ xe {r.Value!.Name}.", null, nameof(Owners));
    }

    [HttpPost("chu-xe/{id:int}/doi-link"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetOwnerLink(int id, CancellationToken ct)
    {
        var r = await api.ResetOwnerLinkAsync(AdminKey, id, ct);
        if (KickIfUnauthorized(r.Status) is { } kick) return kick;
        return Flash(r.Ok, "Đã cấp link quản lý mới. Link cũ không dùng được nữa, nhớ gửi link mới cho chủ xe.", r.Error, nameof(Owners));
    }
}
