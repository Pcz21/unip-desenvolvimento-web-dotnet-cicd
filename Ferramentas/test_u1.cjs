process.chdir(require('path').resolve(__dirname,'../../..'));
const fs=require('fs'); const https=require('https');
const out='ENTREGA_FINAL_UNIP_DESENVOLVIMENTO_WEB/Evidencias/';
function req(path,method='GET',body,headers={}){return new Promise((resolve,reject)=>{const r=https.request('https://localhost:7240'+path,{method,rejectUnauthorized:false,headers:{'Content-Type':'application/json',...headers}},s=>{let b='';s.on('data',d=>b+=d);s.on('end',()=>resolve({path,method,status:s.statusCode,headers:s.headers,body:b}));});r.on('error',reject);if(body)r.write(JSON.stringify(body));r.end();});}
(async()=>{let rows=[];for(const [p,m,b,h,status] of [
['/u1/tarefas/1','GET',null,{},200],['/u1/tarefas/abc','GET',null,{},404],['/u1/tarefas/?situacao=Aberta','GET',null,{'X-Cliente':'laboratorio'},200],
['/u1/tarefas/','POST',{titulo:'Tarefa valida',prazo:'2026-11-09'}, {},201],['/u1/tarefas/','POST',{titulo:''},{},400],['/diagnostico/erro','GET',null,{},500],
['/diagnostico/minimal','GET',null,{Origin:'http://localhost:5173'},200],['/diagnostico/minimal','GET',null,{Origin:'https://indevida.local'},200],['/diagnostico/mvc','GET',null,{},200]]){
let r=await req(p,m,b,h);r.pass=r.status===status;if(h.Origin)r.pass&&=!!r.headers['access-control-allow-origin']===(h.Origin==='http://localhost:5173');rows.push(r);}
fs.writeFileSync(out+'u1-testes.json',JSON.stringify(rows,null,2)); console.log(rows.map(r=>`${r.pass?'PASS':'FAIL'} ${r.method} ${r.path}: ${r.status}`).join('\n'));if(rows.some(r=>!r.pass))process.exitCode=1;
const {chromium}=require('playwright');let browser=await chromium.launch({headless:true,channel:'msedge'});let page=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width:1280,height:800}});await page.goto('https://localhost:7240/diagnostico/mvc');await page.screenshot({path:out+'u1-diagnostico.png'});await browser.close();})();
