using Microsoft.EntityFrameworkCore;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;

namespace MoveGo.API.Services;

/// <summary>
/// Cổng quản lý của chủ xe (truy cập bằng ManageToken bí mật, không cần đăng nhập):
/// xem lịch, tự KHOÁ ngày xe bận, tạm ngưng cho thuê. Đây chính là cách giữ lịch trống luôn chính xác.
/// </summary>
public class OwnerPortalService(AppDbContext db, IClock clock)
{
    public async Task<ServiceResult<OwnerDashboardDto>> GetDashboardAsync(string manageToken, CancellationToken ct = default)
    {
        var owner = await db.Owners.AsNoTracking().FirstOrDefaultAsync(o => o.ManageToken == manageToken, ct);
        if (owner is null) return ServiceResult<OwnerDashboardDto>.Fail(404, "Link quản lý không hợp lệ.");

        var now = clock.Now;
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.OwnerId == owner.Id).OrderBy(v => v.Name).ToListAsync(ct);
        var ids = vehicles.Select(v => v.Id).ToList();

        var blocks = await db.VehicleUnavailabilities.AsNoTracking()
            .Where(u => ids.Contains(u.VehicleId) && u.EndDate > now).OrderBy(u => u.StartDate).ToListAsync(ct);

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.VehicleId) && b.EndDate > now
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.AwaitingDeposit || b.Status == BookingStatus.PendingOwner))
            .OrderBy(b => b.StartDate).ToListAsync(ct);

        var result = vehicles.Select(v => new OwnerVehicleDto(
            v.Id, v.Name, v.PickupLocation, v.PricePerDay, v.IsActive,
            blocks.Where(b => b.VehicleId == v.Id).Select(b => new OwnerBlockDto(b.Id, b.StartDate, b.EndDate, b.Reason)).ToList(),
            bookings.Where(b => b.VehicleId == v.Id).Select(b => new OwnerBookingDto(
                b.Code, b.Status, BookingService.StatusText(b.Status), b.CustomerName,
                b.Status == BookingStatus.Confirmed ? b.CustomerPhone : null,
                b.StartDate, b.EndDate, b.OwnerAmount,
                b.Status == BookingStatus.PendingOwner ? b.OwnerToken : null)).ToList()
        )).ToList();

        return ServiceResult<OwnerDashboardDto>.Success(new OwnerDashboardDto(owner.Name, result));
    }

    public async Task<ServiceResult<OwnerBlockDto>> AddBlockAsync(
        string manageToken, int vehicleId, CreateBlockRequest req, CancellationToken ct = default)
    {
        var vehicle = await FindOwnedVehicleAsync(manageToken, vehicleId, ct);
        if (vehicle is null) return ServiceResult<OwnerBlockDto>.Fail(404, "Không tìm thấy xe của bạn.");

        if (req.StartDate is null || req.EndDate is null || req.EndDate <= req.StartDate)
            return ServiceResult<OwnerBlockDto>.Fail(400, "Khoảng ngày khoá không hợp lệ.");
        var start = req.StartDate.Value;
        var end = req.EndDate.Value;
        var now = clock.Now;
        if (end <= now) return ServiceResult<OwnerBlockDto>.Fail(400, "Không thể khoá ngày đã qua.");
        if ((end - start).TotalDays > 366) return ServiceResult<OwnerBlockDto>.Fail(400, "Mỗi lần khoá tối đa 1 năm.");

        // Không cho khoá đè lên chuyến khách đã cọc / đang giữ chỗ
        var clash = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b =>
            b.VehicleId == vehicleId
            && (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.AwaitingDeposit && b.HoldExpiresAt > now))
            && b.StartDate < end && b.EndDate > start, ct);
        if (clash is not null)
            return ServiceResult<OwnerBlockDto>.Fail(409, $"Khoảng này đã có khách đặt (mã {clash.Code}), bạn không thể khoá.");

        var reason = req.Reason?.Trim() ?? "";
        var block = new VehicleUnavailability
        {
            VehicleId = vehicleId, StartDate = start, EndDate = end,
            Reason = reason.Length > 200 ? reason[..200] : reason
        };
        db.VehicleUnavailabilities.Add(block);
        await db.SaveChangesAsync(ct);

        return ServiceResult<OwnerBlockDto>.Success(new OwnerBlockDto(block.Id, block.StartDate, block.EndDate, block.Reason));
    }

    public async Task<ServiceResult<bool>> RemoveBlockAsync(string manageToken, int blockId, CancellationToken ct = default)
    {
        var block = await db.VehicleUnavailabilities
            .FirstOrDefaultAsync(u => u.Id == blockId && u.Vehicle.Owner.ManageToken == manageToken, ct);
        if (block is null) return ServiceResult<bool>.Fail(404, "Không tìm thấy lịch khoá.");

        db.VehicleUnavailabilities.Remove(block);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> SetActiveAsync(string manageToken, int vehicleId, bool active, CancellationToken ct = default)
    {
        var vehicle = await FindOwnedVehicleAsync(manageToken, vehicleId, ct);
        if (vehicle is null) return ServiceResult<bool>.Fail(404, "Không tìm thấy xe của bạn.");
        vehicle.IsActive = active;
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(active);
    }

    private Task<Vehicle?> FindOwnedVehicleAsync(string manageToken, int vehicleId, CancellationToken ct) =>
        db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.Owner.ManageToken == manageToken, ct);
}
