const {fs,zlib,parts,roles,clamp,tau,sin,cos,dot,cross,norm,sub,mul}=require('./guard-forms.cjs');
const path=require('node:path');
const out=process.argv[2];
if(!out || path.extname(out).toLowerCase()!=='.glb')throw Error('Usage: node guard-figure.cjs <output.glb>');
const D=1024,T=128,N=D*D,color=Buffer.alloc(N*3),orm=Buffer.alloc(N*3),normal=Buffer.alloc(N*3);
const byte=n=>clamp(Math.round(n),0,255);
function noise(x,y,k){let n=(Math.imul(x,374761393)+Math.imul(y,668265263)+Math.imul(k+21,1640531513))|0;n=Math.imul(n^(n>>>13),1274126177);return ((n^(n>>>16))>>>0)/4294967295;}
function value(x,y,cell,k){
 const a=Math.floor(x/cell),b=Math.floor(y/cell),xf=x/cell-a,yf=y/cell-b,t=xf*xf*(3-2*xf),u=yf*yf*(3-2*yf);
 return ((1-t)*noise(a,b,k)+t*noise(a+1,b,k))*(1-u)+((1-t)*noise(a,b+1,k)+t*noise(a+1,b+1,k))*u;
}
const scratches=Array.from({length:16},(_,k)=>{
 const map=new Float32Array(T*T);
 if(![0,1,2,3,6,7,15].includes(k))return map;
 for(let i=0;i<(k<=3?210:74);i++){
  const x=noise(i,0,k)*T,y=noise(i,1,k)*T,ang=noise(i,2,k)*Math.PI,span=2+noise(i,3,k)**3*32;
  const depth=(.16+noise(i,4,k)*.47)*(k<=3?1:.78);
  for(let j=0;j<=Math.ceil(span*2);j++){const f=j/(Math.ceil(span*2)),xx=x+cos(ang)*f*span,yy=y+sin(ang)*f*span;
   const sx=Math.floor(xx),sy=Math.floor(yy);if(sx<1||sx>=T-2||sy<1||sy>=T-2)continue;
   for(let a=0;a<=1;a++)for(let b=0;b<=1;b++){const h=clamp(1-Math.hypot(sx+a-xx,sy+b-yy));
     const idx=(sy+b)*T+sx+a;map[idx]=Math.max(map[idx],h*depth*sin(Math.PI*f));}}
 }return map;});
