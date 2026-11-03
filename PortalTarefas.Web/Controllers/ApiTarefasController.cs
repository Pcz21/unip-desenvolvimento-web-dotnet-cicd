using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace PortalTarefas.Web.Controllers;
public class CriarTarefaDto:TarefaInputModel {}
public class AtualizarTarefaDto:TarefaInputModel {}
public record ResumoTarefaDto(int Id,string Titulo,string Situacao,int Prioridade,string? Observacao=null);
public record DetalheTarefaDto(int Id,string Titulo,string Descricao,string Situacao,int Prioridade,DateTime Prazo,byte[] Versao,string Equipe);
public record PaginaTarefas(List<ResumoTarefaDto> Itens,int Pagina,int Tamanho,int Total);
[ApiController,Route("api/tarefas")]
public class ApiTarefasController(PortalTarefasDbContext db,ITarefaService svc,IAuthorizationService authorization):ControllerBase {
 static DetalheTarefaDto Detail(Tarefa t)=>new(t.Id,t.Titulo,t.Descricao,t.Situacao,t.Prioridade,t.Prazo,t.Versao,t.Equipe);
 [HttpGet(Name="ListarTarefas"),ProducesResponseType<PaginaTarefas>(200),ProducesResponseType<ProblemDetails>(400)]
 public async Task<IActionResult> Listar(int pagina=1,int tamanho=5,string ordenarPor="id",string direcao="asc",string? situacao=null,int? prioridade=null,string? busca=null){
  if(pagina<1||tamanho<1||tamanho>50||!new[]{"id","titulo","prioridade"}.Contains(ordenarPor)||!new[]{"asc","desc"}.Contains(direcao))return Problem(statusCode:400,title:"Paginacao ou ordenacao invalida");
  var q=db.Tarefas.AsNoTracking().AsQueryable();if(situacao!=null)q=q.Where(t=>t.Situacao==situacao);if(prioridade!=null)q=q.Where(t=>t.Prioridade==prioridade);if(busca!=null)q=q.Where(t=>t.Titulo.Contains(busca));
  int total=await q.CountAsync();q=(ordenarPor,direcao) switch {("titulo","asc")=>q.OrderBy(t=>t.Titulo).ThenBy(t=>t.Id),("titulo","desc")=>q.OrderByDescending(t=>t.Titulo).ThenBy(t=>t.Id),("prioridade","asc")=>q.OrderBy(t=>t.Prioridade).ThenBy(t=>t.Id),("prioridade","desc")=>q.OrderByDescending(t=>t.Prioridade).ThenBy(t=>t.Id),(_,"desc")=>q.OrderByDescending(t=>t.Id),_=>q.OrderBy(t=>t.Id)};
  return Ok(new PaginaTarefas(await q.Skip((pagina-1)*tamanho).Take(tamanho).Select(t=>new ResumoTarefaDto(t.Id,t.Titulo,t.Situacao,t.Prioridade)).ToListAsync(),pagina,tamanho,total));
 }
 [HttpGet("{id:int}",Name="ObterTarefa"),ProducesResponseType<DetalheTarefaDto>(200),ProducesResponseType<ProblemDetails>(404)]
 public async Task<IActionResult> Obter(int id)=>await svc.FindAsync(id) is {} t?Ok(Detail(t)):Problem(statusCode:404,title:"Tarefa inexistente");
 [Authorize, HttpPost(Name="CriarTarefa"),ProducesResponseType<DetalheTarefaDto>(201),ProducesResponseType<ValidationProblemDetails>(400)]
 public async Task<IActionResult> Criar(CriarTarefaDto input){var t=await svc.SaveAsync(null,input);return CreatedAtRoute("ObterTarefa",new{id=t.Id},Detail(t));}
 [Authorize, HttpPut("{id:int}",Name="AtualizarTarefa"),ProducesResponseType<DetalheTarefaDto>(200),ProducesResponseType<ProblemDetails>(409),ProducesResponseType<ProblemDetails>(404),ProducesResponseType<ValidationProblemDetails>(400)]
 public async Task<IActionResult> Atualizar(int id,AtualizarTarefaDto input){if(await svc.FindAsync(id) is null)return Problem(statusCode:404);if((await svc.FindAsync(id))!.Equipe!=User.FindFirstValue("equipe")&&!(await authorization.AuthorizeAsync(User,null,"OutraEquipe")).Succeeded)return Forbid();try{return Ok(Detail(await svc.SaveAsync(id,input)));}catch(DbUpdateConcurrencyException){return Problem(statusCode:409,title:"Conflito de edicao",detail:"Recarregue o recurso e reaplique conscientemente a alteracao.");}}
 [Authorize(Roles="Administrador"), HttpDelete("{id:int}",Name="ExcluirTarefa"),ProducesResponseType(204),ProducesResponseType<ProblemDetails>(404)]
 public async Task<IActionResult> Excluir(int id){var t=await db.Tarefas.FindAsync(id);if(t is null)return Problem(statusCode:404);db.Tarefas.Remove(t);await db.SaveChangesAsync();return NoContent();}
}


