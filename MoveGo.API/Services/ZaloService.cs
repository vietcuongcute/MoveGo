using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>Các "khoá nghiệp vụ" ↔ mẫu tin nhắn Zalo. Về sau mỗi khoá ứng với một template ZNS đã đăng ký.</summary>
public static class ZaloTemplates
{
    public const string OwnerNewRequest = "owner_new_request";     // Chủ xe: có khách muốn thuê, bấm link để xác nhận
    public const string CustomerAccepted = "customer_accepted";    // Khách: chủ xe đã đồng ý, bấm link để cọc
    public const string CustomerRejected = "customer_rejected";    // Khách: chủ xe không nhận được đơn
    public const string OwnerConfirmed = "owner_confirmed";        // Chủ xe: khách đã cọc, đây là tên + SĐT khách — hãy nhắn khách
    public const string CustomerConfirmed = "customer_confirmed";  // Khách: đã cọc thành công, chủ xe sẽ nhắn Zalo cho bạn (KHÔNG kèm SĐT chủ xe)
}

public interface IZaloService
{
    /// <summary>Gửi tin Zalo. Trả về true nếu gửi thành công.</summary>
    Task<bool> SendAsync(string phone, string templateKey, IReadOnlyDictionary<string, string> data,
                         string trackingId, CancellationToken ct = default);
}

/// <summary>Chế độ giả lập: chỉ in tin nhắn ra log (kèm link) để bạn bấm thử khi lập trình.</summary>
public class MockZaloService(ILogger<MockZaloService> logger) : IZaloService
{
    public Task<bool> SendAsync(string phone, string templateKey, IReadOnlyDictionary<string, string> data,
                                string trackingId, CancellationToken ct = default)
    {
        var body = string.Join("\n    ", data.Select(kv => $"{kv.Key}: {kv.Value}"));
        logger.LogInformation("\n[ZALO-GIẢ-LẬP] → {Phone}  (mẫu: {Template})\n    {Body}", phone, templateKey, body);
        return Task.FromResult(true);
    }
}

/// <summary>Gửi ZNS thật qua Zalo Business API (chỉ dùng khi bật Zalo:Enabled = true).</summary>
public class ZaloZnsService(
    HttpClient http, IOptions<ZaloOptions> options, ZaloTokenProvider tokens, ILogger<ZaloZnsService> logger) : IZaloService
{
    public async Task<bool> SendAsync(string phone, string templateKey, IReadOnlyDictionary<string, string> data,
                                      string trackingId, CancellationToken ct = default)
    {
        var opt = options.Value;

        if (!opt.Templates.TryGetValue(templateKey, out var templateId) || string.IsNullOrWhiteSpace(templateId))
        {
            logger.LogWarning("Chưa cấu hình template ZNS cho '{Key}' (Zalo:Templates).", templateKey);
            return false;
        }

        var accessToken = await tokens.GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning("Chưa có Zalo access token.");
            return false;
        }

        var body = new Dictionary<string, object>
        {
            ["phone"] = PhoneNumbers.ToZaloFormat(phone),
            ["template_id"] = templateId,
            ["template_data"] = data,
            ["tracking_id"] = trackingId
        };
        if (!string.IsNullOrWhiteSpace(opt.Mode)) body["mode"] = opt.Mode!;

        using var req = new HttpRequestMessage(HttpMethod.Post, opt.ApiUrl) { Content = JsonContent.Create(body) };
        req.Headers.Add("access_token", accessToken);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetInt32() : -1;
            if (error == 0) return true;
            var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : json;
            logger.LogWarning("Zalo ZNS lỗi {Error}: {Message}", error, message);
        }
        catch (JsonException)
        {
            logger.LogWarning("Zalo trả về dữ liệu lạ ({Status}): {Body}", (int)res.StatusCode, json);
        }
        return false;
    }
}

/// <summary>
/// Quản lý access token của Zalo OA. Token sống ~25 giờ; refresh token chỉ dùng được 1 lần rồi đổi cái mới,
/// nên token mới nhất luôn được lưu ra file App_Data/zalo-token.json (đã .gitignore).
/// Lưu ý: cách này giả định chỉ chạy 1 instance server. Chạy nhiều instance thì cần nơi lưu dùng chung (DB).
/// </summary>
public class ZaloTokenProvider(
    IHttpClientFactory httpFactory, IOptions<ZaloOptions> options, IClock clock,
    IHostEnvironment env, ILogger<ZaloTokenProvider> logger)
{
    private sealed record Stored(string AccessToken, string RefreshToken, DateTime ExpiresAt);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string FilePath => Path.Combine(env.ContentRootPath, "App_Data", "zalo-token.json");

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await GetCoreAsync(ct); }
        finally { _gate.Release(); }
    }

    private async Task<string?> GetCoreAsync(CancellationToken ct)
    {
        var opt = options.Value;
        var now = clock.Now;

        var token = await LoadAsync(ct);
        if (token is null)
        {
            if (string.IsNullOrWhiteSpace(opt.AccessToken) && string.IsNullOrWhiteSpace(opt.RefreshToken)) return null;
            token = new Stored(opt.AccessToken, opt.RefreshToken,
                string.IsNullOrWhiteSpace(opt.AccessToken) ? now : now.AddHours(23));
            await SaveAsync(token, ct);
        }

        if (!string.IsNullOrWhiteSpace(token.AccessToken) && token.ExpiresAt > now.AddMinutes(5))
            return token.AccessToken;

        if (string.IsNullOrWhiteSpace(token.RefreshToken) || string.IsNullOrWhiteSpace(opt.AppId))
            return token.AccessToken; // không thể gia hạn, thử dùng token hiện tại

        var http = httpFactory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, opt.OAuthUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["refresh_token"] = token.RefreshToken,
                ["app_id"] = opt.AppId,
                ["grant_type"] = "refresh_token"
            })
        };
        req.Headers.Add("secret_key", opt.AppSecret);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var at) || string.IsNullOrWhiteSpace(at.GetString()))
        {
            logger.LogError("Gia hạn Zalo token thất bại: {Body}", json);
            return token.AccessToken;
        }

        var expiresIn = 90000; // ~25 giờ
        if (root.TryGetProperty("expires_in", out var ex))
        {
            if (ex.ValueKind == JsonValueKind.Number) expiresIn = ex.GetInt32();
            else if (int.TryParse(ex.GetString(), out var parsed)) expiresIn = parsed;
        }

        var refresh = root.TryGetProperty("refresh_token", out var rt) && !string.IsNullOrWhiteSpace(rt.GetString())
            ? rt.GetString()! : token.RefreshToken;
        token = new Stored(at.GetString()!, refresh, now.AddSeconds(expiresIn));
        await SaveAsync(token, ct);
        return token.AccessToken;
    }

    private async Task<Stored?> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            await using var fs = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<Stored>(fs, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đọc được {Path}", FilePath);
            return null;
        }
    }

    private async Task SaveAsync(Stored token, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(token), ct);
    }
}
