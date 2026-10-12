using Microsoft.AspNetCore.Mvc;

namespace MoveGo.API.Services;

/// <summary>Kết quả của service: hoặc thành công kèm dữ liệu, hoặc lỗi kèm mã HTTP + thông báo tiếng Việt.</summary>
public record ServiceResult<T>(T? Value, int StatusCode = 200, string? Error = null)
{
    public bool Ok => Error is null;
    public static ServiceResult<T> Success(T value) => new(value);
    public static ServiceResult<T> Fail(int statusCode, string error) => new(default, statusCode, error);
}

public static class ServiceResultExtensions
{
    /// <summary>Chuyển ServiceResult thành HTTP response chuẩn (ProblemDetails khi lỗi).</summary>
    public static IActionResult ToActionResult<T>(this ServiceResult<T> r, ControllerBase c) =>
        r.Ok
            ? c.Ok(r.Value)
            : c.Problem(title: r.Error, statusCode: r.StatusCode);
}
