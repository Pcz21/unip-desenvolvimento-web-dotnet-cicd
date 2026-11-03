process.chdir(require('path').resolve(__dirname,'../../..'));
const fs=require('fs');const https=require('https');const out='ENTREGA_FINAL_UNIP_DESENVOLVIMENTO_WEB/Evidencias/';
async function req(path,method='GET',body){return new Promise((res,rej)=>{let r=https.request('https://localhost:7240'+path,{method,rejectUnauthorized:false,headers:{'Content-Type':'application/json'}},s=>{let b='';s.on('data',d=>b+=d);s.on('end',()=>res({path,method,status:s.statusCode,headers:s.headers,body:b?JSON.parse(b):null}));});r.on('error',rej);if(body)r.write(JSON.stringify(body));r.end()});}
(async()=>{let rows=[];async function check(path,expected,method='GET',body){let r=await req(path,method,body);r.pass=r.status===expected;rows.push(r);if(!r.pass)throw Error(JSON.stringify(r));return r;}
for(let p of ['/api/tarefas?pagina=0','/api/tarefas?tamanho=0','/api/tarefas?tamanho=51','/api/tarefas?ordenarPor=senha'])await check(p,400);
for(let p of ['/api/tarefas?tamanho=1','/api/tarefas?tamanho=50','/api/tarefas?ordenarPor=titulo&direcao=desc','/api/tarefas?situacao=Aberta&prioridade=2','/api/tarefas?pagina=9999'])await check(p,200);
await check('/api/tarefas/99999',404);await check('/api/tarefas',400,'POST',{titulo:''});
let c=await check('/api/tarefas',201,'POST',{titulo:'Contrato API',descricao:'Teste real',prioridade:1,prazo:'2026-11-05'});if(!c.headers.location)throw Error('Location');let id=c.body.id;
await check('/api/tarefas/'+id,200);await check('/api/tarefas/'+id,200,'PUT',{...c.body,titulo:'Edicao A'});await check('/api/tarefas/'+id,409,'PUT',{...c.body,titulo:'Edicao B'});await check('/api/tarefas/'+id,204,'DELETE');
fs.writeFileSync(out+'u4-testes.json',JSON.stringify(rows,null,2));console.log(rows.map(r=>`PASS ${r.method} ${r.path}: ${r.status}`).join('\n'));})();
