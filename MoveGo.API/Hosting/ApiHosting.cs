using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using MoveGo.API.Data;
using MoveGo.API.Options;
using MoveGo.API.Services;

namespace MoveGo.API.Hosting;

/// <summary>
/// Cấu hình lõi của API, dùng chung cho 2 cách chạy:
///  1) MoveGo.API chạy riêng (Program.cs của API),
///  2) API được GỘP vào website MoveGo.Web (build với -p:EmbedApi=true) để up lên host chỉ có 1 website.
/// </summary>
public static class ApiHosting
{
    public static IServiceCollection AddMoveGoApiCore(this IServiceCollection services, IConfiguration config)
    {
        // Npgsql: cho phép ghi DateTime (Kind=Unspecified) vào cột "timestamp without time zone".
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        services.Configure<BusinessOptions>(config.GetSection("Business"));
        services.AddScoped<PricingService>();
        services.AddScoped<BookingService>();
        services.AddScoped<OwnerPortalService>();
        services.AddScoped<AdminCatalogService>();

        // Zalo ZNS: chưa bật → chế độ giả lập (chỉ in tin nhắn ra console). Bật trong appsettings: Zalo:Enabled = true
        services.Configure<ZaloOptions>(config.GetSection("Zalo"));
        if (config.GetValue<bool>("Zalo:Enabled"))
        {
            services.AddSingleton<ZaloTokenProvider>();
            services.AddHttpClient<IZaloService, ZaloZnsService>();
        }
        else services.AddSingleton<IZaloService, MockZaloService>();
        services.AddHttpClient();

        // Thanh toán cọc (ưu tiên từ trên xuống):
        //   BankPayment:Enabled → chuyển khoản ngân hàng thật + tự nhận diện qua webhook SePay/Casso
        //   PayOs:Enabled       → PayOS
        //   còn lại             → giả lập (nút "đã thanh toán" để thử)
        services.Configure<BankPaymentOptions>(config.GetSection("BankPayment"));
        services.Configure<PayOsOptions>(config.GetSection("PayOs"));
        if (config.GetValue<bool>("BankPayment:Enabled"))
            services.AddSingleton<IPaymentService, BankTransferPaymentService>();
        else if (config.GetValue<bool>("PayOs:Enabled"))
            services.AddHttpClient<IPaymentService, PayOsService>();
        else services.AddSingleton<IPaymentService, MockPaymentService>();

        // Chạy nền mỗi phút: đơn quá hạn → Expired (xe tự mở lại), chuyến kết thúc → Completed
        services.AddHostedService<BookingExpiryWorker>();

        // Chống spam tạo đơn: tối đa 10 yêu cầu / phút / địa chỉ IP
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("booking", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
            // Tra cứu theo SĐT: chặt hơn để khó dò số của người khác
            o.AddPolicy("lookup", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1) }));
        });

        services.AddSingleton<IClock, VietnamClock>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        // Hai loại CSDL: PostgreSQL (mặc định khi chạy ở máy) hoặc SQL Server (MonsterASP gói Free chỉ có MSSQL/MySQL).
        // Tự nhận theo chuỗi kết nối ("Server=..."/"Data Source=..." → SQL Server) hoặc ép bằng Database:Provider.
        var useSqlServer = UsesSqlServer(config);
        services.AddDbContext<AppDbContext>(o =>
        {
            var cs = config.GetConnectionString("Default");
            if (useSqlServer) o.UseSqlServer(cs);
            else o.UseNpgsql(NormalizeConnectionString(cs));
        });

        services.AddProblemDetails();
        return services;
    }

    /// <summary>Khi khởi động: áp migration (tạo/cập nhật bảng) rồi nạp dữ liệu mẫu.</summary>
    public static async Task InitializeMoveGoAsync(this IServiceProvider root, IConfiguration config, IHostEnvironment env)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // PostgreSQL: dùng migration. SQL Server: các migration hiện có viết cho PostgreSQL nên tạo bảng thẳng từ model.
        if (DbLock.IsSqlServer(db)) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();

        if (config.GetValue<bool>("Seed:Enabled"))
            await SeedData.EnsureSeededAsync(db, scope.ServiceProvider.GetRequiredService<IClock>());

        // Khi lập trình: in link quản lý lịch của từng chủ xe ra console để bạn bấm thử.
        if (env.IsDevelopment())
        {
            var web = (config["Business:WebBaseUrl"] ?? "http://localhost:5200").TrimEnd('/');
            var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
            foreach (var o in await db.Owners.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
                log.LogInformation("Chủ xe {Name}: {Web}/owner/manage/{Token}", o.Name, web, o.ManageToken);
        }
    }

    /// <summary>Database:Provider = "SqlServer" | "Postgres"; để trống thì đoán theo chuỗi kết nối.</summary>
    public static bool UsesSqlServer(IConfiguration config)
    {
        var provider = config["Database:Provider"];
        if (!string.IsNullOrWhiteSpace(provider))
            return provider.Replace(" ", "").Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                || provider.Equals("mssql", StringComparison.OrdinalIgnoreCase);
        var cs = config.GetConnectionString("Default") ?? "";
        // Chuỗi của PostgreSQL: "Host=..." / "postgres://..." / "Server=...;Port=5432;..." (MonsterASP hiển thị dạng này)
        if (cs.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) || cs.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || cs.Contains("Port=5432", StringComparison.OrdinalIgnoreCase)) return false;
        return cs.Contains("Server=", StringComparison.OrdinalIgnoreCase) || cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nhận cả dạng "postgresql://user:pass@host/db?sslmode=require" (Neon, Supabase, Render... hay đưa dạng này)
    /// lẫn dạng "Host=...;Database=..." của Npgsql.
    /// </summary>
    public static string NormalizeConnectionString(string? raw)
    {
        raw = raw?.Trim() ?? "";
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return raw;

        var uri = new Uri(raw);
        var user = Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[0]);
        var pass = uri.UserInfo.Contains(':') ? Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[1]) : "";
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var ssl = (query["sslmode"] ?? "require").ToLowerInvariant() switch
        {
            "disable" => "Disable",
            "prefer" or "allow" => "Prefer",
            _ => "Require"
        };
        var cs = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};" +
                 $"Username={user};Password={pass};SSL Mode={ssl}";
        // Host dùng chung thường thiếu chứng chỉ gốc trên máy chủ Windows → không kiểm tra chuỗi chứng chỉ (vẫn mã hoá đường truyền).
        if (ssl != "Disable") cs += ";Trust Server Certificate=true";
        return cs;
    }

    /// <summary>
    /// Khi API chạy chung trong website MVC, các controller API (api/...) bị gom vào "area" riêng để không
    /// trùng tên với controller của website (cùng tên Admin/Booking/Owner) khi sinh link.
    /// </summary>
    public sealed class ApiAreaConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType.Namespace == "MoveGo.API.Controllers")
                controller.RouteValues["area"] = "MoveGoApi";
        }
    }
}
