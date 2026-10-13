namespace MoveGo.API.Options;

public class BusinessOptions
{
    /// <summary>Tỉ lệ hoa hồng (cũng là tỉ lệ cọc). 0.10 = 10%.</summary>
    public decimal CommissionRate { get; set; } = 0.10m;

    /// <summary>Sau khi chủ xe chấp nhận, khách có bao nhiêu phút để cọc.</summary>
    public int DepositHoldMinutes { get; set; } = 30;

    /// <summary>Chủ xe có bao nhiêu giờ để trả lời yêu cầu.</summary>
    public int OwnerResponseHours { get; set; } = 12;

    public int MaxRentalDays { get; set; } = 30;

    /// <summary>
    /// Cho phép nút "giả lập đã thanh toán" ngoài môi trường Development (chỉ có tác dụng khi PayOS CHƯA bật).
    /// Dùng làm phương án dự phòng khi demo; để false khi chạy thật.
    /// </summary>
    public bool AllowMockPayment { get; set; }

    /// <summary>Địa chỉ website MVC (để tạo link gửi qua Zalo).</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:5200";

    /// <summary>Khoá bí mật cho API admin (header X-Admin-Key). Để trống = tắt admin API.</summary>
    public string AdminKey { get; set; } = "";
}

public class ZaloOptions
{
    /// <summary>false = chỉ ghi log (chế độ giả lập), không gửi tin thật.</summary>
    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = "https://business.openapi.zalo.me/message/template";
    public string OAuthUrl { get; set; } = "https://oauth.zaloapp.com/v4/oa/access_token";
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    /// <summary>Token ban đầu lấy từ Zalo (sau đó hệ thống tự gia hạn và lưu DB).</summary>
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    /// <summary>Đặt "development" để thử template chưa duyệt.</summary>
    public string? Mode { get; set; }
    /// <summary>Khoá nghiệp vụ → template_id đã được Zalo duyệt.</summary>
    public Dictionary<string, string> Templates { get; set; } = new();
}

public class PayOsOptions
{
    /// <summary>false = giả lập thanh toán (có nút "đã thanh toán" để test).</summary>
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
}

/// <summary>
/// Nhận cọc bằng chuyển khoản ngân hàng THẬT vào tài khoản của bạn (VietQR) và tự nhận diện tiền về
/// qua webhook của SePay (sepay.vn) hoặc Casso (casso.vn). Không cần PayOS.
/// </summary>
public class BankPaymentOptions
{
    public bool Enabled { get; set; }
    /// <summary>Mã BIN ngân hàng (6 số): MB 970422, Vietcombank 970436, Techcombank 970407, ACB 970416, BIDV 970418, VietinBank 970415, VPBank 970432, TPBank 970423, Sacombank 970403...</summary>
    public string BankBin { get; set; } = "";
    /// <summary>Tên hiển thị cho khách, vd "MB Bank".</summary>
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    /// <summary>Tên chủ tài khoản (in hoa, không dấu), vd "NGUYEN VAN A".</summary>
    public string AccountName { get; set; } = "";
    /// <summary>Khoá bí mật của webhook: SePay "API Key" hoặc Casso "secure-token". Trống = từ chối mọi webhook.</summary>
    public string WebhookSecret { get; set; } = "";
}
