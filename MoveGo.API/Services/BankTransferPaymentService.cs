using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MoveGo.API.Dtos;
using MoveGo.API.Models;
using MoveGo.API.Options;

namespace MoveGo.API.Services;

/// <summary>
/// Cọc bằng chuyển khoản thật: tạo mã VietQR (chuẩn NAPAS) trỏ vào tài khoản của bạn, số tiền + nội dung điền sẵn.
/// Khi tiền về, SePay/Casso gọi webhook /api/payments/bank-webhook, ta đọc NỘI DUNG chuyển khoản để biết đơn nào.
/// </summary>
public class BankTransferPaymentService(IOptions<BankPaymentOptions> options, IClock clock) : IPaymentService
{
    public bool IsMock => false;

    public Task<PaymentLink> CreatePaymentLinkAsync(Booking booking, CancellationToken ct = default)
    {
        var o = options.Value;
        var amount = (long)booking.CommissionAmount;
        var content = BankTransfer.ContentFor(booking.Code);
        var orderCode = PayOsService.NewOrderCode(clock.Now, booking.Id);
        var qr = BankTransfer.BuildVietQr(o.BankBin, o.AccountNumber, amount, content);
        var image = $"https://img.vietqr.io/image/{o.BankBin}-{o.AccountNumber}-compact2.png" +
                    $"?amount={amount}&addInfo={Uri.EscapeDataString(content)}&accountName={Uri.EscapeDataString(o.AccountName)}";
        return Task.FromResult(new PaymentLink(orderCode, image, qr));
    }

    public Task<bool> IsPaidAsync(long orderCode, CancellationToken ct = default) => Task.FromResult(false); // chờ webhook
    public bool VerifyWebhook(System.Text.Json.JsonElement payload) => false;

    public BankTransferInfoDto? DescribeTransfer(Booking booking)
    {
        var o = options.Value;
        return new BankTransferInfoDto(o.BankName, o.AccountNumber, o.AccountName,
            (long)booking.CommissionAmount, BankTransfer.ContentFor(booking.Code));
    }
}

public static class BankTransfer
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // giống mã đơn
    private static readonly Regex CodeRx = new($"MG([{Alphabet}]{{6}})", RegexOptions.Compiled);

    /// <summary>"MG-K7P2QX" → "MGK7P2QX": nhiều ngân hàng tự bỏ dấu gạch nối trong nội dung chuyển khoản.</summary>
    public static string ContentFor(string bookingCode) => bookingCode.Replace("-", "").ToUpperInvariant();

    /// <summary>Tìm mã đơn trong nội dung chuyển khoản (chịu được khoảng trắng, dấu gạch, chữ thường, chữ thừa xung quanh).</summary>
    public static string? ExtractCode(string? transferContent)
    {
        if (string.IsNullOrWhiteSpace(transferContent)) return null;
        var normalized = new string(transferContent.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var m = CodeRx.Match(normalized);
        return m.Success ? $"MG-{m.Groups[1].Value}" : null;
    }

    /// <summary>Chuỗi VietQR động (EMV QRCPS-MPM) — mọi app ngân hàng ở VN đọc được; số tiền và nội dung điền sẵn.</summary>
    public static string BuildVietQr(string bin, string account, long amount, string content)
    {
        static string T(string id, string v) => $"{id}{v.Length:D2}{v}";
        var bank = T("00", bin) + T("01", account);
        var merchant = T("00", "A000000727") + T("01", bank) + T("02", "QRIBFTTA");
        var sb = new StringBuilder()
            .Append(T("00", "01"))
            .Append(T("01", "12"))                                   // 12 = QR động (có số tiền)
            .Append(T("38", merchant))
            .Append(T("53", "704"))                                  // VND
            .Append(T("54", amount.ToString(CultureInfo.InvariantCulture)))
            .Append(T("58", "VN"))
            .Append(T("62", T("08", content)))
            .Append("6304");
        return sb.Append(Crc16(sb.ToString())).ToString();
    }

    private static string Crc16(string s)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(s))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }
}
