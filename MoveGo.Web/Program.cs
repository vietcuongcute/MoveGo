using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
#if EMBED_API
using MoveGo.API.Hosting;
#endif
using MoveGo.Web.Models;
using MoveGo.Web.Services;

var builder = WebApplication.CreateBuilder(args);

#if EMBED_API
// Giá trị mặc định của API đứng ĐẦU danh sách nguồn cấu hình (ưu tiên thấp nhất), nên appsettings.Production.json,
// biến môi trường... vẫn ghi đè được.
builder.Configuration.Sources.Insert(0, new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
{
    Path = "appsettings.api.json",
    Optional = true,
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory)
});
#endif

builder.Services.AddControllersWithViews(o =>
    {
#if EMBED_API
        o.Conventions.Add(new MoveGo.API.Hosting.ApiHosting.ApiAreaConvention());
#endif
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

#if EMBED_API
// ---- Chế độ GỘP: API chạy ngay trong website này (xem MoveGo.Web.csproj, -p:EmbedApi=true) ----
builder.Services.AddMoveGoApiCore(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<InProcessApiHandler>();
builder.Services.AddHttpClient("MoveGoApi", c =>
{
    c.BaseAddress = new Uri("http://moveGo.internal/");   // địa chỉ ảo: request không đi qua mạng
    c.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler<InProcessApiHandler>();
// Trình duyệt gọi API cùng domain với website → không cần địa chỉ riêng, không cần CORS
builder.Services.PostConfigure<ApiOptions>(o => { o.BaseUrl = ""; o.PublicUrl = ""; });
#else
// IHttpClientFactory: quản lý kết nối HTTP tới MoveGo.API (tái sử dụng socket, có timeout)
var apiBase = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100";
builder.Services.AddHttpClient("MoveGoApi", c =>
{
    c.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(20);
});
#endif
builder.Services.AddScoped<IMoveGoApi, MoveGoApi>();
builder.Services.AddScoped<IAdminApi, AdminApi>();

// Giới hạn đăng nhập quản trị: tối đa 5 lần/phút/IP để không ai dò được khoá
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("admin-login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

#if EMBED_API
// Bắt lấy phần còn lại của pipeline để InProcessApiHandler đưa request API vào thẳng đây
app.Use(next => { InProcessApiHandler.Pipeline = next; return next; });

// Tạo/cập nhật bảng và nạp dữ liệu mẫu. Lỗi kết nối DB không làm sập website: ghi log và báo ở /healthz.
var dbReady = false;
try
{
    await app.Services.InitializeMoveGoAsync(app.Configuration, app.Environment);
    dbReady = true;
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Không khởi tạo được database. Kiểm tra ConnectionStrings:Default.");
}
app.MapGet("/healthz", () => dbReady ? Results.Text("ok") : Results.Text("db-error", statusCode: 503));
#endif

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
