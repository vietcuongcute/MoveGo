using System.Text.Json;
using System.Text.Json.Serialization;
using MoveGo.Web.Models;

namespace MoveGo.Web.Services;

public interface IAdminApi
{
    Task<ApiResult<AdminSummaryDto>> GetSummaryAsync(string key, CancellationToken ct = default);
    Task<ApiResult<List<AdminBookingDto>>> GetBookingsAsync(string key, string? status, CancellationToken ct = default);
    Task<ApiResult<bool>> ResendOwnerAsync(string key, int bookingId, CancellationToken ct = default);

    Task<ApiResult<List<AdminOwnerDto>>> GetOwnersAsync(string key, CancellationToken ct = default);
    Task<ApiResult<AdminOwnerDto>> SaveOwnerAsync(string key, OwnerForm form, CancellationToken ct = default);
    Task<ApiResult<AdminOwnerDto>> ResetOwnerLinkAsync(string key, int ownerId, CancellationToken ct = default);

    Task<ApiResult<List<AdminVehicleDto>>> GetVehiclesAsync(string key, CancellationToken ct = default);
    Task<ApiResult<AdminVehicleDto>> GetVehicleAsync(string key, int id, CancellationToken ct = default);
    Task<ApiResult<AdminVehicleDto>> SaveVehicleAsync(string key, VehicleForm form, CancellationToken ct = default);
    Task<ApiResult<AdminVehicleDto>> SetVehicleActiveAsync(string key, int id, bool active, CancellationToken ct = default);
}

/// <summary>Client cho /api/admin. Khoá quản trị truyền theo từng lời gọi (không lưu trong object này).</summary>
public class AdminApi(IHttpClientFactory factory) : IAdminApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient Http => factory.CreateClient("MoveGoApi");

    private Task<ApiResult<T>> Send<T>(string key, HttpMethod method, string url, object? body, CancellationToken ct) =>
        ApiHttp.SendAsync<T>(Http, Json, method, url, body, ("X-Admin-Key", key), ct);

    public Task<ApiResult<AdminSummaryDto>> GetSummaryAsync(string key, CancellationToken ct = default) =>
        Send<AdminSummaryDto>(key, HttpMethod.Get, "api/admin/summary", null, ct);

    public Task<ApiResult<List<AdminBookingDto>>> GetBookingsAsync(string key, string? status, CancellationToken ct = default) =>
        Send<List<AdminBookingDto>>(key, HttpMethod.Get,
            "api/admin/bookings" + (string.IsNullOrWhiteSpace(status) ? "" : "?status=" + Uri.EscapeDataString(status)), null, ct);

    public Task<ApiResult<bool>> ResendOwnerAsync(string key, int bookingId, CancellationToken ct = default) =>
        Send<bool>(key, HttpMethod.Post, $"api/admin/bookings/{bookingId}/resend-owner", null, ct);

    public Task<ApiResult<List<AdminOwnerDto>>> GetOwnersAsync(string key, CancellationToken ct = default) =>
        Send<List<AdminOwnerDto>>(key, HttpMethod.Get, "api/admin/owners", null, ct);

    public Task<ApiResult<AdminOwnerDto>> SaveOwnerAsync(string key, OwnerForm f, CancellationToken ct = default)
    {
        var body = new { name = f.Name, phoneNumber = f.PhoneNumber, zaloPhone = f.ZaloPhone };
        return f.Id == 0
            ? Send<AdminOwnerDto>(key, HttpMethod.Post, "api/admin/owners", body, ct)
            : Send<AdminOwnerDto>(key, HttpMethod.Put, $"api/admin/owners/{f.Id}", body, ct);
    }

    public Task<ApiResult<AdminOwnerDto>> ResetOwnerLinkAsync(string key, int ownerId, CancellationToken ct = default) =>
        Send<AdminOwnerDto>(key, HttpMethod.Post, $"api/admin/owners/{ownerId}/reset-link", null, ct);

    public Task<ApiResult<List<AdminVehicleDto>>> GetVehiclesAsync(string key, CancellationToken ct = default) =>
        Send<List<AdminVehicleDto>>(key, HttpMethod.Get, "api/admin/vehicles", null, ct);

    public Task<ApiResult<AdminVehicleDto>> GetVehicleAsync(string key, int id, CancellationToken ct = default) =>
        Send<AdminVehicleDto>(key, HttpMethod.Get, $"api/admin/vehicles/{id}", null, ct);

    public Task<ApiResult<AdminVehicleDto>> SaveVehicleAsync(string key, VehicleForm f, CancellationToken ct = default)
    {
        var highlights = (f.HighlightsText ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var body = new
        {
            ownerId = f.OwnerId, name = f.Name, brand = f.Brand, category = f.Category, seats = f.Seats,
            pricePerDay = f.PricePerDay, pickupAddress = f.PickupAddress,
            description = f.Description, imageUrl = f.ImageUrl, rangeKm = f.RangeKm, highlights, isActive = f.IsActive
        };
        return f.Id == 0
            ? Send<AdminVehicleDto>(key, HttpMethod.Post, "api/admin/vehicles", body, ct)
            : Send<AdminVehicleDto>(key, HttpMethod.Put, $"api/admin/vehicles/{f.Id}", body, ct);
    }

    public Task<ApiResult<AdminVehicleDto>> SetVehicleActiveAsync(string key, int id, bool active, CancellationToken ct = default) =>
        Send<AdminVehicleDto>(key, HttpMethod.Put, $"api/admin/vehicles/{id}/active", new { isActive = active }, ct);
}
