using Microsoft.AspNetCore.Mvc;
using MoveGo.Web.Models;
using MoveGo.Web.Services;

namespace MoveGo.Web.Controllers;

public class HomeController(IMoveGoApi api, ILogger<HomeController> logger) : Controller
{
    /// <summary>
    /// Trang chủ. Có chọn ngày (?from=&amp;to=) → chỉ hiện xe CÒN TRỐNG trong khoảng đó (gọi api/booking/available-vehicles).
    /// Chưa chọn ngày → hiện toàn bộ xe đang cho thuê.
    /// Loại xe (category) KHÔNG lọc ở server: danh sách luôn đủ các loại, tab lọc ở trình duyệt được chọn sẵn theo loại khách đã chọn.
    /// </summary>
    public async Task<IActionResult> Index([FromQuery] SearchQuery search, CancellationToken ct)
    {
        var vm = new HomeViewModel { Search = search };

        try
        {
            var metaTask = api.GetMetaAsync(ct);

            Task<List<VehicleDto>> vehiclesTask;
            if (search.TryGetRange(out var start, out var end, out var error))
            {
                vm.HasSearch = true;
                vm.SearchStart = start;
                vm.SearchEnd = end;
                vehiclesTask = api.GetAvailableVehiclesAsync(start, end, null, search.Location, ct);
            }
            else
            {
                vm.SearchError = error;
                vehiclesTask = api.GetVehiclesAsync(null, search.Location, ct);
            }

            await Task.WhenAll(metaTask, vehiclesTask);
            vm.Meta = metaTask.Result;
            vm.AvailableVehicles = vehiclesTask.Result;
        }
        catch (HttpRequestException ex)
        {
            // API tắt / chưa chạy: vẫn hiện trang với thông báo thân thiện thay vì trang lỗi.
            logger.LogError(ex, "Không gọi được MoveGo.API");
            vm.ApiError = "Hệ thống đang bảo trì ngắn, vui lòng quay lại sau ít phút.";
        }

        return View(vm);
    }

    [Route("error")]
    public IActionResult Error() => View();
}
