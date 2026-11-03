using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;
using System.Collections.Concurrent;
namespace PortalTarefas.Web;
public class RevokedSessions {public ConcurrentDictionary<string,DateTimeOffset> Items{get;}=new();}
public record LoginInput(string Usuario,string Senha);
public static class Auth {
 public static async Task SeedAsync(IServiceProvider services,IConfiguration config){
  var password=config["Seed:Password"];if(string.IsNullOrEmpty(password))return;
  var roles=services.GetRequiredService<RoleManager<IdentityRole>>();var users=services.GetRequiredService<UserManager<IdentityUser>>();
  foreach(var role in new[]{"Usuario","Administrador"})if(!await roles.RoleExistsAsync(role))await roles.CreateAsync(new(role));
  foreach(var name in new[]{"usuario","administrador"}){var u=await users.FindByNameAsync(name);if(u is null){u=new(){UserName=name};var result=await users.CreateAsync(u,password);if(!result.Succeeded)throw new InvalidOperationException("Senha de seed nao atende a politica.");await users.AddToRoleAsync(u,name=="usuario"?"Usuario":"Administrador");await users.AddClaimAsync(u,new("equipe","A"));if(name=="administrador")await users.AddClaimAsync(u,new("editar_outra_equipe","sim"));}}
 }
 public static void MapAuth(this WebApplication app){
  app.MapGet("/auth/csrf",(HttpContext c,IAntiforgery a)=>{c.Response.Headers.CacheControl="no-store";return Results.Ok(new{token=a.GetAndStoreTokens(c).RequestToken});});
  app.MapPost("/auth/login",async(LoginInput input,UserManager<IdentityUser> users,SignInManager<IdentityUser> sign,HttpContext c)=>{
   var u=await users.FindByNameAsync(input.Usuario);if(u is null){await Task.Delay(120);return Results.Problem(statusCode:401,title:"Credenciais invalidas");}
   var result=await sign.CheckPasswordSignInAsync(u,input.Senha,true);if(!result.Succeeded)return Results.Problem(statusCode:401,title:"Credenciais invalidas");
   var principal=await sign.CreateUserPrincipalAsync(u);((ClaimsIdentity)principal.Identity!).AddClaim(new("sid",Guid.NewGuid().ToString("N")));
   await c.SignInAsync(IdentityConstants.ApplicationScheme,principal,new AuthenticationProperties{IsPersistent=false,ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(20)});return Results.NoContent();
  });
  app.MapPost("/auth/logout",async(HttpContext c,RevokedSessions revoked)=>{var sid=c.User.FindFirstValue("sid");if(sid!=null)revoked.Items[sid]=DateTimeOffset.UtcNow.AddMinutes(20);await c.SignOutAsync(IdentityConstants.ApplicationScheme);return Results.NoContent();}).RequireAuthorization();
  app.MapGet("/auth/session",(HttpContext c)=>{c.Response.Headers.CacheControl="no-store";return Results.Ok(new{authenticated=c.User.Identity?.IsAuthenticated??false,nome=c.User.Identity?.Name,admin=c.User.IsInRole("Administrador")});});
  app.MapGet("/api/privado",()=>Results.Ok(new{mensagem="Leitura privada"})).RequireAuthorization();
  app.MapGet("/api/admin",()=>Results.Ok(new{mensagem="Administracao"})).RequireAuthorization(p=>p.RequireRole("Administrador"));
 }
}
