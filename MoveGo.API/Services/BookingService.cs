using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>
/// Toàn bộ nghiệp vụ vòng đời một đơn thuê:
///   khách gửi yêu cầu → Zalo chủ xe → chủ xe chấp nhận (giữ chỗ) → khách cọc 10% → xác nhận → mở SĐT 2 bên.
/// </summary>
public class BookingService(
    AppDbContext db, IAvailabilityService availability, PricingService pricing,
    IZaloService zalo, IPaymentService payments, IClock clock,
    IOptions<BusinessOptions> business, ILogger<BookingService> logger)
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
            PricePerDayAtBooking = vehicle.PricePerDay,
            TotalAmount = quote.TotalAmount,
            CommissionAmount = quote.CommissionAmount,   // 10% — cũng là tiền cọc
            OwnerAmount = quote.OwnerAmount,             // 90% — khách trả trực tiếp cho chủ xe
            Status = BookingStatus.PendingOwner,
            OwnerToken = Tokens.UrlToken(),
            CreatedAt = now
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        // Gửi Zalo cho chủ xe. Nếu Zalo lỗi, đơn vẫn được lưu (admin có thể gửi lại).
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
    // 2. KHÁCH THEO DÕI ĐƠN
    // =====================================================================
    public async Task<ServiceResult<BookingTrackDto>> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(b => b.Code == code.ToUpperInvariant(), ct);
        if (booking is null) return Fail<BookingTrackDto>(404, "Không tìm thấy đơn này.");

        await RefreshStateAsync(booking, ct);
        return ServiceResult<BookingTrackDto>.Success(ToTrackDto(booking));
    }

    // =====================================================================
    // 3. CHỦ XE XEM / CHẤP NHẬN / TỪ CHỐI (link trong tin Zalo)
    // =====================================================================
    public async Task<ServiceResult<OwnerRequestDto>> GetOwnerRequestAsync(string token, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(b => b.OwnerToken == token, ct);
        if (booking is null) return Fail<OwnerRequestDto>(404, "Link không hợp lệ hoặc đã hết hạn.");

        await RefreshStateAsync(booking, ct);
        return ServiceResult<OwnerRequestDto>.Success(await ToOwnerDtoAsync(booking, ct));
    }

    public async Task<ServiceResult<OwnerRequestDto>> OwnerRespondAsync(
        string token, bool accept, string? reason, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(b => b.OwnerToken == token, ct);
        if (booking is null) return Fail<OwnerRequestDto>(404, "Link không hợp lệ hoặc đã hết hạn.");

        // Khoá theo xe: 2 chủ xe/2 tab bấm "Chấp nhận" cùng lúc cho cùng 1 xe sẽ được xử lý lần lượt, không bị đặt trùng.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockVehicleAsync(booking.VehicleId, ct);
        await db.Entry(booking).ReloadAsync(ct);

        var now = clock.Now;

        if (booking.Status != BookingStatus.PendingOwner)
        {
            await tx.RollbackAsync(ct);
            return Fail<OwnerRequestDto>(409, "Đơn này đã được xử lý trước đó.");
        }

        if (booking.CreatedAt.AddHours(Biz.OwnerResponseHours) <= now || booking.EndDate <= now)
        {
            booking.Status = BookingStatus.Expired;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Fail<OwnerRequestDto>(410, "Yêu cầu này đã hết hạn.");
        }

        // ---- Từ chối ----
        if (!accept)
        {
            booking.Status = BookingStatus.Rejected;
            booking.OwnerRespondedAt = now;
            booking.RejectReason = Trim(reason, 300);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await NotifyCustomerRejectedAsync(booking, ct);
            return ServiceResult<OwnerRequestDto>.Success(await ToOwnerDtoAsync(booking, ct));
        }

        // ---- Chấp nhận: kiểm tra lại xe còn trống (có thể đã có người giữ chỗ trước) ----
        if (!await availability.IsAvailableAsync(booking.VehicleId, booking.StartDate, booking.EndDate, booking.Id, ct))
        {
            booking.Status = BookingStatus.Rejected;
            booking.OwnerRespondedAt = now;
            booking.RejectReason = "Xe vừa được khách khác giữ chỗ trong thời gian này.";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await NotifyCustomerRejectedAsync(booking, ct);
            return Fail<OwnerRequestDto>(409, "Xe đã có lịch trong khoảng thời gian này nên không thể nhận đơn (đơn đã tự đóng).");
        }

        booking.Status = BookingStatus.AwaitingDeposit;
        booking.OwnerRespondedAt = now;
        booking.HoldExpiresAt = now.AddMinutes(Biz.DepositHoldMinutes);

        try
        {
            var link = await payments.CreatePaymentLinkAsync(booking, ct);
            booking.PayOsOrderCode = link.OrderCode;
            booking.CheckoutUrl = link.CheckoutUrl;
            booking.QrCode = link.QrCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không tạo được link thanh toán cho đơn {Code}", booking.Code);
            await tx.RollbackAsync(ct);
            return Fail<OwnerRequestDto>(502, "Không tạo được link thanh toán, vui lòng thử lại sau ít phút.");
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await SafeSendAsync(booking.CustomerPhone, ZaloTemplates.CustomerAccepted, booking.Code, new()
        {
            ["customer_name"] = booking.CustomerName,
            ["vehicle_name"] = booking.Vehicle.Name,
            ["deposit_amount"] = Fmt.Money(booking.CommissionAmount),
            ["hold_minutes"] = Biz.DepositHoldMinutes.ToString(),
            ["order_code"] = booking.Code,
            ["pay_link"] = TrackLink(booking)
        }, ct);

        return ServiceResult<OwnerRequestDto>.Success(await ToOwnerDtoAsync(booking, ct));
    }

    // =====================================================================
    // 4. KHÁCH ĐÃ CỌC → XÁC NHẬN → MỞ SĐT 2 BÊN
    // =====================================================================
    public async Task<ServiceResult<BookingTrackDto>> MarkPaidAsync(Booking booking, CancellationToken ct = default)
    {
        if (booking.Status is BookingStatus.Confirmed or BookingStatus.Completed)
            return ServiceResult<BookingTrackDto>.Success(ToTrackDto(booking)); // webhook gọi lặp → bỏ qua

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockVehicleAsync(booking.VehicleId, ct);
        await db.Entry(booking).ReloadAsync(ct);

        if (booking.Status is BookingStatus.Confirmed or BookingStatus.Completed)
        {
            await tx.RollbackAsync(ct);
            return ServiceResult<BookingTrackDto>.Success(ToTrackDto(booking));
        }

        // Khách trả tiền sau khi hết hạn giữ chỗ: vẫn xác nhận nếu xe còn trống, nếu không thì đánh dấu để hoàn cọc.
        var canConfirm = booking.Status == BookingStatus.AwaitingDeposit
            || (booking.Status == BookingStatus.Expired
                && await availability.IsAvailableAsync(booking.VehicleId, booking.StartDate, booking.EndDate, booking.Id, ct));

        booking.PaidAt = clock.Now;
        if (canConfirm) booking.Status = BookingStatus.Confirmed;
        else booking.AdminNote = $"[{Fmt.DateTimeVn(clock.Now)}] Đã nhận cọc nhưng đơn ở trạng thái {booking.Status} và xe không còn trống → CẦN HOÀN CỌC cho khách.";

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (!canConfirm)
        {
            logger.LogWarning("Đơn {Code} đã nhận cọc nhưng không thể xác nhận — cần hoàn cọc.", booking.Code);
            return Fail<BookingTrackDto>(409, "Đã nhận cọc nhưng xe không còn trống. Move-Go sẽ liên hệ hoàn tiền cho bạn.");
        }

        await SafeSendAsync(booking.Vehicle.Owner.ZaloPhone, ZaloTemplates.OwnerConfirmed, booking.Code, new()
        {
            ["order_code"] = booking.Code,
            ["customer_name"] = booking.CustomerName,
            ["customer_phone"] = booking.CustomerPhone,
            ["vehicle_name"] = booking.Vehicle.Name,
            ["start_date"] = Fmt.DateTimeVn(booking.StartDate),
            ["end_date"] = Fmt.DateTimeVn(booking.EndDate),
            ["owner_amount"] = Fmt.Money(booking.OwnerAmount)
        }, ct);

        await SafeSendAsync(booking.CustomerPhone, ZaloTemplates.CustomerConfirmed, booking.Code, new()
        {
            ["order_code"] = booking.Code,
            ["customer_name"] = booking.CustomerName,
            ["owner_name"] = booking.Vehicle.Owner.Name,
            ["owner_phone"] = booking.Vehicle.Owner.PhoneNumber,
            ["vehicle_name"] = booking.Vehicle.Name,
            ["pickup_address"] = booking.Vehicle.PickupAddress,
            ["owner_amount"] = Fmt.Money(booking.OwnerAmount)
        }, ct);

        return ServiceResult<BookingTrackDto>.Success(ToTrackDto(booking));
    }

    public async Task<ServiceResult<BookingTrackDto>> MarkPaidByOrderCodeAsync(long orderCode, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(b => b.PayOsOrderCode == orderCode, ct);
        if (booking is null) return Fail<BookingTrackDto>(404, "Không có đơn nào ứng với mã thanh toán này.");
        return await MarkPaidAsync(booking, ct);
    }

    public async Task<ServiceResult<BookingTrackDto>> MockPayAsync(string code, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(b => b.Code == code.ToUpperInvariant(), ct);
        if (booking is null) return Fail<BookingTrackDto>(404, "Không tìm thấy đơn này.");
        if (booking.Status != BookingStatus.AwaitingDeposit)
            return Fail<BookingTrackDto>(409, "Đơn này chưa ở bước chờ đặt cọc.");
        return await MarkPaidAsync(booking, ct);
    }

    // =====================================================================
    // 5. DỌN DẸP ĐƠN QUÁ HẠN (gọi mỗi phút bởi BookingExpiryWorker)
    // =====================================================================
    public async Task<int> ExpireStaleAsync(CancellationToken ct = default)
    {
        var now = clock.Now;
        var changed = 0;

        // a) Quá hạn giữ chỗ chờ cọc. Trước khi huỷ, hỏi PayOS xem khách có lỡ trả tiền chưa.
        var holds = await db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v.Owner)
            .Where(b => b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt <= now).ToListAsync(ct);
        foreach (var b in holds)
        {
            if (!payments.IsMock && b.PayOsOrderCode is { } oc && await SafeIsPaidAsync(oc, ct))
            {
                await MarkPaidAsync(b, ct);
            }
            else
            {
                b.Status = BookingStatus.Expired;
                changed++;
            }
        }

        // b) Chủ xe không trả lời quá lâu, hoặc đã quá giờ nhận xe
        var limit = now.AddHours(-Biz.OwnerResponseHours);
        foreach (var b in await db.Bookings
                     .Where(b => b.Status == BookingStatus.PendingOwner && (b.CreatedAt <= limit || b.EndDate <= now))
                     .ToListAsync(ct))
        {
            b.Status = BookingStatus.Expired;
            changed++;
        }

        // c) Chuyến đã kết thúc
        foreach (var b in await db.Bookings
                     .Where(b => b.Status == BookingStatus.Confirmed && b.EndDate <= now).ToListAsync(ct))
        {
            b.Status = BookingStatus.Completed;
            changed++;
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>Gửi lại tin Zalo cho chủ xe (admin dùng khi tin đầu bị lỗi).</summary>
    public async Task<ServiceResult<bool>> ResendOwnerNotificationAsync(int bookingId, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(b => b.Id == bookingId, ct);
        if (booking is null) return Fail<bool>(404, "Không tìm thấy đơn.");
        if (booking.Status != BookingStatus.PendingOwner) return Fail<bool>(409, "Đơn không còn ở trạng thái chờ chủ xe.");

        var sent = await SafeSendAsync(booking.Vehicle.Owner.ZaloPhone, ZaloTemplates.OwnerNewRequest, booking.Code, new()
        {
            ["customer_name"] = booking.CustomerName,
            ["vehicle_name"] = booking.Vehicle.Name,
            ["start_date"] = Fmt.DateTimeVn(booking.StartDate),
            ["end_date"] = Fmt.DateTimeVn(booking.EndDate),
            ["total_amount"] = Fmt.Money(booking.TotalAmount),
            ["owner_amount"] = Fmt.Money(booking.OwnerAmount),
            ["order_code"] = booking.Code,
            ["confirm_link"] = OwnerConfirmLink(booking)
        }, ct);

        if (sent) { booking.OwnerNotifiedAt = clock.Now; await db.SaveChangesAsync(ct); }
        return ServiceResult<bool>.Success(sent);
    }

    // =====================================================================
    // Hàm hỗ trợ
    // =====================================================================
    private Task<Booking?> LoadBookingAsync(System.Linq.Expressions.Expression<Func<Booking, bool>> predicate, CancellationToken ct) =>
        db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v.Owner).FirstOrDefaultAsync(predicate, ct);

    /// <summary>pg_advisory_xact_lock: khoá nhẹ của PostgreSQL, tự nhả khi transaction kết thúc.</summary>
    private Task LockVehicleAsync(int vehicleId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({(long)vehicleId})", ct);

    /// <summary>Cập nhật trạng thái "theo thời gian": hết hạn, hoặc kiểm tra PayOS xem khách đã trả tiền chưa.</summary>
    private async Task RefreshStateAsync(Booking b, CancellationToken ct)
    {
        var now = clock.Now;

        if (b.Status == BookingStatus.AwaitingDeposit && !payments.IsMock && b.PayOsOrderCode is { } oc
            && await SafeIsPaidAsync(oc, ct))
        {
            await MarkPaidAsync(b, ct);
            return;
        }

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

    private async Task<bool> SafeIsPaidAsync(long orderCode, CancellationToken ct)
    {
        try { return await payments.IsPaidAsync(orderCode, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Không kiểm tra được trạng thái thanh toán {Order}", orderCode); return false; }
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

    private Task NotifyCustomerRejectedAsync(Booking b, CancellationToken ct) =>
        SafeSendAsync(b.CustomerPhone, ZaloTemplates.CustomerRejected, b.Code, new()
        {
            ["customer_name"] = b.CustomerName,
            ["vehicle_name"] = b.Vehicle.Name,
            ["order_code"] = b.Code,
            ["search_link"] = Biz.WebBaseUrl.TrimEnd('/') + "/xe"
        }, ct);

    private string OwnerConfirmLink(Booking b) => $"{Biz.WebBaseUrl.TrimEnd('/')}/owner/confirm/{b.OwnerToken}";
    private string TrackLink(Booking b) => $"{Biz.WebBaseUrl.TrimEnd('/')}/booking/{b.Code}";

    private static string? Trim(string? s, int max)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return s.Length > max ? s[..max] : s;
    }

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

    private BookingTrackDto ToTrackDto(Booking b)
    {
        var revealed = b.Status is BookingStatus.Confirmed or BookingStatus.Completed;
        var owner = b.Vehicle.Owner;
        var awaiting = b.Status == BookingStatus.AwaitingDeposit;

        int? secondsLeft = awaiting && b.HoldExpiresAt is { } hold
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
            awaiting ? b.CheckoutUrl : null, awaiting ? b.QrCode : null, payments.IsMock,
            revealed ? new OwnerContactDto(owner.Name, owner.PhoneNumber, $"https://zalo.me/{owner.ZaloPhone}") : null,
            b.RejectReason);
    }

    private async Task<OwnerRequestDto> ToOwnerDtoAsync(Booking b, CancellationToken ct)
    {
        var canRespond = b.Status == BookingStatus.PendingOwner;
        var stillAvailable = !canRespond || await availability.IsAvailableAsync(b.VehicleId, b.StartDate, b.EndDate, b.Id, ct);
        var revealed = b.Status is BookingStatus.Confirmed or BookingStatus.Completed;

        return new OwnerRequestDto(
            b.Code, b.Status, StatusText(b.Status), canRespond, stillAvailable,
            b.Vehicle.Name, b.Vehicle.Category, b.Vehicle.ImageUrl,
            b.CustomerName, b.CustomerNote, revealed ? b.CustomerPhone : null,
            b.StartDate, b.EndDate, b.RentalDays,
            b.TotalAmount, b.OwnerAmount, b.CommissionAmount,
            b.Vehicle.PickupLocation, Biz.DepositHoldMinutes);
    }
}
