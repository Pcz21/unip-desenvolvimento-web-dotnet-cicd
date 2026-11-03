using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
namespace PortalTarefas.Web;
public static class Signals {
 public static readonly ActivitySource Source=new("PortalTarefas");public static readonly Meter Meter=new("PortalTarefas");
 public static readonly Counter<long> Requests=Meter.CreateCounter<long>("portal.requisicoes");public static readonly Counter<long> Saves=Meter.CreateCounter<long>("portal.tarefas.salvas");public static readonly Histogram<double> Duration=Meter.CreateHistogram<double>("portal.duracao","ms");
}
public class ReadyCheck(PortalTarefasDbContext db,IHttpClientFactory factory,IConfiguration config):IHealthCheck {
 public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken token=default){try{if(!await db.Database.CanConnectAsync(token))return HealthCheckResult.Unhealthy();using var r=await factory.CreateClient("health").GetAsync(config["Dependency:Url"]+"/health",token);return r.IsSuccessStatusCode?HealthCheckResult.Healthy():HealthCheckResult.Unhealthy();}catch{return HealthCheckResult.Unhealthy();}}
}
