using System.ComponentModel.DataAnnotations;
namespace PortalTarefas.Web;
public class Tarefa {
 public int Id {get;set;} public string Titulo {get;set;}=""; public string Descricao {get;set;}="";
 public string Situacao {get;set;}="Aberta"; public int Prioridade {get;set;}=1;
 public DateTime Prazo {get;set;}=DateTime.Today.AddDays(7); public byte[] Versao {get;set;}=[];
 public string Equipe {get;set;}="A";
}
public class TarefaInputModel : IValidatableObject {
 [Required(ErrorMessage="Informe o titulo."),StringLength(120,MinimumLength=3,ErrorMessage="Titulo deve ter entre 3 e 120 caracteres.")]
 public string Titulo {get;set;}="";
 [StringLength(1000,ErrorMessage="Descricao deve ter ate 1000 caracteres.")]
 public string Descricao {get;set;}="";
 [Range(1,3,ErrorMessage="Prioridade deve estar entre 1 e 3.")] public int Prioridade {get;set;}=1;
 public DateTime Prazo {get;set;}=DateTime.Today.AddDays(7); public byte[] Versao {get;set;}=[];
 public IEnumerable<ValidationResult> Validate(ValidationContext c) {
  if(Prioridade==3 && Prazo.Date>DateTime.Today.AddDays(7)) yield return new("Prioridade alta exige prazo de ate sete dias.",[nameof(Prazo),nameof(Prioridade)]);
  if(Prazo.Date<DateTime.Today) yield return new("Prazo nao pode estar no passado.",[nameof(Prazo)]);
 }
}
public interface ITarefaService {
 Task<List<Tarefa>> ListAsync(); Task<Tarefa?> FindAsync(int id); Task<Tarefa> SaveAsync(int? id,TarefaInputModel input,bool fail=false);
}
public class MemoryTarefaService : ITarefaService {
 readonly List<Tarefa> items=[new(){Id=1,Titulo="Estudar pipeline HTTP",Descricao="Portal de Tarefas"}];
 public Task<List<Tarefa>> ListAsync()=>Task.FromResult(items.ToList());
 public Task<Tarefa?> FindAsync(int id)=>Task.FromResult(items.FirstOrDefault(t=>t.Id==id));
 public Task<Tarefa> SaveAsync(int? id,TarefaInputModel i,bool fail=false) {
  var t=id is null?new Tarefa{Id=items.Count+1}:items.First(t=>t.Id==id);
  t.Titulo=i.Titulo;t.Descricao=i.Descricao;t.Prioridade=i.Prioridade;t.Prazo=i.Prazo;
  if(id is null)items.Add(t); return Task.FromResult(t);
 }
}
