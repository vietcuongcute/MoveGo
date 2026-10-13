using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoveGo.Web.Models;

namespace MoveGo.Web.Services;

/// <summary>Kết quả gọi API: có dữ liệu, hoặc có thông báo lỗi tiếng Việt từ API (ProblemDetails.title).</summary>
public record ApiResult<T>(T? Value, int Status, string? Error)
{
    public bool Ok => Error is null;
}

public interface IMoveGoApi
{
    Task<MetaDto> GetMetaAsync(CancellationToken ct = default);
    Task<List<VehicleDto>> GetVehiclesAsync(VehicleCategory? category, string? location, CancellationToken ct = default);
    Task<List<VehicleDto>> GetAvailableVehiclesAsync(DateTime start, DateTime end, VehicleCategory? category, string? location, CancellationToken ct = default);

    Task<ApiResult<BookingTrackDto>> GetBookingAsync(string code, CancellationToken ct = default);

    Task<ApiResult<OwnerRequestDto>> GetOwnerRequestAsync(string token, CancellationToken ct = default);
    Task<ApiResult<OwnerRequestDto>> OwnerRespondAsync(string token, bool accept, string? reason, CancellationToken ct = default);

    Task<ApiResult<OwnerDashboardDto>> GetOwnerDashboardAsync(string manageToken, CancellationToken ct = default);
    Task<ApiResult<OwnerBlockDto>> AddBlockAsync(string manageToken, int vehicleId, DateTime start, DateTime end, string? reason, CancellationToken ct = default);
    Task<ApiResult<bool>> RemoveBlockAsync(string manageToken, int blockId, CancellationToken ct = default);
    Task<ApiResult<bool>> SetActiveAsync(string manageToken, int vehicleId, bool active, CancellationToken ct = default);
}

/// <summary>
/// Client gọi MoveGo.API. Dùng IHttpClientFactory (đăng ký tên "MoveGoApi" trong Program.cs) để
/// tái sử dụng kết nối HTTP đúng cách — không bao giờ tự "new HttpClient()" trong mỗi request.
/// </summary>
public class MoveGoApi(IHttpClientFactory factory) : IMoveGoApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient Http => factory.CreateClient("MoveGoApi");

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss");

    // ---------- Dữ liệu cho trang chủ (lỗi mạng sẽ ném HttpRequestException để controller hiển thị thông báo) ----------
    public async Task<MetaDto> GetMetaAsync(CancellationToken ct = default) =>
        await Http.GetFromJsonAsync<MetaDto>("api/meta", Json, ct) ?? MetaDto.Empty;

    public async Task<List<VehicleDto>> GetVehiclesAsync(VehicleCategory? category, string? location, CancellationToken ct = default)
    {
        var qs = BuildQuery(("category", category?.ToString()), ("location", location));
        return await Http.GetFromJsonAsync<List<VehicleDto>>($"api/vehicles{qs}", Json, ct) ?? [];
    }

    public async Task<List<VehicleDto>> GetAvailableVehiclesAsync(
        DateTime start, DateTime end, VehicleCategory? category, string? location, CancellationToken ct = default)
    {
        var qs = BuildQuery(("start", Iso(start)), ("end", Iso(end)), ("category", category?.ToString()), ("location", location));
        return await Http.GetFromJsonAsync<List<VehicleDto>>($"api/booking/available-vehicles{qs}", Json, ct) ?? [];
    }

    // ---------- Các thao tác trả ApiResult ----------
    public Task<ApiResult<BookingTrackDto>> GetBookingAsync(string code, CancellationToken ct = default) =>
        SendAsync<BookingTrackDto>(HttpMethod.Get, $"api/booking/{Uri.EscapeDataString(code)}", null, ct);

    public Task<ApiResult<OwnerRequestDto>> GetOwnerRequestAsync(string token, CancellationToken ct = default) =>
        SendAsync<OwnerRequestDto>(HttpMethod.Get, $"api/owner/request/{Uri.EscapeDataString(token)}", null, ct);

    public Task<ApiResult<OwnerRequestDto>> OwnerRespondAsync(string token, bool accept, string? reason, CancellationToken ct = default) =>
        SendAsync<OwnerRequestDto>(HttpMethod.Post, $"api/owner/request/{Uri.EscapeDataString(token)}", new { accept, reason }, ct);

    public Task<ApiResult<OwnerDashboardDto>> GetOwnerDashboardAsync(string manageToken, CancellationToken ct = default) =>
        SendAsync<OwnerDashboardDto>(HttpMethod.Get, $"api/owner/manage/{Uri.EscapeDataString(manageToken)}", null, ct);

    public Task<ApiResult<OwnerBlockDto>> AddBlockAsync(string manageToken, int vehicleId, DateTime start, DateTime end, string? reason, CancellationToken ct = default) =>
        SendAsync<OwnerBlockDto>(HttpMethod.Post, $"api/owner/manage/{Uri.EscapeDataString(manageToken)}/vehicles/{vehicleId}/blocks",
            new { startDate = Iso(start), endDate = Iso(end), reason }, ct);

    public Task<ApiResult<bool>> RemoveBlockAsync(string manageToken, int blockId, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Delete, $"api/owner/manage/{Uri.EscapeDataString(manageToken)}/blocks/{blockId}", null, ct);

    public Task<ApiResult<bool>> SetActiveAsync(string manageToken, int vehicleId, bool active, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Post, $"api/owner/manage/{Uri.EscapeDataString(manageToken)}/vehicles/{vehicleId}/active?value={active.ToString().ToLowerInvariant()}", null, ct);

    private Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct) =>
        ApiHttp.SendAsync<T>(Http, Json, method, url, body, null, ct);

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Value))
                         .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length == 0 ? "" : "?" + qs;
    }
}
