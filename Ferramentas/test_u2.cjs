process.chdir(require('path').resolve(__dirname,'../../..'));
const fs=require('fs');const{chromium}=require('playwright');
(async()=>{let browser=await chromium.launch({channel:'msedge',headless:true});let page=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width:1280,height:900}});let rows=[];const out='ENTREGA_FINAL_UNIP_DESENVOLVIMENTO_WEB/Evidencias/';
async function check(name,ok){rows.push({name,pass:!!ok});if(!ok)throw Error(name);}
await page.goto('https://localhost:7240/');await check('Lista e layout',await page.locator('header').count()===1);await page.screenshot({path:out+'u2-lista.png'});
await page.goto('https://localhost:7240/Tarefas/Details/1');await check('Detalhes e layout',await page.locator('article').count()===1);await page.screenshot({path:out+'u2-detalhes.png'});
await page.goto('https://localhost:7240/Tarefas/Create');await page.locator('form').evaluate(f=>f.noValidate=true);await page.getByRole('button',{name:'Salvar'}).click();await check('Titulo vazio rejeitado',await page.locator('body').innerText().then(x=>x.includes('Informe o titulo')));await page.screenshot({path:out+'u2-validacao.png'});
await page.locator('#Titulo').fill('Prazo invalido preservado');await page.locator('#Prioridade').selectOption('3');await page.locator('#Prazo').fill('2027-12-01');await page.getByRole('button',{name:'Salvar'}).click();await check('Validacao entre campos',await page.locator('body').innerText().then(x=>x.includes('sete dias')));await check('Dados preservados',await page.locator('#Titulo').inputValue()==='Prazo invalido preservado');
await page.locator('#Prazo').fill('2026-11-05');await page.getByRole('button',{name:'Salvar'}).click();await check('Cadastro valido',/Details\/[0-9]+/.test(page.url()));
await page.getByRole('link',{name:'Editar',exact:true}).click();await page.locator('#Titulo').fill('Tarefa editada');await page.getByRole('button',{name:'Salvar'}).click();await check('Edicao valida',await page.locator('body').innerText().then(x=>x.includes('Tarefa editada')));
let r=await page.request.get('https://localhost:7240/Tarefas/Details/99999');await check('Identificador inexistente',r.status()===404);
fs.writeFileSync(out+'u2-testes.json',JSON.stringify(rows,null,2));console.log(rows);await browser.close();})();


