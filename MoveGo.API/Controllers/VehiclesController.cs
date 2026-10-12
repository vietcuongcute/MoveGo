using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController(AppDbContext db, IAvailabilityService availability, IClock clock) : ControllerBase
{
    /// <summary>Danh sách toàn bộ xe đang cho thuê (chưa xét lịch).</summary>
    [HttpGet]
    public async Task<ActionResult<List<VehicleDto>>> GetAll(
        [FromQuery] VehicleCategory? category, [FromQuery] string? location, CancellationToken ct)
    {
        var query = db.Vehicles.AsNoTracking().Where(v => v.IsActive);
        if (category.HasValue) query = query.Where(v => v.Category == category.Value);
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(v => v.PickupLocation == location);

        var vehicles = await query.OrderBy(v => v.Category).ThenBy(v => v.PricePerDay).ToListAsync(ct);
        return vehicles.Select(v => VehicleMapper.ToDto(v)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleDto>> GetById(int id, CancellationToken ct)
    {
        var v = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        return v is null ? NotFound() : VehicleMapper.ToDto(v);
    }

    /// <summary>
    /// Các khoảng thời gian xe đang bận (đã đặt hoặc chủ xe khoá), dùng để vẽ lịch trống.
    /// Không lộ thông tin khách, chỉ trả khoảng thời gian và loại.
    /// </summary>
    [HttpGet("{id:int}/busy")]
    public async Task<ActionResult<List<BusyRangeDto>>> GetBusy(
        int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == id && v.IsActive, ct)) return NotFound();

        var start = from ?? clock.Now.Date;
        var end = to ?? start.AddDays(180);
        if (end <= start || (end - start).TotalDays > 400)
            return Problem(title: "Khoảng ngày không hợp lệ.", statusCode: 400);

        return await availability.GetBusyRangesAsync(id, start, end, ct);
    }
}