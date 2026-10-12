using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoveGo.API.Data;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Options;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

[ApiController]
[Route("api/booking")]
public class BookingController(
    AppDbContext db, IAvailabilityService availability, PricingService pricing,
    IOptions<BusinessOptions> business) : ControllerBase
{
    /// <summary>
    /// Danh sách xe CÒN TRỐNG trong khoảng ngày khách chọn.
    /// Ví dụ: GET /api/booking/available-vehicles?start=2026-10-20T08:00:00&amp;end=2026-10-22T08:00:00&amp;category=Car4Seats&amp;location=Hà Nội
    /// </summary>
    [HttpGet("available-vehicles")]
    public async Task<ActionResult<List<VehicleDto>>> GetAvailableVehicles(
        [FromQuery] DateTime? start, [FromQuery] DateTime? end,
        [FromQuery] VehicleCategory? category, [FromQuery] string? location, CancellationToken ct)
    {
        if (start is null || end is null)
            return Problem(title: "Thiếu ngày nhận hoặc ngày trả xe.", statusCode: 400);
        if (end <= start)
            return Problem(title: "Ngày trả xe phải sau ngày nhận xe.", statusCode: 400);
        if (PricingService.CountDays(start.Value, end.Value) > business.Value.MaxRentalDays)
            return Problem(title: $"Tối đa {business.Value.MaxRentalDays} ngày cho mỗi đơn.", statusCode: 400);

        var vehicles = await availability.GetAvailableVehiclesAsync(start.Value, end.Value, category, location, ct);
        return vehicles.Select(v => VehicleMapper.ToDto(v, isAvailable: true)).ToList();
    }

    /// <summary>Báo giá (không tạo đơn): tổng tiền, tiền cọc 10% cho Move-Go, phần trả thẳng chủ xe.</summary>
    [HttpPost("quote")]
    public async Task<IActionResult> Quote([FromBody] QuoteRequest req, CancellationToken ct)
    {
        if (req.StartDate is null || req.EndDate is null || req.EndDate <= req.StartDate)
            return Problem(title: "Khoảng ngày thuê không hợp lệ.", statusCode: 400);
        if (PricingService.CountDays(req.StartDate.Value, req.EndDate.Value) > business.Value.MaxRentalDays)
            return Problem(title: $"Tối đa {business.Value.MaxRentalDays} ngày cho mỗi đơn.", statusCode: 400);

        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == req.VehicleId && v.IsActive, ct);
        if (vehicle is null) return Problem(title: "Không tìm thấy xe.", statusCode: 404);

        return Ok(pricing.Calculate(vehicle.PricePerDay, req.StartDate.Value, req.EndDate.Value));
    }
}