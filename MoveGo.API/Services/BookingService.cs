using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>
/// Nghiệp vụ vòng đời một đơn thuê:
///   khách gửi yêu cầu → Zalo chủ xe → chủ xe chấp nhận (giữ chỗ) → khách cọc 10% → xác nhận → mở SĐT 2 bên.
/// Bước 5 mới làm 2 việc đầu: tạo đơn + xem trạng thái đơn.
/// </summary>
public class BookingService(
    AppDbContext db, IAvailabilityService availability, PricingService pricing,
    IZaloService zalo, IClock clock, IOptions<BusinessOptions> business, ILogger<BookingService> logger)
{
    private BusinessOptions Biz => business.Value;

    // =====================================================================
    // 1. KHÁCH GỬI YÊU CẦU
    // =====================================================================
    public async Task<ServiceResult<BookingTrackDto>> CreateAsync(CreateBookingRequest req, CancellationToken ct = default)
    {
        var now = clock.Now;

        var name = req.CustomerName?.Trim() ?? "";
        if (name.Length < 2 || name.Length > 100)
            return Fail<BookingTrackDto>(400, "Vui lòng nhập họ tên (2–100 ký tự).");

        var phone = PhoneNumbers.Normalize(req.CustomerPhone);
        if (phone is null)
            return Fail<BookingTrackDto>(400, "Số điện thoại không hợp lệ (ví dụ: 0912345678).");

        if (req.StartDate is null || req.EndDate is null)
            return Fail<BookingTrackDto>(400, "Vui lòng chọn ngày nhận và ngày trả xe.");
        var start = req.StartDate.Value;
        var end = req.EndDate.Value;
        if (end <= start) return Fail<BookingTrackDto>(400, "Ngày trả xe phải sau ngày nhận xe.");
        if (start < now.AddMinutes(-5)) return Fail<BookingTrackDto>(400, "Thời gian nhận xe đã qua, vui lòng chọn lại.");
        if (PricingService.CountDays(start, end) > Biz.MaxRentalDays)
            return Fail<BookingTrackDto>(400, $"Mỗi đơn tối đa {Biz.MaxRentalDays} ngày.");

        var vehicle = await db.Vehicles.Include(v => v.Owner)
            .FirstOrDefaultAsync(v => v.Id == req.VehicleId && v.IsActive, ct);
        if (vehicle is null) return Fail<BookingTrackDto>(404, "Không tìm thấy xe này.");

        if (!await availability.IsAvailableAsync(vehicle.Id, start, end, null, ct))
            return Fail<BookingTrackDto>(409, "Rất tiếc, xe đã có lịch trong khoảng thời gian này. Vui lòng chọn ngày hoặc xe khác.");

        // Chống spam: 1 SĐT không gửi trùng yêu cầu cho cùng 1 xe, và tối đa 5 yêu cầu đang chờ.
        if (await db.Bookings.AnyAsync(b => b.VehicleId == vehicle.Id && b.CustomerPhone == phone
                                            && b.Status == BookingStatus.PendingOwner && b.EndDate > now, ct))
            return Fail<BookingTrackDto>(409, "Bạn đã gửi yêu cầu cho xe này rồi, chủ xe đang xem xét.");
        if (await db.Bookings.CountAsync(b => b.CustomerPhone == phone && b.Status == BookingStatus.PendingOwner, ct) >= 5)
            return Fail<BookingTrackDto>(429, "Bạn đang có quá nhiều yêu cầu chờ xác nhận. Vui lòng đợi chủ xe phản hồi.");

        // Server luôn tự tính tiền, không tin con số nào từ client gửi lên.
        var quote = pricing.Calculate(vehicle.PricePerDay, start, end);

        string code;
        do { code = Tokens.BookingCode(); } while (await db.Bookings.AnyAsync(b => b.Code == code, ct));

        var note = req.CustomerNote?.Trim();
        var booking = new Booking
        {
            Code = code,
            Vehicle = vehicle,
            CustomerName = name,
            CustomerPhone = phone,
            CustomerNote = string.IsNullOrEmpty(note) ? null : (note.Length > 500 ? note[..500] : note),
            StartDate = start,
            EndDate = end,
            RentalDays = quote.Days,
            PricePerDayAtBooking = vehicle.PricePerDay,   // chủ xe đổi giá sau này không ảnh hưởng đơn cũ
            TotalAmount = quote.TotalAmount,
            CommissionAmount = quote.CommissionAmount,    // 10%: tiền cọc online, cũng là hoa hồng Move-Go
            OwnerAmount = quote.OwnerAmount,              // phần còn lại: khách trả thẳng chủ xe
            Status = BookingStatus.PendingOwner,
            OwnerToken = Tokens.UrlToken(),
            CreatedAt = now
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        // Gửi Zalo cho chủ xe. Nếu Zalo lỗi, đơn vẫn được lưu (không làm khách mất đơn).
        var sent = await SafeSendAsync(vehicle.Owner.ZaloPhone, ZaloTemplates.OwnerNewRequest, booking.Code, new()
        {
            ["customer_name"] = booking.CustomerName,
            ["vehicle_name"] = vehicle.Name,
            ["start_date"] = Fmt.DateTimeVn(booking.StartDate),
            ["end_date"] = Fmt.DateTimeVn(booking.EndDate),
            ["total_amount"] = Fmt.Money(booking.TotalAmount),
            ["owner_amount"] = Fmt.Money(booking.OwnerAmount),
            ["order_code"] = booking.Code,
            ["confirm_link"] = OwnerConfirmLink(booking)
        }, ct);

        if (sent)
        {
            booking.OwnerNotifiedAt = clock.Now;
            await db.SaveChangesAsync(ct);
        }

        return ServiceResult<BookingTrackDto>.Success(ToTrackDto(booking));
    }

    // =====================================================================
    // 2. KHÁCH THEO DÕI ĐƠN BẰNG MÃ ĐƠN
    // =====================================================================
    public async Task<ServiceResult<BookingTrackDto>> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var booking = await db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v.Owner)
            .FirstOrDefaultAsync(b => b.Code == normalized, ct);
        if (booking is null) return Fail<BookingTrackDto>(404, "Không tìm thấy đơn này.");

        await RefreshStateAsync(booking, ct);
        return ServiceResult<BookingTrackDto>.Success(ToTrackDto(booking));
    }

    // =====================================================================
    // Hàm hỗ trợ
    // =====================================================================

    /// <summary>Cập nhật trạng thái "theo thời gian": quá hạn chủ xe trả lời, hoặc quá hạn giữ chỗ chờ cọc.</summary>
    private async Task RefreshStateAsync(Booking b, CancellationToken ct)
    {
        var now = clock.Now;

        if (b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt <= now)
        {
            b.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
        }
        else if (b.Status == BookingStatus.PendingOwner &&
                 (b.CreatedAt.AddHours(Biz.OwnerResponseHours) <= now || b.EndDate <= now))
        {
            b.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<bool> SafeSendAsync(string phone, string templateKey, string trackingId,
                                           Dictionary<string, string> data, CancellationToken ct)
    {
        try { return await zalo.SendAsync(phone, templateKey, data, trackingId, ct); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gửi Zalo '{Template}' tới {Phone} thất bại", templateKey, phone);
            return false;
        }
    }

    private string OwnerConfirmLink(Booking b) => $"{Biz.WebBaseUrl.TrimEnd('/')}/owner/confirm/{b.OwnerToken}";

    private static ServiceResult<T> Fail<T>(int status, string message) => ServiceResult<T>.Fail(status, message);

    public static string StatusText(BookingStatus s) => s switch
    {
        BookingStatus.PendingOwner => "Đang chờ chủ xe xác nhận",
        BookingStatus.AwaitingDeposit => "Chủ xe đã đồng ý — vui lòng đặt cọc",
        BookingStatus.Confirmed => "Đã xác nhận",
        BookingStatus.Rejected => "Chủ xe không thể nhận đơn",
        BookingStatus.Cancelled => "Đã huỷ",
        BookingStatus.Expired => "Đã hết hạn",
        BookingStatus.Completed => "Đã hoàn tất",
        _ => s.ToString()
    };

    /// <summary>Chuyển Booking → DTO cho khách. QUY TẮC RIÊNG TƯ: chỉ khi đã cọc mới lộ SĐT chủ xe + địa chỉ nhận xe.</summary>
    private BookingTrackDto ToTrackDto(Booking b)
    {
        var revealed = b.Status is BookingStatus.Confirmed or BookingStatus.Completed;
        var owner = b.Vehicle.Owner;

        int? secondsLeft = b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt is { } hold
            ? Math.Max(0, (int)(hold - clock.Now).TotalSeconds)
            : null;

        return new BookingTrackDto(
            b.Code, b.Status, StatusText(b.Status),
            b.Vehicle.Name, b.Vehicle.Category, VehicleMapper.CategoryLabel(b.Vehicle.Category), b.Vehicle.ImageUrl,
            b.Vehicle.PickupLocation, revealed ? b.Vehicle.PickupAddress : null,
            b.StartDate, b.EndDate, b.RentalDays,
            b.TotalAmount, b.CommissionAmount, b.OwnerAmount,
            b.CustomerName, b.CreatedAt,
            secondsLeft,
            revealed ? new OwnerContactDto(owner.Name, owner.PhoneNumber, $"https://zalo.me/{owner.ZaloPhone}") : null,
            b.RejectReason);
    }
}
