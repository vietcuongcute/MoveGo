using System.Text.Json.Serialization;
using MoveGo.Web.Models;
using MoveGo.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

// IHttpClientFactory: quản lý kết nối HTTP tới MoveGo.API (tái sử dụng socket, có timeout)
var apiBase = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100";
builder.Services.AddHttpClient("MoveGoApi", c =>
{
    c.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<IMoveGoApi, MoveGoApi>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
