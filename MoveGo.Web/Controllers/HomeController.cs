using Microsoft.AspNetCore.Mvc;
using MoveGo.Web.Models;
using MoveGo.Web.Services;

namespace MoveGo.Web.Controllers;

/// <summary>Các trang công khai: Trang chủ, Xe cho thuê, Tính giá, Quy trình, Hỏi đáp.</summary>
public class HomeController(IMoveGoApi api, ILogger<HomeController> logger) : Controller
{
    /// <summary>Trang chủ: giới thiệu + vài xe nổi bật. Form tìm xe ở đây sẽ chuyển sang trang /xe.</summary>
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await LoadAsync(new SearchQuery(), applySearch: false, withVehicles: true, ct));

    /// <summary>
    /// Trang xe cho thuê. Có chọn ngày (?from=&amp;to=) → chỉ hiện xe CÒN TRỐNG trong khoảng đó
    /// (gọi api/booking/available-vehicles). Chưa chọn ngày → hiện toàn bộ xe đang cho thuê.
    /// Loại xe (category) KHÔNG lọc ở server: danh sách luôn đủ các loại, tab lọc ở trình duyệt được chọn sẵn theo loại khách đã chọn.
    /// </summary>
    [Route("xe")]
    public async Task<IActionResult> Fleet([FromQuery] SearchQuery search, CancellationToken ct) =>
        View(await LoadAsync(search, applySearch: true, withVehicles: true, ct));

    /// <summary>Trang máy tính giá: cần danh sách xe để chọn.</summary>
    [Route("tinh-gia")]
    public async Task<IActionResult> Pricing(CancellationToken ct) =>
        View(await LoadAsync(new SearchQuery(), applySearch: false, withVehicles: true, ct));

    [Route("quy-trinh")]
    public async Task<IActionResult> Process(CancellationToken ct) =>
        View(await LoadAsync(new SearchQuery(), applySearch: false, withVehicles: false, ct));

    [Route("hoi-dap")]
    public async Task<IActionResult> Faq(CancellationToken ct) =>
        View(await LoadAsync(new SearchQuery(), applySearch: false, withVehicles: false, ct));

    [Route("error")]
    public IActionResult Error() => View();

    private async Task<HomeViewModel> LoadAsync(SearchQuery search, bool applySearch, bool withVehicles, CancellationToken ct)
    {
        var vm = new HomeViewModel { Search = search };

        try
        {
            var metaTask = api.GetMetaAsync(ct);
            Task<List<VehicleDto>>? vehiclesTask = null;

            if (withVehicles)
            {
                string? error = null;
                DateTime start = default, end = default;
                var hasRange = applySearch && search.TryGetRange(out start, out end, out error);

                if (hasRange)
                {
                    vm.HasSearch = true;
                    vm.SearchStart = start;
                    vm.SearchEnd = end;
                    vehiclesTask = api.GetAvailableVehiclesAsync(start, end, null, search.Location, ct);
                }
                else
                {
                    vm.SearchError = error;
                    vehiclesTask = api.GetVehiclesAsync(null, applySearch ? search.Location : null, ct);
                }
            }

            vm.Meta = await metaTask;
            if (vehiclesTask is not null) vm.AvailableVehicles = await vehiclesTask;
        }
        catch (HttpRequestException ex)
        {
            // API tắt / chưa chạy: vẫn hiện trang với thông báo thân thiện thay vì trang lỗi.
            logger.LogError(ex, "Không gọi được MoveGo.API");
            vm.ApiError = "Hệ thống đang bảo trì ngắn, vui lòng quay lại sau ít phút.";
        }

        return vm;
    }
}
