"""Disposable CI infrastructure and deployment tests; never publish runtime secrets."""
from pathlib import Path
import argparse,datetime,hashlib,hmac,io,json,os,re,secrets,shutil,socket,ssl,subprocess,sys,tempfile,time,urllib.request,zipfile
ROOT=Path(__file__).resolve().parents[1]
RUN=re.sub('[^a-z0-9-]','',os.environ.get('GITHUB_RUN_ID',os.environ.get('UNIP_CI_ID','local20261102')).lower())
STATE=Path(os.environ.get('RUNNER_TEMP',tempfile.gettempdir()))/('unip-ci-'+RUN)
EVIDENCE=ROOT/'artifacts/evidence'
EVIDENCE.mkdir(parents=True,exist_ok=True)
def command(args,**kwargs):
 return subprocess.run([str(a) for a in args],check=True,text=True,encoding='utf-8',errors='replace',**kwargs)
def capture(args):return command(args,stdout=subprocess.PIPE).stdout.strip()
def load():return json.loads((STATE/'state.json').read_text())
def save(s):(STATE/'state.json').write_text(json.dumps(s),encoding='utf-8')
def event(name,**data):
 row={'time_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'event':name,**data}
 with (EVIDENCE/'deployment-events.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps(row)+'\n')
 print(json.dumps(row),flush=True)
def wait(check,seconds=180):
 last=None
 for _ in range(seconds//2):
  try:
   result=check()
   if result:return result
  except Exception as e:last=type(e).__name__
  time.sleep(2)
 raise RuntimeError('Timeout aguardando dependencia: '+str(last))
def freeport():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
def port(name,internal):
 return int(capture(['docker','port',name,str(internal)]).splitlines()[0].rsplit(':',1)[1])
def database_password():
 # Remove the root secret before spawning any child process; never persist it.
 seed=os.environ.pop('CI_SECRET_SEED','')
 if os.environ.get('REQUIRE_SECRET_STORE')=='true' and not seed:
  raise RuntimeError('GitHub Actions Secret UNIP_CI_SECRET_SEED obrigatorio; infraestrutura bloqueada.')
 if seed:
  if not re.fullmatch(r'[0-9a-fA-F]{64}',seed):
   raise RuntimeError('Formato invalido do secret de CI; nenhum valor foi registrado.')
  context='unip-ci:'+RUN+':'+os.environ.get('GITHUB_RUN_ATTEMPT','local')+':'+secrets.token_hex(16)
  material=hmac.new(bytes.fromhex(seed),context.encode(),hashlib.sha256).hexdigest()
  source='github_actions_secrets'
 else:
  material=secrets.token_hex(32);source='ephemeral_local'
 password='Ci!'+material+'Aa9'
 if os.environ.get('GITHUB_ACTIONS')=='true':
  if seed:print('::add-mask::'+seed)
  print('::add-mask::'+password)
 return password,source

def init():
 password,source=database_password()
 STATE.mkdir(parents=True,exist_ok=False)
 prefix='unip-ci-'+RUN
 s={'prefix':prefix,'network':prefix+'-net','sql':prefix+'-sql','dependency':prefix+'-dependency','gateway':prefix+'-gateway','baseline':prefix+'-a','candidate':prefix+'-b','password':password}
 save(s)
 (STATE/'sql.env').write_text('ACCEPT_EULA=Y\nMSSQL_SA_PASSWORD='+password+'\n',encoding='utf-8')
 command(['docker','network','create',s['network']],stdout=subprocess.DEVNULL)
 command(['docker','run','-d','--name',s['sql'],'--network',s['network'],'--env-file',STATE/'sql.env','-p','127.0.0.1::1433','mcr.microsoft.com/mssql/server:2025-latest'],stdout=subprocess.DEVNULL)
 wait(lambda:subprocess.run(['docker','exec',s['sql'],'sh','-c','SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q "SELECT 1"'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0)
 if source=='github_actions_secrets':
  event('secret_store_verified',provider='GitHub Actions Secrets',secret_name='UNIP_CI_SECRET_SEED',database_authentication='verified',credential='derived_per_execution',root_secret_persisted=False)
 shutil.copy2(ROOT/'Ferramentas/dependency.cjs',STATE/'dependency.cjs')
 command(['docker','run','-d','--name',s['dependency'],'--network',s['network'],'--mount',f'type=bind,source={STATE / "dependency.cjs"},target=/dependency.cjs,readonly','node:24.12.0-bookworm-slim','node','/dependency.cjs'],stdout=subprocess.DEVNULL)
 cs=f'Server=127.0.0.1,{port(s["sql"],1433)};Database=PortalIntegration;User ID=sa;Password={password};TrustServerCertificate=True'
 (STATE/'test-connection.txt').write_text(cs,encoding='utf-8')
 if os.environ.get('GITHUB_ACTIONS')=='true':
  print('::add-mask::'+password);print('::add-mask::'+cs)
  with open(os.environ['GITHUB_ENV'],'a',encoding='utf-8') as f:f.write('ConnectionStrings__Portal='+cs+'\n')
 event('infrastructure_ready',database='isolated',password='not_recorded')
def images():
 s=load();current=capture(['git','rev-parse','HEAD']);baseline=capture(['git','rev-parse',os.environ.get('BASELINE_REF','HEAD^')]);s.update(current_sha=current,baseline_sha=baseline)
 archive=subprocess.check_output(['git','archive','--format=zip',baseline],cwd=ROOT)
 target=STATE/'baseline-source';target.mkdir(exist_ok=True)
 with zipfile.ZipFile(io.BytesIO(archive)) as z:
  for n in z.namelist():assert not n.startswith('/') and '..' not in Path(n).parts
  z.extractall(target)
 for kind,context,sha in [('baseline',target,baseline),('candidate',ROOT,current)]:
  tag='unip-cicd:'+RUN+'-'+kind;s[kind+'_image']=tag
  command(['docker','build','--build-arg','RELEASE_SHA='+sha,'-t',tag,context])
  s[kind+'_digest']=capture(['docker','image','inspect',tag,'--format','{{.Id}}'])
  assert capture(['docker','image','inspect',tag,'--format','{{.Config.User}}'])=='app'
 save(s);event('images_built',baseline_commit=baseline,candidate_commit=current,baseline_digest=s['baseline_digest'],candidate_digest=s['candidate_digest'])
def deploy():
 s=load()
 connection=f'Server={s["sql"]},1433;Database=PortalStaging;User ID=sa;Password={s["password"]};TrustServerCertificate=True'
 (STATE/'app.env').write_text('ASPNETCORE_ENVIRONMENT=Production\nASPNETCORE_FORWARDEDHEADERS_ENABLED=true\nConnectionStrings__Portal='+connection+'\nDependency__Url=http://'+s['dependency']+':5399\n',encoding='utf-8')
 for kind in ['baseline','candidate']:
  command(['docker','run','-d','--name',s[kind],'--network',s['network'],'--env-file',STATE/'app.env','-p','127.0.0.1::8080',s[kind+'_image']],stdout=subprocess.DEVNULL)
  wait(lambda:urllib.request.urlopen('http://127.0.0.1:'+str(port(s[kind],8080))+'/health/ready',timeout=3).status==200)
 openssl=shutil.which('openssl') or 'C:/Program Files/Git/usr/bin/openssl.exe'
 command([openssl,'req','-x509','-newkey','rsa:2048','-nodes','-keyout',STATE/'tls.key','-out',STATE/'tls.crt','-days','1','-subj','/CN=localhost','-addext','subjectAltName=DNS:localhost,IP:127.0.0.1'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 write_proxy(s,'baseline')
 command(['docker','run','-d','--name',s['gateway'],'--network',s['network'],'-p','127.0.0.1::80','-p','127.0.0.1::443','--mount',f'type=bind,source={STATE},target=/ci,readonly','nginx:1.28-alpine','nginx','-c','/ci/nginx.conf','-g','daemon off;'],stdout=subprocess.DEVNULL)
 s['http_port']=port(s['gateway'],80);s['https_port']=port(s['gateway'],443);save(s)
 smoke('baseline_deployed','baseline')
def write_proxy(s,kind):
 config='''events {}
http {
  server {
    listen 80;
    listen 443 ssl;
    ssl_certificate /ci/tls.crt;
    ssl_certificate_key /ci/tls.key;
    location / {
      proxy_pass http://UPSTREAM:8080;
      proxy_set_header Host $host;
      proxy_set_header X-Forwarded-Proto https;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
  }
}
'''.replace('UPSTREAM',s[kind])
 (STATE/'nginx.conf').write_text(config,encoding='utf-8')
def smoke(name,kind,external=None):
 s=load();base=external or 'https://localhost:'+str(s['https_port']);ctx=None if external else ssl.create_default_context(cafile=str(STATE/'tls.crt'))
 expected=s['baseline_sha' if kind=='baseline' else 'current_sha']
 def ready():
  with urllib.request.urlopen(base+'/deployment-version.json',context=ctx,timeout=10) as r:return json.load(r)['commit']==expected
 wait(ready,120)
 checks={}
 for path in ['/health/live','/health/ready','/app/','/api/tarefas?tamanho=1','/deployment-version.json']:
  with urllib.request.urlopen(base+path,context=ctx,timeout=15) as r:
   body=r.read().decode();assert r.status==200
   if path=='/app/':assert '<div id="root">' in body
   if path.startswith('/api/'):assert len(json.loads(body)['itens'])==1
   checks[path]=r.status
 actual=capture(['docker','inspect',s[kind],'--format','{{.Image}}']);assert actual==s[kind+'_digest']
 event(name,url=base,commit=expected,image=actual,checks=checks,tls_verification='trusted_public_CA' if external else 'local_certificate_explicitly_trusted')
def switch(kind,name):
 s=load();write_proxy(s,kind);command(['docker','exec',s['gateway'],'nginx','-c','/ci/nginx.conf','-s','reload']);smoke(name,kind)
def rollback():
 switch('candidate','candidate_deployed');switch('baseline','rollback_verified');switch('candidate','candidate_restored')
def expose():
 if os.environ.get('EXTERNAL_DEPLOYMENT_APPROVED')!='true':raise RuntimeError('Implantacao externa nao autorizada.')
 s=load();cloudflared=shutil.which('cloudflared')
 if not cloudflared:raise RuntimeError('Instale cloudflared da fonte oficial antes de expor staging.')
 log=STATE/'tunnel.log';f=log.open('w',encoding='utf-8')
 process=subprocess.Popen([cloudflared,'tunnel','--url','http://127.0.0.1:'+str(s['http_port']),'--no-autoupdate'],stdout=f,stderr=subprocess.STDOUT)
 s['tunnel_pid']=process.pid;save(s)
 url=wait(lambda:(re.search(r'https://[a-z0-9-]+\.trycloudflare\.com',log.read_text(encoding='utf-8',errors='replace')) or [None])[0],120)
 s['public_url']=url;save(s)
 smoke('public_https_validated','candidate',url)
 if os.environ.get('GITHUB_STEP_SUMMARY'):
  with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as f:f.write('## Staging HTTPS temporario\n'+url+'\n\nEncerra ao terminar o job.\n')
 for kind,label in [('baseline','public_rollback_verified'),('candidate','public_candidate_restored')]:
  write_proxy(s,kind);command(['docker','exec',s['gateway'],'nginx','-c','/ci/nginx.conf','-s','reload']);smoke(label,kind,url)
 event('public_staging_available',url=url)
 deadline=time.monotonic()+int(os.environ.get('STAGING_HOLD_SECONDS','900'))
 while time.monotonic()<deadline:time.sleep(5)
def cleanup():
 if not (STATE/'state.json').exists():return
 s=load()
 if 'tunnel_pid' in s:
  try:os.kill(s['tunnel_pid'],15)
  except ProcessLookupError:pass
 for key in ['gateway','candidate','baseline','dependency','sql']:
  name=s[key];assert name.startswith('unip-ci-'+RUN+'-')
  subprocess.run(['docker','rm','-f',name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 subprocess.run(['docker','network','rm',s['network']],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 event('disposable_environment_stopped')
def testenv():
 env=os.environ.copy();env['ConnectionStrings__Portal']=(STATE/'test-connection.txt').read_text();command(['dotnet','run','--project','PortalTarefas.Tests','-c','Release','--no-build'],cwd=ROOT,env=env)
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('operation',choices=['init','images','deploy','rollback','expose','cleanup','testenv']);op=parser.parse_args().operation
 globals()[op]()