for(let k=0;k<roles.length;k++){
 const [name,rgb,metal,rough]=roles[k],tilex=(k%4)*T,tiley=Math.floor(k/4)*T,heights=new Float32Array(T*T);
 for(let y=0;y<T;y++)for(let x=0;x<T;x++){
  const i=3*((tiley+y)*D+tilex+x),grain=noise(x,y,k),cloud=(value(x,y,26,k)-.5),fine=value(x,y,5,k+18)-.5;
  const scratch=scratches[k][y*T+x];let h=0,tint=1,rr=rough;
  if(k<=3){
   const stroke=(value(x*.13+y*.12,y*1.5,2,k+21)-.5);
   h=.017*stroke+.031*fine-.23*scratch;
   tint+=.095*cloud+.028*fine+.015*(grain-.5)+.033*stroke+.34*scratch;
   rr+=(.065*cloud+.034*stroke+.024*fine-.055*scratch);
  } else if([4,5,14].includes(k)){
   const weave=k===14?
    sin((x+.9*value(x,y,16,k))*2+sin(y*.018))*.5:
    ((sin(x*tau/4.3+.35*sin(y*tau/8))*.50+sin(y*tau/4.1+.6*sin(x*tau/8))*.32)*(.80+.20*sin((x+y)*tau/8)));
   h=weave*.20+.12*fine;tint+=.11*cloud+.057*weave+.026*grain;
   rr+=.05*weave;
  }else if([6,7,15].includes(k)){
   const cellGrain=Math.abs(sin(x*.057+2.7*sin(y*.061)+3*value(x,y,6,k)));
   h=.16*fine+.17*cellGrain-.29*scratch;tint+=.20*cloud+.115*fine+.085*(cellGrain-.5)+.25*scratch;
   rr+=.08*cloud+.06*fine;
  }else{
   const pores=Math.max(0,grain-.91)*8;h=(fine*.027-pores*.025);tint+=.055*cloud+.023*fine-.017*pores;
   rr+=.022*cloud;
  }
  heights[y*T+x]=h;
  for(let q=0;q<3;q++)color[i+q]=byte(rgb[q]*tint+(k===3&&q===0?cloud*8:0));
  orm[i]=255;orm[i+1]=byte(rr*255);orm[i+2]=byte(metal*255);
 }
 for(let y=0;y<T;y++)for(let x=0;x<T;x++){
  const i=3*((tiley+y)*D+tilex+x),h=(xx,yy)=>heights[clamp(yy,0,T-1)*T+clamp(xx,0,T-1)];
  const vx=(h(x-1,y)-h(x+1,y))*1.3,vy=(h(x,y+1)-h(x,y-1))*1.3,l=Math.hypot(vx,vy,1);
  normal[i]=byte(127.5*(1+vx/l));normal[i+1]=byte(127.5*(1+vy/l));normal[i+2]=byte(127.5*(1+1/l));
 }
}
require('./surface-atlas.cjs')({color,orm,normal,D,noise,value});
function png(data){const crcTable=Array.from({length:256},(_,k)=>{let n=k;for(let j=0;j<8;j++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});
 const chunk=(k,b)=>{const a=Buffer.from(k),h=Buffer.alloc(4),t=Buffer.alloc(4);h.writeUInt32BE(b.length);let c=0xffffffff;for(const x of a)c=crcTable[(c^x)&255]^(c>>>8);for(const x of b)c=crcTable[(c^x)&255]^(c>>>8);t.writeUInt32BE((c^0xffffffff)>>>0);return Buffer.concat([h,a,b,t]);};
 const hdr=Buffer.alloc(13);hdr.writeUInt32BE(D);hdr.writeUInt32BE(D,4);hdr[8]=8;hdr[9]=2;
 const stride=D*3,out=Buffer.alloc(D*(stride+1));for(let j=0;j<D;j++){const off=j*(stride+1);out[off]=1;for(let i=0;i<stride;i++)out[off+1+i]=(data[j*stride+i]-(i<3?0:data[j*stride+i-3]))&255;}
 return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(out,{level:8})),chunk('IEND',Buffer.alloc(0))]);}
const g={asset:{version:'2.0',generator:'Castle guard sculpture'},scene:0,scenes:[{name:'Guard',nodes:[0]}],nodes:[],meshes:[],materials:[{name:'Blackened steel, wool and hide',pbrMetallicRoughness:{baseColorFactor:[1,1,1,1],baseColorTexture:{index:0},metallicFactor:1,roughnessFactor:1,metallicRoughnessTexture:{index:1}},normalTexture:{index:2,scale:.52},doubleSided:false}],textures:[],images:[],samplers:[{magFilter:9729,minFilter:9987,wrapS:33071,wrapT:33071}],bufferViews:[],accessors:[],buffers:[]};
let chunks=[],bytes=0,tris=0,vs=0;const views=(buf,target)=>{let at=bytes;chunks.push(buf);bytes+=buf.length;const pad=(-bytes)&3;if(pad){chunks.push(Buffer.alloc(pad));bytes+=pad;}const view={buffer:0,byteOffset:at,byteLength:buf.length};if(target)view.target=target;g.bufferViews.push(view);return g.bufferViews.length-1;};
function accessor(array,size,type,target,bounds=false){const C=type===5126?Float32Array:type===5123?Uint16Array:Uint32Array;const arr=C.from(array);const a={bufferView:views(Buffer.from(arr.buffer),target),count:array.length/size,type:['','SCALAR','VEC2','VEC3','VEC4'][size],componentType:type};
 if(bounds){a.min=Array(size).fill(Infinity);a.max=Array(size).fill(-Infinity);array.forEach((n,i)=>{const j=i%size;a.min[j]=Math.min(a.min[j],n);a.max[j]=Math.max(a.max[j],n);});}g.accessors.push(a);return g.accessors.length-1;}
