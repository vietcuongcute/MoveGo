namespace MoveGo.API.Models;

public enum VehicleCategory
{
    Car4Seats = 1,
    Car7Seats = 2,
    ElectricBike = 3
}

public enum BookingStatus
{
    PendingOwner = 0,    // Khách vừa gửi yêu cầu, chờ chủ xe xác nhận qua Zalo
    AwaitingDeposit = 1, // Chủ xe đã chấp nhận, đang giữ xe chờ khách cọc 10%
    Confirmed = 2,       // Đã nhận cọc, hai bên được xem SĐT của nhau
    Rejected = 3,        // Chủ xe từ chối (hoặc xe không còn trống)
    Cancelled = 4,       // Huỷ thủ công
    Expired = 5,         // Quá hạn xác nhận / quá hạn cọc
    Completed = 6        // Đã trả xe
}