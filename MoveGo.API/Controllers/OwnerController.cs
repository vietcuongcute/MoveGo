using Microsoft.AspNetCore.Mvc;
using MoveGo.API.Dtos;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

/// <summary>
/// Các API dành cho chủ xe. Không cần đăng nhập: mỗi link chứa 1 token bí mật (256-bit) do hệ thống tạo.
///  - request/{token}: link trong tin Zalo → xem và chấp nhận/từ chối 1 đơn.
/// </summary>
[ApiController]
[Route("api/owner")]
public class OwnerController(BookingService bookings) : ControllerBase
{
    [HttpGet("request/{token}")]
    public async Task<IActionResult> GetRequest(string token, CancellationToken ct) =>
        (await bookings.GetOwnerRequestAsync(token, ct)).ToActionResult(this);

    [HttpPost("request/{token}")]
    public async Task<IActionResult> Respond(string token, [FromBody] OwnerRespondRequest body, CancellationToken ct) =>
        (await bookings.OwnerRespondAsync(token, body.Accept, body.Reason, ct)).ToActionResult(this);
}
