using System.Diagnostics;
namespace PortalTarefas.Web;
public sealed class RequestLogMiddleware(RequestDelegate next, ILogger<RequestLogMiddleware> logger)
{
 public async Task InvokeAsync(HttpContext c) {
  c.TraceIdentifier=Guid.NewGuid().ToString("N"); c.Response.OnStarting(()=>{c.Response.Headers["X-Correlation-ID"]=c.TraceIdentifier;return Task.CompletedTask;});
  using var scope=logger.BeginScope(new Dictionary<string,object>{{"CorrelationId",c.TraceIdentifier}});
  var sw=Stopwatch.StartNew();Signals.Requests.Add(1,new KeyValuePair<string,object?>("metodo",c.Request.Method)); logger.LogInformation("Entrada {Method} {Path}",c.Request.Method,c.Request.Path);
  try { await next(c); } finally { Signals.Duration.Record(sw.Elapsed.TotalMilliseconds);logger.LogInformation("Saida {Method} {Path} {Status} {Ms}",c.Request.Method,c.Request.Path,c.Response.StatusCode,sw.Elapsed.TotalMilliseconds); }
 }
}
public static class PipelineExtensions { public static IApplicationBuilder UseRequestLog(this IApplicationBuilder app)=>app.UseMiddleware<RequestLogMiddleware>(); }

