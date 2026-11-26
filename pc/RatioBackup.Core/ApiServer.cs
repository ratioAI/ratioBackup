using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RatioBackup.Core;

/// <summary>HTTPS-API nach docs/PROTOCOL.md.</summary>
public static class ApiServer
{
    public const int DefaultPort = 47321;

    public static WebApplication Build(BackupStore store, PairingService pairing, X509Certificate2? cert,
        int port = DefaultPort, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.Limits.MaxRequestBodySize = null;
            if (cert != null) o.ListenAnyIP(port, l => l.UseHttps(cert));
        });
        configure?.Invoke(builder);
        var app = builder.Build();

        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path != "/pair" && !pairing.IsAuthorized(ctx.Request.Headers.Authorization.ToString()))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            await next();
        });

        app.MapPost("/pair", (PairRequest req) =>
            pairing.TryPair(req.Code, req.DeviceName) is { } r ? Results.Ok(r) : Results.StatusCode(StatusCodes.Status403Forbidden));

        app.MapGet("/status", () => Results.Ok(store.GetStatus()));

        app.MapPost("/manifest", (ManifestRequest m) => Results.Ok(store.ApplyManifest(m)));

        app.MapPut("/files", async (HttpContext ctx) =>
        {
            var q = ctx.Request.Query;
            var path = q["path"].ToString();
            if (!PathMapper.IsSafe(path)
                || !long.TryParse(q["size"], out var size) || !long.TryParse(q["mtimeMs"], out var mtime)
                || !long.TryParse(q["offset"], out var offset) || size < 0 || offset < 0)
                return Results.BadRequest();

            var sha = ctx.Request.Headers["X-Sha256"].ToString();
            var r = await store.WriteAsync(path, size, mtime, offset, ctx.Request.Body, sha.Length == 0 ? null : sha);
            return r.Outcome switch
            {
                UploadOutcome.Complete => Results.Ok(),
                UploadOutcome.Partial => Results.Json(new OffsetDto(r.Offset), statusCode: StatusCodes.Status206PartialContent),
                UploadOutcome.WrongOffset => Results.Json(new OffsetDto(r.Offset), statusCode: StatusCodes.Status409Conflict),
                UploadOutcome.Corrupt => Results.StatusCode(StatusCodes.Status422UnprocessableEntity),
                _ => Results.StatusCode(StatusCodes.Status507InsufficientStorage),
            };
        });

        app.MapPost("/complete", () => { store.Complete(); return Results.Ok(); });
        return app;
    }
}
