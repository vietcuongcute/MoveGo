using Microsoft.EntityFrameworkCore;

namespace MoveGo.API.Data;

/// <summary>Khoá theo xe (chống 2 người cùng giữ một xe) — cách làm khác nhau giữa PostgreSQL và SQL Server.</summary>
public static class DbLock
{
    public static bool IsSqlServer(DbContext db) =>
        db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Gọi BÊN TRONG transaction; khoá tự nhả khi transaction kết thúc.</summary>
    public static Task AcquireVehicleAsync(DbContext db, int vehicleId, CancellationToken ct)
    {
        if (IsSqlServer(db))
        {
            var resource = "movego-vehicle-" + vehicleId;
            return db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_getapplock @Resource = {resource}, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000", ct);
        }
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({(long)vehicleId})", ct);
    }
}
