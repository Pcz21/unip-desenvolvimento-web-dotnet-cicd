using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
namespace PortalTarefas.Web.Controllers;
public class TarefasController(ITarefaService service,Microsoft.AspNetCore.Authorization.IAuthorizationService authorization):Controller {
 public async Task<IActionResult> Index()=>View(await service.ListAsync());
 public async Task<IActionResult> Details(int id)=>await service.FindAsync(id) is {} t?View(t):NotFound();
 [Microsoft.AspNetCore.Authorization.Authorize] public IActionResult Create()=>View(new TarefaInputModel());
 [Microsoft.AspNetCore.Authorization.Authorize,HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Create(TarefaInputModel input){if(!ModelState.IsValid)return View(input);var t=await service.SaveAsync(null,input);return RedirectToAction(nameof(Details),new{id=t.Id});}
 [Microsoft.AspNetCore.Authorization.Authorize] public async Task<IActionResult> Edit(int id){var t=await service.FindAsync(id);if(t is null)return NotFound();if(t.Equipe!=User.FindFirst("equipe")?.Value&&!(await authorization.AuthorizeAsync(User,null,"OutraEquipe")).Succeeded)return Forbid();return View(new TarefaInputModel{Titulo=t.Titulo,Descricao=t.Descricao,Prioridade=t.Prioridade,Prazo=t.Prazo,Versao=t.Versao});}
 [Microsoft.AspNetCore.Authorization.Authorize,HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Edit(int id,TarefaInputModel input){var resource=await service.FindAsync(id);if(resource is null)return NotFound();if(resource.Equipe!=User.FindFirst("equipe")?.Value&&!(await authorization.AuthorizeAsync(User,null,"OutraEquipe")).Succeeded)return Forbid();if(!ModelState.IsValid)return View(input);try{await service.SaveAsync(id,input);}catch(DbUpdateConcurrencyException){ModelState.AddModelError("","Conflito de edicao. Recarregue os detalhes antes de tentar novamente.");return View(input);}return RedirectToAction(nameof(Details),new{id});}
}


