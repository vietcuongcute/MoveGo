using System.Security.Cryptography;

namespace MoveGo.API.Services;

/// <summary>Giờ hiện tại theo múi giờ Việt Nam (UTC+7), bất kể server đặt ở đâu.</summary>
public interface IClock
{
    DateTime Now { get; }
}

public class VietnamClock : IClock
{
    private static readonly TimeZoneInfo Tz = FindTimeZone();

    public DateTime Now =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz), DateTimeKind.Unspecified);

    private static TimeZoneInfo FindTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN");
    }
}

/// <summary>Sinh mã ngẫu nhiên an toàn (dùng crypto RNG, không dùng Random).</summary>
public static class Tokens
{
    // Bỏ các ký tự dễ nhầm: 0/O, 1/I
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string BookingCode()
    {
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return "MG-" + new string(chars);
    }

    /// <summary>Token dài dùng trong URL (32 byte ngẫu nhiên → 43 ký tự).</summary>
    public static string UrlToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}