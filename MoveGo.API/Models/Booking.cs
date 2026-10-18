using System.ComponentModel.DataAnnotations;

namespace MoveGo.API.Models;

public class Booking
{
    public int Id { get; set; }

    /// <summary>Mã đơn hiển thị cho khách, ví dụ MG-K7P2QX. Cũng là "chìa khoá" xem trạng thái đơn.</summary>
    [MaxLength(16)] public string Code { get; set; } = "";

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    [MaxLength(100)] public string CustomerName { get; set; } = "";
    [MaxLength(20)] public string CustomerPhone { get; set; } = "";
    [MaxLength(500)] public string? CustomerNote { get; set; }

    /// <summary>Địa chỉ khách muốn nhận xe (khách tự nhập khi đặt). Khách và chủ xe tự thoả thuận giờ giao.</summary>
    [MaxLength(200)] public string PickupAddress { get; set; } = "";

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int RentalDays { get; set; }

    /// <summary>Lưu lại giá/ngày tại thời điểm đặt (chủ xe đổi giá sau này không ảnh hưởng đơn cũ).</summary>
    public decimal PricePerDayAtBooking { get; set; }


    /// <summary>Tổng tiền thuê (đã gồm dịch vụ cộng thêm).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Hoa hồng MoveGo = 10% tổng, cũng chính là tiền cọc khách trả online.</summary>
    public decimal CommissionAmount { get; set; }

    /// <summary>Phần còn lại (90%) khách trả trực tiếp cho chủ xe khi nhận xe.</summary>
    public decimal OwnerAmount { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.PendingOwner;

    /// <summary>Token bí mật nằm trong link Zalo gửi cho chủ xe.</summary>
    [MaxLength(64)] public string OwnerToken { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime? OwnerNotifiedAt { get; set; }

    /// <summary>Lúc gửi thành công tin Zalo "khách đã cọc, đây là SĐT khách" cho chủ xe (để chủ xe chủ động nhắn khách). Null = chưa báo được.</summary>
    public DateTime? PaidNotifiedAt { get; set; }
    public DateTime? OwnerRespondedAt { get; set; }
    [MaxLength(300)] public string? RejectReason { get; set; }

    /// <summary>Hạn chót khách phải cọc. Quá hạn → Expired và xe được mở lại.</summary>
    public DateTime? HoldExpiresAt { get; set; }

    // --- Thanh toán cọc (PayOS), sẽ dùng ở bước sau ---
    public long? PayOsOrderCode { get; set; }
    [MaxLength(500)] public string? CheckoutUrl { get; set; }
    [MaxLength(2000)] public string? QrCode { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Ghi chú nội bộ cho admin (VD: cần hoàn cọc).</summary>
    [MaxLength(500)] public string? AdminNote { get; set; }
}