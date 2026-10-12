using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoveGo.API.Data;
using MoveGo.API.Models;
using MoveGo.API.Options;
using MoveGo.API.Services;

namespace MoveGo.API.Controllers;

/// <summary>Bảo vệ API admin bằng header X-Admin-Key (đặt trong cấu hình Business:AdminKey).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminKeyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configured = context.HttpContext.RequestServices.GetRequiredService<IOptions<BusinessOptions>>().Value.AdminKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            context.Result = new ObjectResult(new ProblemDetails { Title = "Admin API đang tắt (chưa cấu hình Business:AdminKey).", Status = 503 }) { StatusCode = 503 };
            return;
        }

        var provided = context.HttpContext.Request.Headers["X-Admin-Key"].ToString();
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(provided));
        if (!ok)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}

[ApiController]
[Route("api/admin")]
[AdminKey]
public class AdminController(AppDbContext db, BookingService bookings, IOptions<BusinessOptions> biz) : ControllerBase
{
    /// <summary>Danh sách chủ xe kèm link quản lý lịch (gửi link này cho chủ xe qua Zalo khi onboard).</summary>
    [HttpGet("owners")]
    public async Task<IActionResult> Owners(CancellationToken ct)
    {
        var web = biz.Value.WebBaseUrl.TrimEnd('/');
        var owners = await db.Owners.AsNoTracking().OrderBy(o => o.Id).ToListAsync(ct);
        return Ok(owners.Select(o => new { o.Id, o.Name, o.PhoneNumber, o.ZaloPhone, ManageLink = $"{web}/owner/manage/{o.ManageToken}" }));
    }

    /// <summary>Tổng quan: số đơn theo trạng thái, doanh thu hoa hồng, đơn cần hoàn cọc.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var byStatus = (await db.Bookings.AsNoTracking()
                .GroupBy(b => b.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Status.ToString(), x => x.Count);

        var commission = await db.Bookings.Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
            .SumAsync(b => b.CommissionAmount, ct);
        var needRefund = await db.Bookings.CountAsync(b => b.AdminNote != null, ct);
        var ownerNotNotified = await db.Bookings.CountAsync(b => b.Status == BookingStatus.PendingOwner && b.OwnerNotifiedAt == null, ct);

        return Ok(new { byStatus, commissionEarned = commission, needRefund, ownerNotNotified });
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> List([FromQuery] BookingStatus? status, CancellationToken ct)
    {
        var query = db.Bookings.AsNoTracking().Include(b => b.Vehicle).ThenInclude(v => v.Owner).AsQueryable();
        if (status.HasValue) query = query.Where(b => b.Status == status.Value);

        var items = await query.OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync(ct);
        return Ok(items.Select(b => new
        {
            b.Id, b.Code, Status = b.Status.ToString(),
            Vehicle = b.Vehicle.Name, Owner = b.Vehicle.Owner.Name, OwnerPhone = b.Vehicle.Owner.PhoneNumber,
            b.CustomerName, b.CustomerPhone, b.StartDate, b.EndDate, b.RentalDays,
            b.TotalAmount, b.CommissionAmount, b.OwnerAmount,
            b.CreatedAt, b.OwnerNotifiedAt, b.OwnerRespondedAt, b.PaidAt, b.AdminNote
        }));
    }

    /// <summary>Gửi lại tin Zalo cho chủ xe nếu lần đầu bị lỗi.</summary>
    [HttpPost("bookings/{id:int}/resend-owner")]
    public async Task<IActionResult> ResendOwner(int id, CancellationToken ct) =>
        (await bookings.ResendOwnerNotificationAsync(id, ct)).ToActionResult(this);
}
