namespace MoveGo.API.Services;

/// <summary>Các "khoá nghiệp vụ" ↔ mẫu tin nhắn Zalo. Về sau mỗi khoá ứng với một template ZNS đã đăng ký.</summary>
public static class ZaloTemplates
{
    public const string OwnerNewRequest = "owner_new_request";     // Chủ xe: có khách muốn thuê, bấm link để xác nhận
    public const string CustomerAccepted = "customer_accepted";    // Khách: chủ xe đã đồng ý, bấm link để cọc
    public const string CustomerRejected = "customer_rejected";    // Khách: chủ xe không nhận được đơn
    public const string OwnerConfirmed = "owner_confirmed";        // Chủ xe: khách đã cọc, đây là SĐT khách
    public const string CustomerConfirmed = "customer_confirmed";  // Khách: đã cọc, đây là SĐT chủ xe
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
