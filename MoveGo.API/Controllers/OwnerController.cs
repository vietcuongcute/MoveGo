using Microsoft.AspNetCore.Mvc;
using MoveGo.API.Dtos;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

/// <summary>
/// Các API dành cho chủ xe. Không cần đăng nhập: mỗi link chứa 1 token bí mật (256-bit) do hệ thống tạo.
///  - request/{token}: link trong tin Zalo → xem và chấp nhận/từ chối 1 đơn.
///  - manage/{manageToken}: cổng quản lý lịch xe của chủ xe.
/// </summary>
[ApiController]
[Route("api/owner")]
public class OwnerController(BookingService bookings, OwnerPortalService portal) : ControllerBase
{
    [HttpGet("request/{token}")]
    public async Task<IActionResult> GetRequest(string token, CancellationToken ct) =>
        (await bookings.GetOwnerRequestAsync(token, ct)).ToActionResult(this);

    [HttpPost("request/{token}")]
    public async Task<IActionResult> Respond(string token, [FromBody] OwnerRespondRequest body, CancellationToken ct) =>
        (await bookings.OwnerRespondAsync(token, body.Accept, body.Reason, ct)).ToActionResult(this);

    [HttpGet("manage/{manageToken}")]
    public async Task<IActionResult> Dashboard(string manageToken, CancellationToken ct) =>
        (await portal.GetDashboardAsync(manageToken, ct)).ToActionResult(this);

    [HttpPost("manage/{manageToken}/vehicles/{vehicleId:int}/blocks")]
    public async Task<IActionResult> AddBlock(string manageToken, int vehicleId, [FromBody] CreateBlockRequest body, CancellationToken ct) =>
        (await portal.AddBlockAsync(manageToken, vehicleId, body, ct)).ToActionResult(this);

    [HttpDelete("manage/{manageToken}/blocks/{blockId:int}")]
    public async Task<IActionResult> RemoveBlock(string manageToken, int blockId, CancellationToken ct) =>
        (await portal.RemoveBlockAsync(manageToken, blockId, ct)).ToActionResult(this);

    [HttpPost("manage/{manageToken}/vehicles/{vehicleId:int}/active")]
    public async Task<IActionResult> SetActive(string manageToken, int vehicleId, [FromQuery] bool value, CancellationToken ct) =>
        (await portal.SetActiveAsync(manageToken, vehicleId, value, ct)).ToActionResult(this);
}
