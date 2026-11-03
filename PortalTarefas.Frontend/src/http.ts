import {PortalClient} from './generated';
export class HttpError extends Error {constructor(public status:number,message:string,public fields:Record<string,string[]>={}){super(message)}}
let csrf='';export function setCsrf(value:string){csrf=value}
export async function request(path:string,init:RequestInit={}){
 let r:Response;try{r=await fetch(((import.meta as any).env.VITE_API_URL||'')+path,{...init,credentials:'same-origin',headers:{'Content-Type':'application/json','X-CSRF-TOKEN':csrf,...init.headers}})}catch(e){if((e as Error).name==='AbortError')throw e;throw new HttpError(0,'Falha de rede. Tente novamente.')}
 if(!r.ok){const p=await r.json().catch(()=>({}));throw new HttpError(r.status,p.detail||p.title||'Falha HTTP',p.errors||{})}return r.status===204?null:r.json();
}
export const generatedClient=new PortalClient('',{fetch:(url,init)=>fetch(url,{...init,credentials:'same-origin'})});

