using Microsoft.AspNetCore.Diagnostics;
using SmartMarket.Application.Common;

namespace SmartMarket.Api.Endpoints;

/// <summary>
/// Any command that loses a race on a unique index (two runs, rejects, confirms or check-ins at once) answers 409 with a
/// Thai "try again", not 500. Handlers that know a better message catch the exception themselves (submit does).
/// </summary>
public sealed class ConcurrentUpdateHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not ConcurrentUpdateException) return false;
        await ResultHttp.Problem(Error.Conflict("conflict.concurrent", "มีการทำรายการเดียวกันพร้อมกัน กรุณาโหลดหน้าใหม่แล้วลองอีกครั้ง"))
            .ExecuteAsync(http);
        return true;
    }
}
