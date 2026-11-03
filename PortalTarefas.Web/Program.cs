using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PortalTarefas.Web;
var b=WebApplication.CreateBuilder(args);
b.Services.AddOpenApi();b.Services.AddProblemDetails(); b.Services.AddControllersWithViews(o=>{o.Filters.Add<ActionLogFilter>();o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes=true;});
var connection=b.Configuration.GetConnectionString("Portal")??throw new InvalidOperationException("Configure ConnectionStrings:Portal externamente.");
b.Services.AddDbContextPool<PortalTarefasDbContext>(o=>o.UseSqlServer(connection));
b.Services.AddScoped<ITarefaService,EfTarefaService>();b.Services.AddScoped<PainelService>();
b.Services.AddCors(o=>o.AddPolicy("FrontendLocal",p=>p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
b.Services.AddIdentity<IdentityUser,IdentityRole>(o=>{o.Password.RequiredLength=12;o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(5);}).AddEntityFrameworkStores<PortalTarefasDbContext>().AddDefaultTokenProviders();
b.Services.AddSingleton<RevokedSessions>();
b.Services.ConfigureApplicationCookie(o=>{o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;o.ExpireTimeSpan=TimeSpan.FromMinutes(20);o.SlidingExpiration=false;o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};o.Events.OnValidatePrincipal=c=>{var revoked=c.HttpContext.RequestServices.GetRequiredService<RevokedSessions>();var now=DateTimeOffset.UtcNow;foreach(var pair in revoked.Items)if(pair.Value<now)revoked.Items.TryRemove(pair.Key,out _);if(c.Principal?.FindFirstValue("sid") is {} sid&&revoked.Items.ContainsKey(sid))c.RejectPrincipal();return Task.CompletedTask;};});
b.Services.AddAuthorization(o=>o.AddPolicy("OutraEquipe",p=>p.RequireClaim("editar_outra_equipe","sim")));
b.Services.AddAntiforgery(o=>o.HeaderName="X-CSRF-TOKEN");
b.Logging.AddJsonConsole(o=>o.IncludeScopes=true);
b.Services.AddOpenTelemetry().WithTracing(t=>t.AddSource("PortalTarefas").AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddConsoleExporter()).WithMetrics(m=>m.AddMeter("PortalTarefas").AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddConsoleExporter((e,r)=>r.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds=3000));
b.Services.AddHealthChecks().AddCheck<ReadyCheck>("dependencias",tags:["ready"]);
b.Services.AddHttpClient("health",c=>c.Timeout=TimeSpan.FromSeconds(2));
b.Services.AddHttpClient("externo",c=>c.BaseAddress=new Uri(b.Configuration["Dependency:Url"]??"http://localhost:5399")).AddStandardResilienceHandler(o=>{o.Retry.MaxRetryAttempts=2;o.Retry.Delay=TimeSpan.FromMilliseconds(100);o.Retry.UseJitter=false;o.Retry.DisableForUnsafeHttpMethods();o.AttemptTimeout.Timeout=TimeSpan.FromSeconds(1);o.TotalRequestTimeout.Timeout=TimeSpan.FromSeconds(5);o.CircuitBreaker.MinimumThroughput=4;o.CircuitBreaker.FailureRatio=.5;o.CircuitBreaker.SamplingDuration=TimeSpan.FromSeconds(10);o.CircuitBreaker.BreakDuration=TimeSpan.FromSeconds(5);});
b.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("didatico",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new(){PermitLimit=3,Window=TimeSpan.FromSeconds(10),QueueLimit=0}));});
b.Services.AddOutputCache(o=>o.AddPolicy("publico",p=>p.Expire(TimeSpan.FromSeconds(5))));
if(!Uri.TryCreate(b.Configuration["Dependency:Url"],UriKind.Absolute,out var dependencyUri))throw new InvalidOperationException("Configure Dependency:Url com URL absoluta.");
var app=b.Build();
using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<PortalTarefasDbContext>();await db.Database.MigrateAsync();await Seed.RunAsync(db);await Auth.SeedAsync(scope.ServiceProvider,b.Configuration);}
app.UseRequestLog();app.UseExceptionHandler();app.UseHttpsRedirection();app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseOutputCache();app.UseCors("FrontendLocal");app.UseAuthentication();app.UseAuthorization();
app.Use(async(c,next)=>{if(!HttpMethods.IsGet(c.Request.Method)&&!HttpMethods.IsHead(c.Request.Method)&&!HttpMethods.IsOptions(c.Request.Method)){try{await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}catch(AntiforgeryValidationException){await Results.Problem(statusCode:400,title:"Verificacao CSRF falhou").ExecuteAsync(c);return;}}await next();});
app.MapAuth();
app.MapHealthChecks("/health/live",new HealthCheckOptions{Predicate=_=>false});
app.MapHealthChecks("/health/ready",new HealthCheckOptions{Predicate=r=>r.Tags.Contains("ready")});
app.MapGet("/publico/resumo",()=>new{geradoEm=DateTimeOffset.UtcNow}).CacheOutput("publico");
app.MapGet("/publico/limitado",()=>Results.Ok(new{mensagem="Dentro do limite"})).RequireRateLimiting("didatico");
if(app.Environment.IsDevelopment())app.MapGet("/lab/consultar",async(IHttpClientFactory f)=>{using var span=Signals.Source.StartActivity("dependencia.consultar");try{using var r=await f.CreateClient("externo").GetAsync("/data");return Results.Json(new{status=(int)r.StatusCode},statusCode:(int)r.StatusCode);}catch(Polly.CircuitBreaker.BrokenCircuitException){return Results.Problem(statusCode:503,title:"Circuito aberto");}catch{return Results.Problem(statusCode:503,title:"Dependencia indisponivel");}});

app.MapGet("/diagnostico/minimal",()=>new{aplicacao="PortalTarefas",versao=1});
app.MapGet("/diagnostico/erro",IResult ()=>throw new InvalidOperationException("Falha didatica controlada"));
var g=app.MapGroup("/u1/tarefas");
g.MapGet("/",async(string? situacao,[FromHeader(Name="X-Cliente")]string? cliente,ITarefaService s,HttpContext c)=>{c.Response.Headers["X-Cliente-Recebido"]=cliente??"ausente";return (await s.ListAsync()).Where(t=>situacao==null||t.Situacao==situacao);});
g.MapGet("/{id:int}",async(int id,ITarefaService s)=>await s.FindAsync(id) is {} t?Results.Ok(t):Results.Problem(statusCode:404));
g.MapPost("/",async(TarefaInputModel i,ITarefaService s)=>{var errors=new List<System.ComponentModel.DataAnnotations.ValidationResult>();if(!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(i,new(i),errors,true))return Results.ValidationProblem(errors.SelectMany(e=>e.MemberNames.Select(m=>(m,e.ErrorMessage))).GroupBy(x=>x.m).ToDictionary(g=>g.Key,g=>g.Select(x=>x.ErrorMessage!).ToArray()));var t=await s.SaveAsync(null,i);return Results.Created($"/u1/tarefas/{t.Id}",t);}).RequireAuthorization();
if(app.Environment.IsDevelopment())app.MapOpenApi();
app.MapControllers();app.MapControllerRoute("default","{controller=Tarefas}/{action=Index}/{id?}");app.MapFallbackToFile("/app/{*path:nonfile}","app/index.html");app.Run();



