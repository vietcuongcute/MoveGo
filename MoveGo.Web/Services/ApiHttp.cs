using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MoveGo.Web.Services;

/// <summary>Gửi request tới MoveGo.API và gói kết quả thành ApiResult (dùng chung cho API công khai và API admin).</summary>
internal static class ApiHttp
{
    public static async Task<ApiResult<T>> SendAsync<T>(
        HttpClient http, JsonSerializerOptions json, HttpMethod method, string url, object? body,
        (string Name, string Value)? header, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(method, url);
            if (header is { } h) req.Headers.Add(h.Name, h.Value);
            if (body is not null) req.Content = JsonContent.Create(body, options: json);

            using var res = await http.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
                return new ApiResult<T>(await res.Content.ReadFromJsonAsync<T>(json, ct), (int)res.StatusCode, null);

            return new ApiResult<T>(default, (int)res.StatusCode, await ReadProblemTitleAsync(res, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ApiResult<T>(default, 503, "Hệ thống đang bận, vui lòng thử lại sau ít phút.");
        }
    }

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("title", out var t) && t.GetString() is { Length: > 0 } title) return title;
        }
        catch (JsonException) { }

        return res.StatusCode == HttpStatusCode.NotFound ? "Không tìm thấy." : "Có lỗi xảy ra, vui lòng thử lại.";
    }
}
