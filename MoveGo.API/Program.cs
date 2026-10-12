using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MoveGo.API.Data;
using MoveGo.API.Services;
using MoveGo.API.Options;

// Npgsql: cho phép ghi DateTime (Kind=Unspecified) vào cột "timestamp without time zone".
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BusinessOptions>(builder.Configuration.GetSection("Business"));
builder.Services.AddScoped<PricingService>();

builder.Services.AddSingleton<IClock, VietnamClock>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers()
    // Enum trả về dạng chữ ("Car4Seats") thay vì số (1) cho dễ đọc
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Khi khởi động: áp migration (tạo/cập nhật bảng) rồi nạp dữ liệu mẫu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Seed:Enabled"))
        await SeedData.EnsureSeededAsync(db, scope.ServiceProvider.GetRequiredService<IClock>());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/", () => "Move-Go API is running");
app.Run();