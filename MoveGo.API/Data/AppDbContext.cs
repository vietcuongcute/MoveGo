using Microsoft.EntityFrameworkCore;
using MoveGo.API.Models;

namespace MoveGo.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<VehicleUnavailability> VehicleUnavailabilities => Set<VehicleUnavailability>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Toàn bộ giờ trong hệ thống là giờ Việt Nam (1 múi giờ) nên dùng timestamp KHÔNG múi giờ cho đơn giản.
        // (SQL Server: datetime2)
        configurationBuilder.Properties<DateTime>().HaveColumnType(DbLock.IsSqlServer(this) ? "datetime2" : "timestamp without time zone");
        configurationBuilder.Properties<decimal>().HavePrecision(18, 0); // VNĐ không có số lẻ
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Owner>(e =>
        {
            e.HasIndex(x => x.ManageToken).IsUnique();
        });

        b.Entity<Vehicle>(e =>
        {
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Owner).WithMany(o => o.Vehicles)
                .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.IsActive, x.Category, x.PickupLocation });
        });

        b.Entity<Booking>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Vehicle).WithMany(v => v.Bookings)
                .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.OwnerToken).IsUnique();
            e.HasIndex(x => x.PayOsOrderCode).IsUnique();
            // Index phục vụ truy vấn kiểm tra trùng lịch (bước 3)
            e.HasIndex(x => new { x.VehicleId, x.StartDate, x.EndDate });
        });

        b.Entity<VehicleUnavailability>(e =>
        {
            e.HasOne(x => x.Vehicle).WithMany(v => v.Unavailabilities)
                .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.VehicleId, x.StartDate, x.EndDate });
        });
    }
}