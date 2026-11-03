using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace PortalTarefas.Web;
public class Auditoria { public int Id{get;set;} public int TarefaId{get;set;} public string Evento{get;set;}=""; public DateTime Em{get;set;}=DateTime.UtcNow; }
public class PortalTarefasDbContext(DbContextOptions<PortalTarefasDbContext> options):IdentityDbContext<IdentityUser>(options) {
 public DbSet<Tarefa> Tarefas=>Set<Tarefa>(); public DbSet<Auditoria> Auditorias=>Set<Auditoria>();
 protected override void OnModelCreating(ModelBuilder b){base.OnModelCreating(b);b.Entity<Tarefa>(e=>{e.HasKey(t=>t.Id);e.Property(t=>t.Titulo).HasMaxLength(120).IsRequired();e.Property(t=>t.Descricao).HasMaxLength(1000);e.Property(t=>t.Situacao).HasMaxLength(30);e.Property(t=>t.Equipe).HasMaxLength(30);e.Property(t=>t.Versao).IsRowVersion();e.HasIndex(t=>new{t.Situacao,t.Prioridade});});b.Entity<Auditoria>().Property(a=>a.Evento).HasMaxLength(80);}
}
public static class Seed {
 public static async Task RunAsync(PortalTarefasDbContext db){if(!await db.Tarefas.AnyAsync()){db.Tarefas.AddRange(Enumerable.Range(1,15).Select(i=>new Tarefa{Titulo=$"Tarefa didatica {i:00}",Descricao="Seed idempotente do Portal de Tarefas",Prioridade=1+i%3,Situacao=i%2==0?"Concluida":"Aberta",Prazo=DateTime.Today.AddDays(i)}));await db.SaveChangesAsync();}}
}
public class EfTarefaService(PortalTarefasDbContext db):ITarefaService {
 public Task<List<Tarefa>> ListAsync()=>db.Tarefas.AsNoTracking().OrderBy(t=>t.Id).ToListAsync();
 public async Task<Tarefa?> FindAsync(int id){using var span=Signals.Source.StartActivity("db.tarefas.consultar");span?.SetTag("db.system","mssql");return await db.Tarefas.AsNoTracking().SingleOrDefaultAsync(t=>t.Id==id);}
 public async Task<Tarefa> SaveAsync(int? id,TarefaInputModel i,bool fail=false){
  using var span=Signals.Source.StartActivity("tarefa.salvar");
  using var tx=await db.Database.BeginTransactionAsync();
  var t=id is null?new Tarefa():await db.Tarefas.SingleAsync(t=>t.Id==id);
  if(id is not null)db.Entry(t).Property(x=>x.Versao).OriginalValue=i.Versao;
  t.Titulo=i.Titulo;t.Descricao=i.Descricao??"";t.Prioridade=i.Prioridade;t.Prazo=i.Prazo;
  if(id is null)db.Tarefas.Add(t);
  await db.SaveChangesAsync();db.Auditorias.Add(new(){TarefaId=t.Id,Evento=id is null?"Criacao":"Edicao"});await db.SaveChangesAsync();
  if(fail)throw new InvalidOperationException("Rollback didatico");await tx.CommitAsync();Signals.Saves.Add(1);return t;
 }
}
public record PainelLinha(string Situacao,int Total);
public class PainelService(PortalTarefasDbContext db){
 public Task<List<PainelLinha>> EfAsync(int minimo)=>db.Tarefas.AsNoTracking().Where(t=>t.Prioridade>=minimo).GroupBy(t=>t.Situacao).OrderBy(g=>g.Key).Select(g=>new PainelLinha(g.Key,g.Count())).ToListAsync();
 public async Task<List<PainelLinha>> AdoAsync(int minimo){var result=new List<PainelLinha>();var conn=db.Database.GetDbConnection();await db.Database.OpenConnectionAsync();try{using var cmd=conn.CreateCommand();cmd.CommandText="SELECT Situacao,COUNT(*) FROM Tarefas WHERE Prioridade >= @min GROUP BY Situacao ORDER BY Situacao";var p=cmd.CreateParameter();p.ParameterName="@min";p.Value=minimo;cmd.Parameters.Add(p);using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())result.Add(new(r.GetString(0),r.GetInt32(1)));}finally{await db.Database.CloseConnectionAsync();}return result;}
}
