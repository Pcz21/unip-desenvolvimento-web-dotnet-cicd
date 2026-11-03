using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
namespace PortalTarefas.Web;
public class ActionLogFilter(ILogger<ActionLogFilter> log):IAsyncActionFilter {
 public async Task OnActionExecutionAsync(ActionExecutingContext c,ActionExecutionDelegate next){var sw=Stopwatch.StartNew();var result=await next();log.LogInformation("Acao {Action} resultado {Result} duracao {Ms}",c.ActionDescriptor.DisplayName,result.Result?.GetType().Name,sw.Elapsed.TotalMilliseconds);}
}
