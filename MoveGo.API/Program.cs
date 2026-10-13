using System.Text.Json.Serialization;
using MoveGo.API.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Toàn bộ dịch vụ lõi (DB, đặt xe, Zalo, PayOS, rate limit...) nằm trong Hosting/ApiHosting.cs
builder.Services.AddMoveGoApiCore(builder.Configuration);

builder.Services.AddControllers()
    // Enum trả về dạng chữ ("Car4Seats") thay vì số (1) cho dễ đọc
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: cho phép trình duyệt ở website MVC gọi thẳng API (AJAX)
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5200"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await app.Services.InitializeMoveGoAsync(app.Configuration, app.Environment);

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
