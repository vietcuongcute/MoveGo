using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MoveGo.Web.Controllers;

/// <summary>Trang ghi nguồn ảnh xe (ảnh Wikimedia Commons giấy phép CC yêu cầu ghi tác giả + giấy phép).</summary>
public class CreditsController(IWebHostEnvironment env) : Controller
{
    public record PhotoCredit(string Title, string Page, string Author, string License, string? LicenseUrl, string File);

    public const string CreditsRelativePath = "uploads/vehicles/credits.json";

    [HttpGet("nguon-anh")]
    public IActionResult Index()
    {
        var path = Path.Combine(env.WebRootPath, CreditsRelativePath);
        var data = new Dictionary<string, List<PhotoCredit>>();
        if (System.IO.File.Exists(path))
        {
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, List<PhotoCredit>>>(
                           System.IO.File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? data;
            }
            catch (JsonException) { /* file hỏng thì hiện trang trống */ }
        }
        return View(data);
    }
}
