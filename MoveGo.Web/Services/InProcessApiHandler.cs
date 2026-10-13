#if EMBED_API
namespace MoveGo.Web.Services;

/// <summary>
/// Chế độ gộp API vào website: thay vì gửi HTTP qua mạng tới MoveGo.API, các lời gọi của <see cref="MoveGoApi"/> /
/// <see cref="AdminApi"/> được đưa thẳng vào pipeline của chính ứng dụng (cùng tiến trình, không cần cổng thứ hai).
/// IP của khách được giữ nguyên để rate limit vẫn tính đúng theo từng người.
/// </summary>
public sealed class InProcessApiHandler(IServiceProvider root, IHttpContextAccessor accessor) : HttpMessageHandler
{
    /// <summary>Phần còn lại của pipeline (gán trong Program.cs bằng app.Use(next => ...)).</summary>
    public static RequestDelegate? Pipeline { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var pipeline = Pipeline ?? throw new InvalidOperationException("Pipeline chưa được khởi tạo.");
        var outer = accessor.HttpContext;
        await using var scope = root.CreateAsyncScope();

        var ctx = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = ct };
        var req = ctx.Request;
        req.Method = request.Method.Method;
        req.Scheme = outer?.Request.Scheme ?? "http";
        req.Host = outer?.Request.Host ?? new HostString("localhost");
        var uri = request.RequestUri!;
        req.Path = uri.AbsolutePath;
        req.QueryString = new QueryString(uri.Query);
        ctx.Connection.RemoteIpAddress = outer?.Connection.RemoteIpAddress;

        foreach (var h in request.Headers) req.Headers[h.Key] = h.Value.ToArray();
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(ct);
            foreach (var h in request.Content.Headers) req.Headers[h.Key] = h.Value.ToArray();
            req.Body = new MemoryStream(bytes);
            req.ContentLength = bytes.Length;
        }

        await using var body = new MemoryStream();
        ctx.Response.Body = body;
        await pipeline(ctx);

        body.Position = 0;
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)ctx.Response.StatusCode)
        {
            Content = new ByteArrayContent(body.ToArray()),
            RequestMessage = request
        };
        foreach (var h in ctx.Response.Headers)
        {
            if (!response.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray()))
                response.Content.Headers.TryAddWithoutValidation(h.Key, h.Value.ToArray());
        }
        return response;
    }
}
#endif
