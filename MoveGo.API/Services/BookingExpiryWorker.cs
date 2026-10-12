namespace MoveGo.API.Services;

/// <summary>Chạy nền mỗi phút: đơn quá hạn → Expired (xe tự mở lại), chuyến kết thúc → Completed.</summary>
public class BookingExpiryWorker(IServiceScopeFactory scopes, ILogger<BookingExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<BookingService>();
                var n = await service.ExpireStaleAsync(stoppingToken);
                if (n > 0) logger.LogInformation("Đã cập nhật {Count} đơn quá hạn/hoàn tất.", n);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "BookingExpiryWorker gặp lỗi");
            }
        } while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
