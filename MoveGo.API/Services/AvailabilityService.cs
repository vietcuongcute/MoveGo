using Microsoft.EntityFrameworkCore;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;

namespace MoveGo.API.Services;

public interface IAvailabilityService
{
    /// <summary>Các xe còn trống trong khoảng [start, end).</summary>
    Task<List<Vehicle>> GetAvailableVehiclesAsync(DateTime start, DateTime end, VehicleCategory? category, CancellationToken ct = default);

    /// <summary>1 xe cụ thể có trống trong khoảng [start, end) không? excludeBookingId: bỏ qua chính đơn đang xét.</summary>
    Task<bool> IsAvailableAsync(int vehicleId, DateTime start, DateTime end, int? excludeBookingId = null, CancellationToken ct = default);

    /// <summary>Các khoảng bận của 1 xe (để vẽ lịch).</summary>
    Task<List<BusyRangeDto>> GetBusyRangesAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>
/// TRÁI TIM CỦA HỆ THỐNG: xe nào còn trống?
///
/// Hai khoảng [aStart, aEnd) và [bStart, bEnd) TRÙNG NHAU khi:  aStart &lt; bEnd  VÀ  aEnd &gt; bStart.
///
/// Một xe bị coi là BẬN trong khoảng khách chọn nếu có ít nhất một trong các thứ sau trùng khoảng đó:
///  1) Booking đã Confirmed (khách đã cọc).
///  2) Booking AwaitingDeposit mà thời gian giữ chỗ (HoldExpiresAt) CHƯA hết hạn.
///  3) Khoảng chủ xe tự khoá (VehicleUnavailability).
///
/// Booking PendingOwner (mới gửi yêu cầu) KHÔNG chặn lịch: nếu chặn thì một người nghịch gửi yêu cầu ảo
/// có thể "khoá" cả đội xe. Ai được chủ xe chấp nhận trước thì giữ chỗ trước.
/// </summary>
public class AvailabilityService(AppDbContext db, IClock clock) : IAvailabilityService
{
    public async Task<List<Vehicle>> GetAvailableVehiclesAsync(
        DateTime start, DateTime end, VehicleCategory? category, CancellationToken ct = default)
    {
        var now = clock.Now;

        var query = db.Vehicles.AsNoTracking().Where(v => v.IsActive && !v.IsDeleted);

        if (category.HasValue) query = query.Where(v => v.Category == category.Value);

        // Giữ lại xe KHÔNG có booking trùng và KHÔNG có lịch khoá trùng
        query = query.Where(v =>
            !v.Bookings.Any(b =>
                (b.Status == BookingStatus.Confirmed ||
                 (b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt > now))
                && b.StartDate < end && b.EndDate > start)
            && !v.Unavailabilities.Any(u => u.StartDate < end && u.EndDate > start));

        return await query.OrderBy(v => v.PricePerDay).ToListAsync(ct);
    }

    public async Task<bool> IsAvailableAsync(
        int vehicleId, DateTime start, DateTime end, int? excludeBookingId = null, CancellationToken ct = default)
    {
        var now = clock.Now;
        var exclude = excludeBookingId ?? -1;

        var bookingClash = await db.Bookings.AnyAsync(b =>
            b.VehicleId == vehicleId && b.Id != exclude
            && (b.Status == BookingStatus.Confirmed ||
                (b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt > now))
            && b.StartDate < end && b.EndDate > start, ct);
        if (bookingClash) return false;

        var blocked = await db.VehicleUnavailabilities.AnyAsync(u =>
            u.VehicleId == vehicleId && u.StartDate < end && u.EndDate > start, ct);
        return !blocked;
    }

    public async Task<List<BusyRangeDto>> GetBusyRangesAsync(int vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var now = clock.Now;

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.VehicleId == vehicleId
                && (b.Status == BookingStatus.Confirmed ||
                    (b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt > now))
                && b.StartDate < to && b.EndDate > from)
            .Select(b => new { b.StartDate, b.EndDate })
            .ToListAsync(ct);

        var blocks = await db.VehicleUnavailabilities.AsNoTracking()
            .Where(u => u.VehicleId == vehicleId && u.StartDate < to && u.EndDate > from)
            .Select(u => new { u.StartDate, u.EndDate })
            .ToListAsync(ct);

        return bookings.Select(b => new BusyRangeDto(b.StartDate, b.EndDate, "booked"))
            .Concat(blocks.Select(b => new BusyRangeDto(b.StartDate, b.EndDate, "blocked")))
            .OrderBy(r => r.From)
            .ToList();
    }
}
