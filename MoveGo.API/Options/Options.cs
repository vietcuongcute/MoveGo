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
