using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web;
using System.Text.Json;
var cs=Environment.GetEnvironmentVariable("ConnectionStrings__Portal")??throw new Exception("Configure ConnectionStrings__Portal com banco isolado.");
var options=new DbContextOptionsBuilder<PortalTarefasDbContext>().UseSqlServer(cs).Options;
var rows=new List<object>();
void Check(string name,bool pass){rows.Add(new{name,pass});Console.WriteLine($"{(pass?"PASS":"FAIL")} {name}");if(!pass)throw new Exception(name);}
await using var db=new PortalTarefasDbContext(options);
await db.Database.MigrateAsync();await Seed.RunAsync(db);int n=await db.Tarefas.CountAsync();await Seed.RunAsync(db);Check("Migration em banco isolado e seed idempotente",n==15&&await db.Tarefas.CountAsync()==15);
var svc=new EfTarefaService(db);var i=new TarefaInputModel{Titulo="Transacao de teste",Prazo=DateTime.Today.AddDays(2)};
var t=await svc.SaveAsync(null,i);Check("Commit tarefa e auditoria",await db.Auditorias.AnyAsync(a=>a.TarefaId==t.Id));
var before=await db.Tarefas.CountAsync();int audit=await db.Auditorias.CountAsync();try{await svc.SaveAsync(null,i,true);}catch(InvalidOperationException){}db.ChangeTracker.Clear();Check("Rollback tarefa e auditoria",await db.Tarefas.CountAsync()==before&&await db.Auditorias.CountAsync()==audit);
await using var db2=new PortalTarefasDbContext(options);var a=await db.Tarefas.FirstAsync();var b=await db2.Tarefas.SingleAsync(x=>x.Id==a.Id);a.Titulo="Sessao A";await db.SaveChangesAsync();b.Titulo="Sessao B";bool conflict=false;try{await db2.SaveChangesAsync();}catch(DbUpdateConcurrencyException){conflict=true;}Check("Duas sessoes detectam concorrencia",conflict);
var panel=new PainelService(db);Check("EF projetado igual a ADO parametrizado",(await panel.EfAsync(2)).SequenceEqual(await panel.AdoAsync(2)));
if(Environment.GetEnvironmentVariable("FORCE_TEST_FAILURE")=="1")Check("Falha deliberada de gate",false);
Console.WriteLine(JsonSerializer.Serialize(rows));
