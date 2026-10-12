using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoveGo.API.Models;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

public record PaymentLink(long OrderCode, string CheckoutUrl, string QrCode);

public interface IPaymentService
{
    /// <summary>true = đang chạy giả lập (chưa có tài khoản PayOS).</summary>
    bool IsMock { get; }
    Task<PaymentLink> CreatePaymentLinkAsync(Booking booking, CancellationToken ct = default);
    Task<bool> IsPaidAsync(long orderCode, CancellationToken ct = default);
    bool VerifyWebhook(JsonElement payload);
}

/// <summary>Giả lập: không có tiền thật. Trang theo dõi đơn sẽ có nút "Giả lập đã thanh toán" (chỉ ở môi trường Development).</summary>
public class MockPaymentService(IClock clock, IOptions<BusinessOptions> business) : IPaymentService
{
    public bool IsMock => true;

    public Task<PaymentLink> CreatePaymentLinkAsync(Booking booking, CancellationToken ct = default)
    {
        var orderCode = PayOsService.NewOrderCode(clock.Now, booking.Id);
        var url = $"{business.Value.WebBaseUrl.TrimEnd('/')}/booking/{booking.Code}";
        return Task.FromResult(new PaymentLink(orderCode, url, $"MOCK-PAYMENT-{booking.Code}-{booking.CommissionAmount:0}"));
    }

    public Task<bool> IsPaidAsync(long orderCode, CancellationToken ct = default) => Task.FromResult(false);
    public bool VerifyWebhook(JsonElement payload) => false;
}

/// <summary>
/// Tích hợp PayOS (payos.vn) bằng HttpClient thuần để bạn thấy rõ cách hoạt động:
///   1) Tạo link thanh toán: POST /v2/payment-requests (ký HMAC-SHA256 bằng Checksum Key).
///   2) PayOS gọi webhook về server khi khách trả tiền; hoặc ta hỏi trạng thái: GET /v2/payment-requests/{orderCode}.
/// </summary>
public class PayOsService(HttpClient http, IOptions<PayOsOptions> options, IOptions<BusinessOptions> business, IClock clock)
    : IPaymentService
{
    public bool IsMock => false;

    public static long NewOrderCode(DateTime now, int bookingId) =>
        long.Parse($"{now:yyMMddHHmm}{bookingId % 10000:D4}");   // 14 chữ số, duy nhất theo đơn

    public async Task<PaymentLink> CreatePaymentLinkAsync(Booking booking, CancellationToken ct = default)
    {
        var opt = options.Value;
        var orderCode = NewOrderCode(clock.Now, booking.Id);
        var amount = (long)booking.CommissionAmount;
        var description = booking.Code;                                  // "MG-K7P2QX" (PayOS giới hạn độ dài mô tả)
        var back = $"{business.Value.WebBaseUrl.TrimEnd('/')}/booking/{booking.Code}";

        // Chuỗi ký: các trường sắp theo thứ tự chữ cái
        var signData = $"amount={amount}&cancelUrl={back}&description={description}&orderCode={orderCode}&returnUrl={back}";

        var body = new Dictionary<string, object?>
        {
            ["orderCode"] = orderCode,
            ["amount"] = amount,
            ["description"] = description,
            ["items"] = new[] { new { name = $"Cọc thuê {booking.Vehicle.Name}", quantity = 1, price = amount } },
            ["cancelUrl"] = back,
            ["returnUrl"] = back,
            ["signature"] = Sign(signData, opt.ChecksumKey)
        };
        if (booking.HoldExpiresAt is { } hold)
            body["expiredAt"] = new DateTimeOffset(DateTime.SpecifyKind(hold, DateTimeKind.Unspecified), TimeSpan.FromHours(7))
                .ToUnixTimeSeconds();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{opt.BaseUrl.TrimEnd('/')}/v2/payment-requests")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("x-client-id", opt.ClientId);
        req.Headers.Add("x-api-key", opt.ApiKey);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!(root.TryGetProperty("code", out var code) && code.GetString() == "00")
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            var desc = root.TryGetProperty("desc", out var d) ? d.GetString() : json;
            throw new InvalidOperationException($"PayOS từ chối tạo link thanh toán: {desc}");
        }

        return new PaymentLink(orderCode, data.GetProperty("checkoutUrl").GetString()!, data.GetProperty("qrCode").GetString() ?? "");
    }

    public async Task<bool> IsPaidAsync(long orderCode, CancellationToken ct = default)
    {
        var opt = options.Value;
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{opt.BaseUrl.TrimEnd('/')}/v2/payment-requests/{orderCode}");
        req.Headers.Add("x-client-id", opt.ClientId);
        req.Headers.Add("x-api-key", opt.ApiKey);

        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("data", out var data)
               && data.ValueKind == JsonValueKind.Object
               && data.TryGetProperty("status", out var status)
               && status.GetString() == "PAID";
    }

    /// <summary>Kiểm tra chữ ký webhook để chắc chắn tin nhắn thật sự do PayOS gửi.</summary>
    public bool VerifyWebhook(JsonElement payload)
    {
        if (!payload.TryGetProperty("signature", out var sigEl) || sigEl.GetString() is not { } signature) return false;
        if (!payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return false;

        var parts = data.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value.ValueKind switch
            {
                JsonValueKind.Null => "",
                JsonValueKind.String => p.Value.GetString(),
                _ => p.Value.GetRawText()
            }}");

        var expected = Sign(string.Join("&", parts), options.Value.ChecksumKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }

    private static string Sign(string data, string key)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
