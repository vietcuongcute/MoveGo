using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MoveGo.API.Data;
using MoveGo.API.Services;
using MoveGo.API.Options;

// Npgsql: cho phép ghi DateTime (Kind=Unspecified) vào cột "timestamp without time zone".
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BusinessOptions>(builder.Configuration.GetSection("Business"));
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<OwnerPortalService>();
builder.Services.AddScoped<AdminCatalogService>();

// Zalo ZNS: chưa bật → chế độ giả lập (chỉ in tin nhắn ra console). Bật trong appsettings: Zalo:Enabled = true
builder.Services.Configure<ZaloOptions>(builder.Configuration.GetSection("Zalo"));
if (builder.Configuration.GetValue<bool>("Zalo:Enabled"))
{
    builder.Services.AddSingleton<ZaloTokenProvider>();
    builder.Services.AddHttpClient<IZaloService, ZaloZnsService>();
}
else builder.Services.AddSingleton<IZaloService, MockZaloService>();
builder.Services.AddHttpClient();

// PayOS: chưa bật → thanh toán giả lập. Bật trong appsettings: PayOs:Enabled = true
builder.Services.Configure<PayOsOptions>(builder.Configuration.GetSection("PayOs"));
if (builder.Configuration.GetValue<bool>("PayOs:Enabled"))
    builder.Services.AddHttpClient<IPaymentService, PayOsService>();
else builder.Services.AddSingleton<IPaymentService, MockPaymentService>();

// Chạy nền mỗi phút: đơn quá hạn → Expired (xe tự mở lại), chuyến kết thúc → Completed
builder.Services.AddHostedService<BookingExpiryWorker>();

// Chống spam tạo đơn: tối đa 10 yêu cầu / phút / địa chỉ IP
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("booking", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddSingleton<IClock, VietnamClock>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers()
    // Enum trả về dạng chữ ("Car4Seats") thay vì số (1) cho dễ đọc
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: cho phép trình duyệt ở website MVC gọi thẳng API (AJAX)
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Khi khởi động: áp migration (tạo/cập nhật bảng) rồi nạp dữ liệu mẫu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Seed:Enabled"))
        await SeedData.EnsureSeededAsync(db, scope.ServiceProvider.GetRequiredService<IClock>());

    // Khi lập trình: in link quản lý lịch của từng chủ xe ra console để bạn bấm thử.
    if (app.Environment.IsDevelopment())
    {
        var web = (app.Configuration["Business:WebBaseUrl"] ?? "http://localhost:5200").TrimEnd('/');
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        foreach (var o in await db.Owners.AsNoTracking().OrderBy(x => x.Id).ToListAsync())
            log.LogInformation("Chủ xe {Name}: {Web}/owner/manage/{Token}", o.Name, web, o.ManageToken);
    }
}

app.UseExceptionHandler();   // lỗi không lường trước → ProblemDetails JSON thay vì stack trace
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/", () => "MoveGo API is running");
app.Run();