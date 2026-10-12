using Microsoft.AspNetCore.Mvc;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

[ApiController]
[Route("api/booking")]
public class BookingController(IAvailabilityService availability) : ControllerBase
{
    private const int MaxRentalDays = 30;

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
        if ((end.Value - start.Value).TotalDays > MaxRentalDays)
            return Problem(title: $"Tối đa {MaxRentalDays} ngày cho mỗi đơn.", statusCode: 400);

        var vehicles = await availability.GetAvailableVehiclesAsync(start.Value, end.Value, category, location, ct);
        return vehicles.Select(v => VehicleMapper.ToDto(v, isAvailable: true)).ToList();
    }
}