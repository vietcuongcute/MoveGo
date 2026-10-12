using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController(
    BookingService bookings, IPaymentService payments, IHostEnvironment env, ILogger<PaymentController> logger) : ControllerBase
{
    /// <summary>CHỈ để thử khi lập trình: giả lập khách đã cọc. Tự tắt khi không phải Development hoặc đã bật PayOS.</summary>
    [HttpPost("mock/{code}/complete")]
    public async Task<IActionResult> MockComplete(string code, CancellationToken ct)
    {
        if (!payments.IsMock || !env.IsDevelopment()) return NotFound();
        return (await bookings.MockPayAsync(code, ct)).ToActionResult(this);
    }

    /// <summary>
    /// Webhook PayOS gọi về khi khách thanh toán xong. Chữ ký được kiểm tra bằng Checksum Key —
    /// không có chữ ký đúng thì không ai giả mạo được "đã thanh toán".
    /// </summary>
    [HttpPost("payos-webhook")]
    public async Task<IActionResult> PayOsWebhook([FromBody] JsonElement payload, CancellationToken ct)
    {
        if (!payments.VerifyWebhook(payload))
        {
            logger.LogWarning("Webhook PayOS sai chữ ký.");
            return BadRequest(new { success = false });
        }

        var data = payload.GetProperty("data");
        var paid = data.TryGetProperty("code", out var c) && c.GetString() == "00";
        if (paid && data.TryGetProperty("orderCode", out var oc) && oc.TryGetInt64(out var orderCode))
        {
            var result = await bookings.MarkPaidByOrderCodeAsync(orderCode, ct);
            if (!result.Ok) logger.LogWarning("Webhook orderCode {Order}: {Error}", orderCode, result.Error);
        }

        // Luôn trả 200 cho webhook hợp lệ (kể cả payload kiểm tra mà PayOS gửi lúc đăng ký webhook).
        return Ok(new { success = true });
    }
}