function tangents(n){
 const T=new Float64Array(n.v.length),B=new Float64Array(n.v.length),out=[];
 for(let j=0;j<n.ix.length;j+=3){
  const [a,b,c]=n.ix.slice(j,j+3),eu=n.uv[2*b]-n.uv[2*a],ev=n.uv[2*b+1]-n.uv[2*a+1],fu=n.uv[2*c]-n.uv[2*a],fv=n.uv[2*c+1]-n.uv[2*a+1],det=eu*fv-fu*ev;
  if(Math.abs(det)<1e-12)continue;
  for(let k=0;k<3;k++){const d=n.v[3*b+k]-n.v[3*a+k],e=n.v[3*c+k]-n.v[3*a+k],t=(d*fv-e*ev)/det,bt=(e*eu-d*fu)/det;
   for(const i of [a,b,c]){T[3*i+k]+=t;B[3*i+k]+=bt;}}
 }
 for(let i=0;i<T.length;i+=3){const N=n.n.slice(i,i+3),avg=Array.from(T.subarray(i,i+3));let v=sub(avg,mul(N,dot(N,avg)));
  if(Math.hypot(...v)<1e-7)v=cross(N,Math.abs(N[1])<.8?[0,1,0]:[1,0,0]);v=norm(v);
  out.push(...v,dot(cross(N,v),B.subarray(i,i+3))<0?-1:1);
 }
 return out;
}
for(const n of parts){const node={name:n.name,translation:n.parent?n.origin.map((v,i)=>v-n.parent.origin[i]):n.origin};
 if(n.name==='Chest')node.rotation=[0,-.015,.013,Math.sqrt(1-.015**2-.013**2)];
 if(n.name==='Head')node.rotation=[0,.055,-.018,Math.sqrt(1-.055**2-.018**2)];
 const children=parts.filter(m=>m.parent===n).map(m=>parts.indexOf(m));if(children.length)node.children=children;
 if(n.ix.length){if(n.v.some(x=>!Number.isFinite(x))||n.n.some(x=>!Number.isFinite(x)))throw Error(n.name+' nonfinite');
 const prim={attributes:{POSITION:accessor(n.v,3,5126,34962,true),NORMAL:accessor(n.n,3,5126,34962),TEXCOORD_0:accessor(n.uv,2,5126,34962),TANGENT:accessor(tangents(n),4,5126,34962)},indices:accessor(n.ix,1,n.v.length/3<65536?5123:5125,34963),material:0};
 node.mesh=g.meshes.length;g.meshes.push({name:n.name+' geometry',primitives:[prim]});tris+=n.ix.length/3;vs+=n.v.length/3;}
 g.nodes.push(node);}
for(const [i,source] of [color,orm,normal].entries()){g.images.push({name:['Guard surface color','Guard roughness and metal','Guard surface relief'][i],mimeType:'image/png',bufferView:views(png(source))});g.textures.push({sampler:0,source:i});}
g.buffers.push({byteLength:bytes});const json=Buffer.from(JSON.stringify(g)),pad=(-json.length)&3,binary=Buffer.concat(chunks),hdr=Buffer.alloc(20),bh=Buffer.alloc(8);
hdr.write('glTF');hdr.writeUInt32LE(2,4);hdr.writeUInt32LE(28+json.length+pad+binary.length,8);hdr.writeUInt32LE(json.length+pad,12);hdr.writeUInt32LE(0x4e4f534a,16);bh.writeUInt32LE(binary.length);bh.writeUInt32LE(0x004e4942,4);
const glb=Buffer.concat([hdr,json,Buffer.alloc(pad,32),bh,binary]);
const stats={size:glb.length,triangles:tris,vertices:vs,meshes:g.meshes.length,materials:g.materials.length,dimensions:[]};
for(let k=0;k<3;k++){let mi=Infinity,ma=-Infinity;for(const p of parts){for(let j=k;j<p.v.length;j+=3){const v=p.v[j]+p.origin[k];mi=Math.min(mi,v);ma=Math.max(ma,v);}}stats.dimensions.push([mi,ma]);}
fs.writeFileSync(out,glb);
console.log(JSON.stringify(stats));
