using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController(
    BookingService bookings, IPaymentService payments, IHostEnvironment env,
    Microsoft.Extensions.Options.IOptions<MoveGo.API.Options.BusinessOptions> business,
    Microsoft.Extensions.Options.IOptions<MoveGo.API.Options.BankPaymentOptions> bank, ILogger<PaymentController> logger) : ControllerBase
{
    /// <summary>Giả lập khách đã cọc. Chỉ chạy khi PayOS chưa bật VÀ (môi trường Development hoặc Business:AllowMockPayment = true).</summary>
    [HttpPost("mock/{code}/complete")]
    public async Task<IActionResult> MockComplete(string code, CancellationToken ct)
    {
        if (!payments.IsMock || !(env.IsDevelopment() || business.Value.AllowMockPayment)) return NotFound();
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

    /// <summary>
    /// Webhook "tiền về tài khoản" của SePay (header Authorization: Apikey ...) hoặc Casso (header secure-token: ...).
    /// Không có khoá đúng thì không ai giả mạo được "đã chuyển khoản". Đơn được nhận diện bằng mã MG… trong NỘI DUNG chuyển khoản.
    /// </summary>
    [HttpPost("bank-webhook")]
    public async Task<IActionResult> BankWebhook([FromBody] JsonElement payload, CancellationToken ct)
    {
        var o = bank.Value;
        if (!o.Enabled || string.IsNullOrEmpty(o.WebhookSecret)) return NotFound();

        var auth = Request.Headers.Authorization.ToString();
        var provided = auth.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim()
            : Request.Headers["secure-token"].ToString() is { Length: > 0 } t ? t
            : Request.Headers["X-Webhook-Secret"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(o.WebhookSecret))))
        {
            logger.LogWarning("Webhook ngân hàng sai khoá.");
            return Unauthorized(new { success = false, error = 1 });
        }

        foreach (var tx in ReadIncomingTransfers(payload, o.AccountNumber))
        {
            var r = await bookings.MarkPaidByTransferAsync(tx.Content, tx.Amount, tx.Id, ct);
            if (r.Ok) logger.LogInformation("Đã nhận cọc {Amount} cho nội dung '{Content}'.", tx.Amount, tx.Content);
            else logger.LogWarning("Giao dịch '{Content}' {Amount}đ: {Error}", tx.Content, tx.Amount, r.Error);
        }
        // Luôn 200 cho webhook hợp lệ (giao dịch không phải cọc cũng vậy) để cổng không gửi lại mãi.
        return Ok(new { success = true, error = 0 });
    }

    private record IncomingTransfer(string? Id, string? Content, decimal Amount);

    /// <summary>Đọc cả hai định dạng: SePay (1 giao dịch phẳng) và Casso (data là 1 object hoặc mảng). Chỉ lấy tiền VÀO.</summary>
    private static IEnumerable<IncomingTransfer> ReadIncomingTransfers(JsonElement root, string myAccount)
    {
        static string? Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : null) : null;
        static decimal Num(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
                ? (v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0) : 0;

        // SePay
        if (Str(root, "transferType") is { } type)
        {
            if (!type.Equals("in", StringComparison.OrdinalIgnoreCase)) yield break;
            if (Str(root, "accountNumber") is { Length: > 0 } acc && !string.IsNullOrEmpty(myAccount) && acc != myAccount) yield break;
            yield return new(Str(root, "id"), Str(root, "content") ?? Str(root, "description"), Num(root, "transferAmount"));
            yield break;
        }

        // Casso
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            var items = data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().ToList() : [data];
            foreach (var it in items)
            {
                var amount = Num(it, "amount");
                if (amount <= 0) continue;                                         // tiền ra
                yield return new(Str(it, "id") ?? Str(it, "tid"), Str(it, "description"), amount);
            }
        }
    }
}
