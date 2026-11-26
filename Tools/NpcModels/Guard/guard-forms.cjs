const fs = require('fs');
const zlib = require('zlib');
const tau = 2*Math.PI, sin = Math.sin, cos = Math.cos, clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
const add=(a,b)=>a.map((x,i)=>x+b[i]), sub=(a,b)=>a.map((x,i)=>x-b[i]), mul=(a,s)=>a.map(x=>x*s);
const dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const norm=a=>mul(a,1/(Math.hypot(...a)||1)),mix=(a,b,t)=>a*(1-t)+b*t;
const roles = [
 ['iron',[46,58,70],.76,.47],['steel',[80,94,108],.81,.44],['edge',[137,148,152],.78,.37],
 ['gold',[204,158,80],.76,.39],['cloak',[39,42,51],0,.92],['fold',[53,56,64],0,.87],
 ['leather',[67,48,40],0,.68],['welt',[116,82,54],.06,.58],
 ['skin',[183,135,106],0,.65],['cheek',[144,98,77],0,.70],['lip',[154,96,81],0,.61],
 ['iris',[91,72,48],.02,.35],['eye',[208,201,185],0,.33],['shade',[36,32,33],0,.77],
 ['plume',[43,50,58],0,.68],['oxhide',[67,47,41],.06,.70]];
