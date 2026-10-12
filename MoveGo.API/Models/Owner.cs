using System.ComponentModel.DataAnnotations;

namespace MoveGo.API.Models;

/// <summary>Chủ xe: người nhận tin Zalo và nhận 90% tiền thuê.</summary>
public class Owner
{
    public int Id { get; set; }

    [MaxLength(120)] public string Name { get; set; } = "";

    [MaxLength(20)] public string PhoneNumber { get; set; } = "";

    /// <summary>SĐT đăng ký Zalo (nhận ZNS). Thường trùng với PhoneNumber.</summary>
    [MaxLength(20)] public string ZaloPhone { get; set; } = "";

    /// <summary>Mã bí mật để chủ xe vào trang quản lý lịch (khoá ngày xe bận).</summary>
    [MaxLength(64)] public string ManageToken { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public List<Vehicle> Vehicles { get; set; } = new();
}