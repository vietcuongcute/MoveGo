namespace MoveGo.API.Options;

public class BusinessOptions
{
    /// <summary>Tỉ lệ hoa hồng (cũng là tỉ lệ cọc). 0.10 = 10%.</summary>
    public decimal CommissionRate { get; set; } = 0.10m;

    /// <summary>Sau khi chủ xe chấp nhận, khách có bao nhiêu phút để cọc.</summary>
    public int DepositHoldMinutes { get; set; } = 30;

    /// <summary>Chủ xe có bao nhiêu giờ để trả lời yêu cầu.</summary>
    public int OwnerResponseHours { get; set; } = 12;

    /// <summary>Số ngày thuê tối đa cho mỗi đơn.</summary>
    public int MaxRentalDays { get; set; } = 30;

    /// <summary>Địa chỉ website MVC (để tạo link gửi qua Zalo, dùng ở các bước sau).</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:5200";

    /// <summary>Khoá bí mật cho API admin (header X-Admin-Key). Để trống = tắt admin API.</summary>
    public string AdminKey { get; set; } = "";
}