const tag=Object.fromEntries(roles.map((r,i)=>[r[0],i]));
const atlasSize=1024;
function mapUV(mat,uv,p) {
 const tile=tag[mat];
 if (tile!==undefined) return [((tile%4)*128+4+120*clamp(uv[0]))/atlasSize,
   (Math.floor(tile/4)*128+4+120*clamp(uv[1]))/atlasSize];
 if(mat==='portrait')return [(518+498*clamp(.5+(p[0]-.004+.015*(p[2]-.10))/.238))/atlasSize,
   (8+612*clamp((1.813-p[1]-.018*(p[2]-.10))/.359))/atlasSize];
 if(mat==='escutcheon')return [(9+486*clamp((p[0]-.219)/.336))/atlasSize,
   (526+486*clamp((1.283-p[1])/.522))/atlasSize];
 if(mat==='crest')return [(523+486*clamp(uv[0]))/atlasSize,(644+170*clamp(uv[1]))/atlasSize];
 if(mat==='irisPaint')return [(528+222*clamp(uv[0]))/atlasSize,(854+155*clamp(uv[1]))/atlasSize];
 throw Error('Missing atlas coordinates for '+mat);
}
let parts=[],part;
function node(name,origin,parent){part={name,origin,parent,v:[],n:[],uv:[],ix:[],cache:new Map()};parts.push(part);return part;}
const root=node('Figure',[0,0,0],null);
const legs=node('Legs',[0,0,0],root);
const chest=node('Chest',[0,.98,0],root);
const head=node('Head',[.004,1.47,-.006],chest);
const jaw=node('Jaw',[.004,1.557,.045],head);
const left=node('GestureArm',[-.205,1.358,-.008],chest);
const right=node('OffArm',[.212,1.338,-.013],chest);
function use(n){part=n;}
function tri(mat,a,b,c,ua,ub,uc,na,nb,nc){
 const cr=cross(sub(b,a),sub(c,a));if(Math.hypot(...cr)<1e-11)return;
 const normal=norm(cr);
 const shading=n=>n&&dot(n,n)>.90?n:normal;
 for(const [p,n,uv] of [[a,shading(na),ua],[b,shading(nb),ub],[c,shading(nc),uc]]){
  const mapped=mapUV(mat,uv,p);
  const raw=[...sub(p,part.origin),...n,...mapped].map(x=>Math.round(x*1e6)/1e6);
  const key=raw.join(':'),old=part.cache.get(key);if(old!==undefined){part.ix.push(old);continue;}
  const id=part.v.length/3;part.cache.set(key,id);part.ix.push(id);part.v.push(...raw.slice(0,3));part.n.push(...raw.slice(3,6));part.uv.push(...raw.slice(6));
 }
}
function grid(mat,nu,nv,at,{closed=false,reverse=false}={}){
 const pts=[],ns=[];for(let j=0;j<=nv;j++){pts[j]=[];ns[j]=[];for(let i=0;i<=nu;i++)pts[j].push(at(i/nu,j/nv));}
 for(let j=0;j<=nv;j++)for(let i=0;i<=nu;i++){
  const lo=closed?(i===0||i===nu?nu-1:i-1):Math.max(0,i-1),hi=closed?(i===nu||i===0?1:i+1):Math.min(nu,i+1);
  ns[j][i]=mul(norm(cross(sub(pts[j][hi],pts[j][lo]),sub(pts[Math.min(nv,j+1)][i],pts[Math.max(0,j-1)][i]))),reverse?-1:1);
 }
 for(let j=0;j<nv;j++)for(let i=0;i<nu;i++){
  const emit=(a,b,c)=>{if(reverse)[b,c]=[c,b];const list=[a,b,c];tri(mat,...list.map(([u,v])=>pts[v][u]),...list.map(([u,v])=>[u/nu,1-v/nv]),...list.map(([u,v])=>ns[v][u]));};
  emit([i,j],[i+1,j],[i+1,j+1]);emit([i,j],[i+1,j+1],[i,j+1]);
 }
}
function loft(mat,rows,N=28,deform=null){
 const point=(u,v)=>{const h=v*(rows.length-1),i=Math.min(rows.length-2,Math.floor(h)),q=h-i;
  const [x,y,z,a,b]=rows[i].map((w,k)=>mix(w,rows[i+1][k],q));const t=u*tau;
  let p=[x+a*sin(t),y,z+b*cos(t)];return deform?deform(p,t,v):p;};
 // On full rings, keep the UV discontinuity behind the character. The points passed
 // to authored detail curves and the original silhouette remain in model coordinates.
 grid(mat,N,rows.length-1,(u,v)=>point((u+.5)%1,v),{closed:true});for(const [j,down] of [[0,true],[rows.length-1,false]]){
  const row=rows[j],center=[row[0],row[1],row[2]],v=down?0:1;
  for(let i=0;i<N;i++){const u=i/N,w=(i+1)/N,A=point(u,v),B=point(w,v);const U=t=>[.5+.48*sin(t*tau),.5+.48*cos(t*tau)];if(down)tri(mat,center,B,A,[.5,.5],U(w),U(u));else tri(mat,center,A,B,[.5,.5],U(u),U(w));}
 }
}
function egg(m,x,y,z,rx,ry,rz,nu=20,nv=12){
 const rows=Array.from({length:nv+1},(_,i)=>{const h=Math.PI*(i/nv-.5);return[x,y+ry*sin(h),z,rx*Math.max(.0005,cos(h)),rz*Math.max(.0005,cos(h))]});loft(m,rows,nu);
}
function sweep(m,rows,N=12){
 const rings=rows.map((r,i)=>{const p=r.slice(0,3),axis=norm(sub(rows[Math.min(rows.length-1,i+1)].slice(0,3),rows[Math.max(0,i-1)].slice(0,3)));
  const ref=Math.abs(axis[0])<.79?[1,0,0]:[0,0,1],X=norm(sub(ref,mul(axis,dot(axis,ref)))),Z=norm(cross(X,axis));return {p,X,Z,a:r[3],b:r[4]??r[3]};});
 function at(u,j){const r=rings[j];return add(r.p,add(mul(r.X,sin(tau*u)*r.a),mul(r.Z,cos(tau*u)*r.b)));}
 grid(m,N,rows.length-1,(u,v)=>at(u,Math.round(v*(rows.length-1))),{closed:true});
 for(const j of [0,rows.length-1])for(let i=0;i<N;i++){
  const a=at(i/N,j),b=at((i+1)/N,j),p=rings[j].p,U=k=>[.5+.48*sin(tau*k/N),.5+.48*cos(tau*k/N)];
  j?tri(m,p,a,b,[.5,.5],U(i),U(i+1)):tri(m,p,b,a,[.5,.5],U(i+1),U(i));
 }
}
const path=(m,pts,r=.003,n=8)=>sweep(m,pts.map(p=>[...p,r]),n);
function faceplate(m,poly,z,th=.006,bulge=0){
 const c=[0,1].map(k=>poly.reduce((s,p)=>s+p[k],0)/poly.length), xs=poly.map(p=>p[0]),ys=poly.map(p=>p[1]),min=[Math.min(...xs),Math.min(...ys)],max=[Math.max(...xs),Math.max(...ys)];
 const uv=p=>[clamp((p[0]-min[0])/(max[0]-min[0]||1)),1-clamp((p[1]-min[1])/(max[1]-min[1]||1))];
 const depth=p=>typeof z==='function'?z(p[0],p[1]):z;
 let pts=poly.map(p=>[p[0],p[1],depth(p)]);const area=pts.reduce((a,p,i)=>{const q=pts[(i+1)%pts.length];return a+p[0]*q[1]-p[1]*q[0]},0);if(area<0)pts.reverse();
 const mid=[...c,depth(c)+bulge],d=p=>[p[0],p[1],p[2]-th];for(let i=0;i<pts.length;i++){const a=pts[i],b=pts[(i+1)%pts.length];tri(m,mid,a,b,uv(mid),uv(a),uv(b));tri(m,d(mid),d(b),d(a),uv(mid),uv(b),uv(a));tri(m,a,d(a),d(b),[0,0],[0,1],[1,1]);tri(m,a,d(b),b,[0,0],[1,1],[1,0]);}
}
function pin(m,x,y,z,rx,ry=rx){egg(m,x,y,z,rx,ry,rx*.34,12,6)}
function disc(m,x,y,z,r,th=.005){faceplate(m,Array.from({length:28},(_,i)=>[x+r*cos(tau*i/28),y+r*sin(tau*i/28)]),z,th,.001);}
function band(m,x,z,y,rx,rz,h){loft(m,[[x,y-h/2,z,rx,rz],[x,y+h/2,z,rx,rz]],28);}
module.exports={fs,zlib,tau,sin,cos,clamp,add,sub,mul,dot,cross,norm,mix,tag,roles,parts,root,legs,chest,head,jaw,left,right,use,tri,grid,loft,egg,sweep,path,faceplate,pin,disc,band};
