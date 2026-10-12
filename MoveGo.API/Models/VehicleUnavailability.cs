using System.ComponentModel.DataAnnotations;

namespace MoveGo.API.Models;

/// <summary>Khoảng thời gian chủ xe tự khoá (xe bận, bảo dưỡng, cho người quen thuê...).</summary>
public class VehicleUnavailability
{
    public int Id { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(200)] public string Reason { get; set; } = "";
}