// Original articulated mounted knight. Measurements in metres.
// Build with Node.js 18+ : node eventide-rider.cjs --output ./Knight.glb
// No textures, libraries, or downloaded source assets are required.
const fs=require('node:fs'),zip=require('node:zlib'),path=require('node:path');
const args=process.argv.slice(2);
if(args.length && !(args.length===2 && args[0]==='--output') && !(args.length===1 && !args[0].startsWith('-')))
 throw Error('Usage: node eventide-rider.cjs [--output] [Knight.glb]');
const outputPath=path.resolve(args.at(-1)||'Knight.glb');
const TAU=Math.PI*2, add=(a,b)=>a.map((v,i)=>v+b[i]),sub=(a,b)=>a.map((v,i)=>v-b[i]),mul=(a,s)=>a.map(v=>v*s),dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],len=a=>Math.hypot(...a),unit=a=>mul(a,1/(len(a)||1)),mix=(a,b,t)=>add(mul(a,1-t),mul(b,t)),clamp=(v,l=0,h=1)=>Math.min(h,Math.max(l,v)),sin=Math.sin,cos=Math.cos,PI=Math.PI;
// Atlas swatches specify sRGB pigment, surface texture, metallicity and roughness.
const tiles = {
  Pearl: ['#564547', 'coat', 0, .73],
  ShadowCoat: ['#44383c', 'coat', 0, .77],
  Muzzle: ['#3d383d', 'hide', 0, .76],
  Graphite: ['#24232b', 'hair', 0, .63],
  Ash: ['#433b42', 'hair', 0, .60],
  SilkMane: ['#635054', 'hair', 0, .55],
  Hoof: ['#504d45', 'horn', 0, .56],
  Nostril: ['#19191f', 'hide', 0, .82],
  InnerEar: ['#886b6c', 'skin', 0, .83],
  EyeDark: ['#302b2a', 'shine', 0, .25],
  Amber: ['#8d623a', 'shine', 0, .23],
  Ivory: ['#e8dfca', 'brocade', 0, .86],
  Gold: ['#c8a560', 'metal', .68, .38],
  Antique: ['#946d43', 'metal', .65, .45],
  Silver: ['#b3c1ca', 'metal', .73, .37],
  Blade: ['#758b9d', 'metal', .72, .43],
  Etch: ['#4d6374', 'metal', .6, .42],
  Midnight: ['#263859', 'cloth', 0, .79],
  Royal: ['#354c73', 'brocade', 0, .81],
  Lapis: ['#4e6784', 'cloth', 0, .75],
  Lining: ['#a499b4', 'cloth', 0, .73],
  Mahogany: ['#53372d', 'hide', 0, .62],
  Russet: ['#875e43', 'hide', 0, .66],
  Cream: ['#f4dfb1', 'cloth', 0, .78],
  Skin: ['#eac5b0', 'skin', 0, .68],
  Blush: ['#c68e7d', 'skin', 0, .72],
  Lip: ['#ad6865', 'skin', 0, .55],
  Iris: ['#42615b', 'shine', 0, .29],
  IrisLight: ['#65877b', 'shine', 0, .32],
  Lash: ['#4c352f', 'hair', 0, .76],
  Sclera: ['#e4dcd0', 'shine', 0, .46],
  Glint: ['#fff8db', 'shine', 0, .18],
  Butter: ['#c9a273', 'hair', 0, .53],
  GoldHair: ['#a77e54', 'hair', 0, .63],
  BlondLight: ['#e3c296', 'hair', 0, .53],
  Chain: ['#687782', 'mail', .49, .59],
  Gem: ['#508a9c', 'shine', .22, .25],
  Charcoal: ['#43464e', 'cloth', 0, .81],
  Honey: ['#b79366', 'hair', 0, .58],
};
const keys=Object.keys(tiles),TS=112,cols=8,rows=5,tw=cols*TS,th=rows*TS;
if (keys.length > cols * rows) throw Error("Texture atlas has too few tiles.");
function rand(i,j){let n=Math.imul(i+1,374761393)+Math.imul(j+1,668265263);n=Math.imul(n^(n>>>13),1274126177);return ((n^(n>>>16))>>>0)/4294967295;}
function tex(t,u,v){const [hex,kind,metal,rough]=tiles[t],c=[1,3,5].map(x=>parseInt(hex.substring(x,x+2),16));let sh=1,shine=0,noise=rand(Math.floor(u*TS),Math.floor(v*TS))-.5;const a=TAU*u,b=TAU*v;
 if(kind==='coat'){sh+=.035*sin(3*a+sin(b*4))+.027*sin(b*7+cos(2*a))+.022*noise;const dots=sin(3*a+.7*sin(3*b))*cos(4*b+.4*cos(2*a));sh-=.05*Math.max(0,dots)}
 else if(kind==='metal'){sh+=.018*sin(b*40+sin(a)*4)+.008*noise+.036*sin(a+.25*b);shine=.03*noise}
 else if(kind==='hair'||kind==='horn'){sh+=.045*sin(15*a+2*sin(b))+.028*sin(8*a+.9*sin(2*b))+.021*noise}
 else if(kind==='cloth'||kind==='brocade'){sh+=.018*sin(31*a)*cos(31*b)+.017*noise;if(kind==='brocade'){const x=u*4,y=v*4,rx=(x%1)-.5,ry=(y%1)-.5;const fern=Math.abs(Math.abs(rx)-.15*(1+cos(ry*TAU*1.5)));if(fern<.037)sh+=.10;const diamond=Math.abs(Math.abs(rx)+Math.abs(ry)-.43);if(diamond<.021)sh+=.08}}
 else if(kind==='hide'){sh+=.035*noise+.023*sin(8*a+2*cos(7*b))}
 else if(kind==='skin'){sh+=.013*noise+.013*cos(a)*cos(b)}
 else if(kind==='mail'){const x=(u*16+(Math.floor(v*19)%2)*.5)%1-.5,y=(v*19)%1-.5;sh+=.22*cos((Math.hypot(x,y*.8)-.26)*7)}
 return [c.map(q=>clamp(Math.round(q*sh),0,255)),[255,clamp(Math.round((rough+shine)*255),0,255),Math.round(metal*255)]];
}
function png(w,h,rgb){const raw=Buffer.alloc(h*(w*3+1));for(let y=0;y<h;y++)rgb.copy(raw,y*(w*3+1)+1,y*w*3,(y+1)*w*3);function chunk(type,b){const t=Buffer.from(type),d=Buffer.alloc(4),post=Buffer.alloc(4);d.writeUInt32BE(b.length);let crc=-1;for(const ch of Buffer.concat([t,b])){crc^=ch;for(let j=0;j<8;j++)crc=(crc>>>1)^((crc&1)?0xedb88320:0)}post.writeUInt32BE((crc^-1)>>>0);return Buffer.concat([d,t,b,post])}const ih=Buffer.alloc(13);ih.writeUInt32BE(w);ih.writeUInt32BE(h,4);ih[8]=8;ih[9]=2;return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',ih),chunk('IDAT',zip.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))])}
const albedo=Buffer.alloc(tw*th*3),pbr=Buffer.alloc(tw*th*3);keys.forEach((k,idx)=>{for(let y=0;y<TS;y++)for(let x=0;x<TS;x++){const [c,m]=tex(k,clamp((x-3)/(TS-7)),clamp((y-3)/(TS-7))),i=3*((Math.floor(idx/cols)*TS+y)*tw+(idx%cols)*TS+x);for(let q=0;q<3;q++){albedo[i+q]=c[q];pbr[i+q]=m[q]}}});
const frames={horse:-50,body:-16,face:5};const rot=(p,a)=>[p[0]*cos(a)+p[2]*sin(a),p[1],-p[0]*sin(a)+p[2]*cos(a)];
function world(p,basis){const a=frames[basis]*PI/180;return add(rot(sub(p,[0,0,-.08]),a),[0,0,-.08])}
let parts=[],cur=null;function make(n,org,basis,parent){const part={name:n,org:world(org,basis),basis,parent,verts:[],norms:[],uv:[],inds:[],lookup:new Map(),triMat:[]};parts.push(part);cur=part;return part}
function use(n){cur=parts.find(q=>q.name===n)}
function uv(t,u,v){const i=keys.indexOf(t);if(i<0)throw Error(t);return [((i%cols)*TS+3.5+clamp(u)*(TS-7))/tw,(Math.floor(i/cols)*TS+3.5+clamp(v)*(TS-7))/th]}
function face(t,p,n,st){const out=[];for(let j=0;j<3;j++){const pos=sub(world(p[j],cur.basis),cur.org),no=rot(n[j],frames[cur.basis]*PI/180),tc=uv(t,...st[j]);const tag=[...pos,...no,...tc].map(v=>Math.round(v*1e5)).join(',');let ix=cur.lookup.get(tag);if(ix===undefined){ix=cur.verts.length/3;cur.lookup.set(tag,ix);cur.verts.push(...pos);cur.norms.push(...no);cur.uv.push(...tc)}out.push(ix)}if(new Set(out).size===3){cur.inds.push(...out);cur.triMat.push(t)}}
function patch(t,grid,orient,wrap=false,smooth=true){const h=grid.length,w=grid[0].length,ns=grid.map(row=>row.map(()=>[0,0,0])),faces=[];
 for(let y=0;y<h-1;y++)for(let x=0;x<w-1;x++){for(let v of [[[y,x],[y,x+1],[y+1,x+1]],[[y,x],[y+1,x+1],[y+1,x]]]){
  let points=v.map(([i,j])=>grid[i][j]);let nn=cross(sub(points[1],points[0]),sub(points[2],points[0]));if(len(nn)<1e-10)continue;
  const want=typeof orient==='function'?orient(y,x,points[0]):orient;if(dot(nn,want)<0){v=[v[0],v[2],v[1]];nn=mul(nn,-1)}for(const [i,j] of v)ns[i][j]=add(ns[i][j],nn);faces.push([v,unit(nn)])}}
 if(wrap)for(let i=0;i<h;i++){const k=add(ns[i][0],ns[i][w-1]);ns[i][0]=k;ns[i][w-1]=k}
 for(const [v,flat] of faces){face(t,v.map(([i,j])=>grid[i][j]),v.map(([i,j])=>smooth?unit(ns[i][j]):flat),v.map(([i,j])=>[j/(w-1),i/(h-1)]))}
}
function loft(t,sections,axis='y',N=22,warp=null){const rr=sections.map(([x,y,z,rx,rr],i)=>Array.from({length:N+1},(_,j)=>{const a=TAU*j/N;let p=axis==='y'?[x+rx*sin(a),y,z+rr*cos(a)]:[x+rx*sin(a),y+rr*cos(a),z];return warp?warp(p,a,i):p}));patch(t,rr,(i,j,p)=>{const mid=mix(sections[i].slice(0,3),sections[i+1].slice(0,3),.5);const d=sub(p,mid);d[axis==='y'?1:2]=0;return d},true);for(let s of [0,sections.length-1]){const center=sections[s].slice(0,3),d=[0,0,0];d[axis==='y'?1:2]=(s?1:-1)*Math.sign(sections.at(-1)[axis==='y'?1:2]-sections[0][axis==='y'?1:2]||1);for(let i=0;i<N;i++){let p=[center,rr[s][i],rr[s][i+1]];if(dot(cross(sub(p[1],p[0]),sub(p[2],p[0])),d)<0)p.reverse();face(t,p,[d,d,d],[[.5,.5],[.0,.0],[1,1]])}}}
function ell(t,c,sz,N=16,H=9){
 const radius=Math.max(...sz);
 if(radius<=.017){N=Math.min(N,9);H=Math.min(H,4);}
 else if(radius<.042){N=Math.min(N,14);H=Math.min(H,7);}
 loft(t,Array.from({length:H+1},(_,i)=>{const a=-PI/2+PI*i/H;return [c[0],c[1]+sz[1]*sin(a),c[2],Math.max(.0001,sz[0]*cos(a)),Math.max(.0001,sz[2]*cos(a))]}),'y',N);
}
function sweep(t,pts,rr,N=9,ref=[1,0,0]){const grid=pts.map((p,i)=>{let tan=unit(sub(pts[Math.min(i+1,pts.length-1)],pts[Math.max(0,i-1)]));let r=ref;if(Math.abs(dot(ref,tan))>.98)r=[0,1,0];const v=unit(cross(tan,r)),u=unit(cross(v,tan));const rad=Array.isArray(rr[i])?rr[i]:[rr[i],rr[i]];return Array.from({length:N+1},(_,j)=>add(p,add(mul(u,rad[0]*cos(TAU*j/N)),mul(v,rad[1]*sin(TAU*j/N)))))});
 patch(t,grid,(i,j,p)=>sub(p,pts[i]),true);for(const s of [0,pts.length-1]){const n=unit(sub(pts[s],pts[s===0?1:s-1]));for(let j=0;j<N;j++){let p=[pts[s],grid[s][j],grid[s][j+1]];if(dot(cross(sub(p[1],p[0]),sub(p[2],p[0])),n)<0)p.reverse();face(t,p,[n,n,n],[[.5,.5],[0,0],[1,1]])}}}
function cord(t,pts,r=.008){sweep(t,pts,pts.map(()=>r),r<=.0045?5:7)}
function strap(t,pts,width=.025){sweep(t,pts,pts.map(()=>[width,.004]),8)}
function panel(t,rows,orient=[0,0,1]){patch(t,rows,orient)}
function disk(t,center,u,v,n=22){let normal=unit(cross(u,v));for(let i=0;i<n;i++){const a=i*TAU/n,b=(i+1)*TAU/n;const pts=[center,add(center,add(mul(u,cos(a)),mul(v,sin(a)))),add(center,add(mul(u,cos(b)),mul(v,sin(b))))];face(t,pts,[normal,normal,normal],[[.5,.5],[.5+.47*cos(a),.5+.47*sin(a)],[.5+.47*cos(b),.5+.47*sin(b)]])}}
function hoop(t,c,u,v,r=.006){cord(t,Array.from({length:21},(_,i)=>add(c,add(mul(u,cos(TAU*i/20)),mul(v,sin(TAU*i/20))))),r)}
// Interpolate the silhouette without sharp changes at each sculpted section.
function smoothPath(points, subdivisions=3){
 const result=[];
 for(let k=0;k<points.length-1;k++){
  const a=points[Math.max(0,k-1)],b=points[k],c=points[k+1],d=points[Math.min(k+2,points.length-1)];
  for(let j=0;j<subdivisions;j++){
   const t=j/subdivisions;
   result.push(b.map((v,i)=>.5*(2*v+(-a[i]+c[i])*t+
    (2*a[i]-5*v+4*c[i]-d[i])*t*t+(-a[i]+3*v-3*c[i]+d[i])*t*t*t)));
  }
 }
 return [...result,points.at(-1)];
}
function taperedLine(tile,points,radius){
 const line=points.length<7?smoothPath(points,2):points;
 sweep(tile,line,line.map((_,i)=>radius*(.13+.87*Math.max(0,sin(PI*(i/(line.length-1))))**.55)),5);
}
const root=make('Figure',[0,0,0],'horse',null),horse=make('HorseChest',[0,1.12,.22],'horse',root),neck=make('HorseNeck',[0,1.31,.48],'horse',horse);
make('EarL',[-.093,1.973,.772],'horse',neck);make('EarR',[.093,1.973,.772],'horse',neck);
const chest=make('Chest',[0,1.63,-.08],'body',root),head=make('Head',[0,2.112,-.08],'face',chest);make('Jaw',[0,2.182,.024],'face',head);make('GestureArm',[-.17,2.068,-.062],'body',chest);
make('HorseRump',[0,1.19,-.63],'horse',horse);make('HorseWithers',[0,1.44,.41],'horse',horse);make('HorseMuzzle',[0,1.57,1.126],'horse',neck);
for(const s of [-1,1])for(const end of ['Fore','Hind']){const id=end+(s<0?'L':'R'),z=end==='Fore'?.43:-.57;const leg=make(id,[s*.216,1.17,z],'horse',horse);make(id+'Cannon',[s*.233,.51,z-.02],'horse',leg)}
make('HorseTail',[0,1.326,-.834],'horse',horse);
use('HorseChest');
// Barrel is a continuous sectional surface: croup, loins, ribs and brisket, with a visible tuck at the flanks.
const barrel=[[0,1.165,-.905,.080,.162],[0,1.200,-.83,.228,.276],
 [0,1.209,-.70,.297,.295],[0,1.197,-.52,.297,.294],[0,1.174,-.32,.268,.287],
 [0,1.131,-.10,.273,.329],[0,1.115,.12,.309,.364],[0,1.145,.296,.301,.395],
 [0,1.173,.435,.257,.381],[0,1.17,.545,.204,.304],[0,1.181,.61,.12,.194],[0,1.192,.634,.035,.108]];
loft('Pearl',smoothPath(barrel,2),'z',32,(p,a)=>{
 const haunch=Math.exp(-((p[2]+.65)**2)/.069);
 // The near hip drops slightly over the resting hind leg; the saddle bed stays level.
 return [p[0],p[1]+.022*haunch*sin(a)*Math.max(0,cos(a)),p[2]];
});
for(const s of [-1,1]){sweep('Pearl',[[s*.215,1.40,-.70],[s*.259,1.27,-.637],[s*.245,1.10,-.605],[s*.218,.92,-.51]],[[.029,.051],[.069,.105],[.07,.094],[.045,.052]],15);
 // The scapula and deep pectoral lie in the surface, not freestanding spheres.
 sweep('Pearl',[[s*.145,1.475,.236],[s*.234,1.372,.337],[s*.245,1.216,.412],[s*.214,1.026,.387]],[[.033,.036],[.068,.082],[.076,.105],[.044,.062]],16);
}
for(const s of [-1,1])for(const front of [true,false]){const id=(front?'Fore':'Hind')+(s<0?'L':'R'),x=s*(front?.198:.21),step=front?(s<0?.066:-.092):(s<0?.108:.038),z=front?.416:-.616;
 const resting=!front&&s<0;
 use(id);const path=front?[[x,1.287,z],[x+.013*s,1.155,z+.049],[x+.019*s,.987,z+.032],[x+.025*s,.882,z-.018],[x+.032*s,.735,z+step*.35],[x+.031*s,.57,z+.014+step],[x+.027*s,.51,z+.02+step]]:
 [[x,1.302,z],[x+.013*s,1.142,z-.006],[x+.021*s,1.00,z+(resting?.110:.069)],
 [x+.019*s,.899,z+(resting?.188:.118)],[x+.025*s,.79,z+(resting?.112:.046)],
 [x+.027*s,.657,z+(resting?-.004:-.061)],[x+.026*s,.555,z-.126+step],[x+.026*s,.511,z-.12+step]];
 const radii=front?[[.111,.111],[.112,.134],[.085,.113],[.066,.087],[.059,.070],[.053,.058],[.054,.066]]:[[.122,.13],[.122,.156],[.095,.145],[.074,.102],[.070,.088],[.055,.071],[.046,.071],[.044,.056]];
 const limb=smoothPath(path,2),limbRadii=smoothPath(radii,2);
 sweep('Pearl',limb,limbRadii,22);
 // Tendon of the forearm and the broad biceps femoris fan blend into the articulated limb.
 const profile=front?
  [[.068,1.18,.042],[.073,1.09,.070],[.066,.994,.071],[.049,.909,.034],[.041,.823,.010]]:
  [[.086,1.25,-.038],[.103,1.151,-.037],[.088,1.050,.038],[.058,.957,.105],[.043,.901,.123]];
 const muscle=profile.map(([dx,yy,dz])=>[x+s*dx,yy,z+dz+(resting?.046*clamp((1.20-yy)/.30):0)]);
 sweep('Pearl',smoothPath(muscle,2),smoothPath([[.028,.024],[.045,.050],[.042,.056],[.023,.036],[.004,.011]],2),12);
 use(id+'Cannon');const start=path.at(-1),xx=start[0],lift=resting?.018:0;
 const zz=start[2]+(resting?.062:0);
 const below=[mix(start,path.at(-2),.53),start,[xx,.471,start[2]+.006],[xx,.408,start[2]+(resting?.026:.015)],
  [xx,.27,zz+.018],[xx,.205+lift,zz+.013],[xx,.172+lift,zz+.016],[xx,.122+lift,zz+.045],[xx,.089+lift,zz+.057]];
 sweep(front&&s<0?'Pearl':'ShadowCoat',smoothPath(below,2),smoothPath([[.041,.050],[.053,.060],[.047,.058],
  [.035,.046],[.032,.038],[.049,.056],[.046,.060],[.042,.048],[.035,.040]],2),17);
 // Delicate tendon line, corona and layered, flattened hoof with toe facing forward.
 sweep('Muzzle',[[xx+.034*s,.441,zz-.021],[xx+.030*s,.357,zz-.014],[xx+.029*s,.248,zz-.005],[xx+.040*s,.194+lift,zz-.010]],[[.004,.006],[.003,.006],[.004,.005],[.005,.007]],7);
 const hoofSlope=(p,a)=>[p[0],p[1]-(resting?.018*cos(a):0),p[2]];
 loft('Hoof',[[xx,.002+lift,zz+.064,.066,.090],[xx,.014+lift,zz+.067,.071,.095],[xx,.045+lift,zz+.069,.067,.087],[xx,.098+lift,zz+.052,.056,.065],[xx,.123+lift,zz+.044,.045,.054]],'y',24,hoofSlope);
 loft('ShadowCoat',[[xx,.095+lift,zz+.05,.057,.066],[xx,.106+lift,zz+.049,.057,.064],[xx,.122+lift,zz+.045,.045,.055]],'y',19,hoofSlope);
 for(let k=0;k<5;k++){
  const theta=PI*(.61+k*.18),dx=sin(theta),dz=cos(theta);
  const feather=[[xx+.041*dx,.194+lift,zz+.013+.050*dz],
    [xx+.044*dx,.166+lift,zz+.020+.053*dz],
    [xx+.032*dx,.123+lift+.008*cos(k),zz+.040+.049*dz]];
  sweep('ShadowCoat',smoothPath(feather,2),[[.003,.004],[.006,.005],[.008,.006],[.005,.004],[.0005,.0006]],7);
 }
}
use('HorseNeck');
// Curving cervix tapers upward to poll; cross-sections lie across the sweep instead of being stacked balls.
sweep('Pearl',smoothPath([[0,1.22,.415],[0,1.353,.500],[0,1.506,.556],[0,1.642,.606],[0,1.770,.674],[0,1.870,.737],[0,1.941,.775]],3),
 smoothPath([[.231,.296],[.216,.279],[.176,.240],[.151,.203],[.126,.165],[.106,.123],[.080,.081]],3),26);
const skull=[[0,1.898,.748],[0,1.903,.805],[0,1.874,.876],[0,1.805,.938],[0,1.735,1.004],[0,1.64,1.084],[0,1.585,1.128],[0,1.559,1.14]];
sweep('Pearl',skull,[[.092,.12],[.133,.145],[.14,.154],[.121,.131],[.102,.104],[.08,.074],[.087,.079],[.072,.058]],28);
sweep('Muzzle',[[0,1.608,1.116],[0,1.577,1.145],[0,1.549,1.153],[0,1.529,1.142]],[[.082,.063],[.094,.073],[.090,.069],[.059,.043]],22);
for(const s of [-1,1]){
 const cheek=[s*.096,1.80,.894];
 ell('Pearl',cheek,[.052,.103,.091],18,11);
 ell('EyeDark',[s*.125,1.855,.914],[.025,.025,.044],18,9);
 ell('Amber',[s*.149,1.856,.922],[.004,.016,.021],14,9);
 ell('EyeDark',[s*.153,1.853,.926],[.0028,.008,.015],14,7);
 ell('Glint',[s*.1555,1.863,.93],[.0019,.0035,.004],10,6);
 cord('Muzzle',[[s*.133,1.851,.958],[s*.149,1.869,.941],[s*.151,1.879,.914],[s*.139,1.869,.885],[s*.131,1.862,.876]],.0048);
 ell('Nostril',[s*.082,1.579,1.172],[.017,.021,.025],14,8);
 cord('ShadowCoat',[[s*.080,1.598,1.147],[s*.094,1.599,1.166],[s*.099,1.584,1.180],[s*.092,1.567,1.181]],.004);
 cord('Nostril',[[s*.024,1.522,1.177],[s*.060,1.53,1.18],[s*.085,1.541,1.143],[s*.091,1.558,1.112]],.0035);
 use(s<0?'EarL':'EarR');const e=s<0?-.017:.006;
 sweep('Pearl',[[s*.086,1.959,.774],[s*.096,2.005,.767],[s*.117,2.062,.774+e],[s*.13,2.116,.785+e],[s*.133,2.134,.789+e]],[[.032,.029],[.043,.029],[.037,.023],[.019,.014],[.002,.003]],17);
 panel('InnerEar',[[[s*.084,2.006,.793],[s*.094,1.996,.799],[s*.108,2.006,.794]],[[s*.101,2.061,.793+e],[s*.117,2.057,.8+e],[s*.13,2.064,.791+e]],[[s*.126,2.109,.8+e],[s*.129,2.118,.802+e],[s*.132,2.11,.797+e]]]);use('HorseNeck');
}
// Two visible layers of long, tapered mane locks along the nape.
for(let i=0;i<=11;i++){const u=i/11,y=1.44+u*.533,z=.34+u*.412+.018*sin(u*PI),s=i%2===0?1:-1;
 const pts=[[s*.025,y+.014,z],[s*.015,y+.035,z-.038],[s*.07,y+.017,z-.093],[s*.133,y-.037,z-.103],[s*(.14+.018*sin(i)),y-.102,z-.110],[s*.132,y-.14-.016*cos(i),z-.085]];
 sweep(i%3===0?'Ash':'Graphite',pts,[[.018,.023],[.032,.035],[.045,.036],[.038,.03],[.017,.016],[.001,.001]],11);
 if(i%2===0)cord('SilkMane',pts.slice(1,5).map(p=>add(p,[s*.009,.008,0])),.0026)
}
for(let i=0;i<6;i++){const x=(i-2.5)*.017;const pts=[[x,1.989,.780],[x*1.1,2.006,.819],[x*1.13-.005,1.98,.867],[x*1.2-.018,1.923,.903],[x*1.1-.018,1.894+.012*sin(i),.921]];sweep(i%2?'Ash':'Graphite',pts,[[.018,.02],[.022,.021],[.019,.015],[.013,.01],[.001,.001]],11)}
use('HorseTail');
const tailCentre=[[0,1.326,-.834],[.005,1.311,-.945],[.035,1.205,-1.016],
 [.062,1.047,-1.086],[.076,.892,-1.162],[.056,.723,-1.204],
 [.004,.573,-1.207],[-.051,.437,-1.17],[-.070,.337,-1.111]];
const tailRadii=[[.042,.038],[.076,.062],[.091,.079],[.098,.087],[.100,.078],
 [.091,.069],[.069,.052],[.043,.034],[.006,.006]];
sweep('Graphite',smoothPath(tailCentre,2),smoothPath(tailRadii,2),21);
for(let i=0;i<17;i++){
 const a=TAU*i/17,theta=(k)=>a+.26*sin(.63*k+.4*a)+.030*k;
 const points=tailCentre.map((p,k)=>[p[0]+tailRadii[k][0]*.91*sin(theta(k)),
  p[1]-.035*(k/8)**4*(1+.85*sin(a*3)),
  p[2]+tailRadii[k][1]*.91*cos(theta(k))-.021*(k/8)**3*sin(a)]);
 const strandRadii=[.012,.018,.023,.027,.026,.022,.018,.012,.001];
 sweep(i%5===1?'SilkMane':i%3===0?'Ash':'Graphite',smoothPath(points,2),
  smoothPath(strandRadii.map(v=>[v,v*.81]),2),9);
}
// The two reins hang as graceful arcs from the snaffle rings and meet each gauntlet.
for(const s of [-1,1]){
 use('HorseNeck');
 strap('Mahogany',[[s*.088,1.967,.735],[s*.116,1.946,.78],[s*.138,1.885,.815],[s*.147,1.807,.866],[s*.135,1.717,.953],[s*.097,1.61,1.063]],.014);
 strap('Russet',[[s*.123,1.86,.842],[s*.116,1.775,.85],[s*.096,1.746,.848],[s*.044,1.743,.809]],.012);
 hoop('Gold',[s*.10,1.61,1.068],[0,.024,0],[0,0,.025],.004);
 ell('Gold',[s*.149,1.808,.864],[.006,.014,.014],11);
}
sweep('Mahogany',[[-.095,1.611,1.062],[-.098,1.639,1.103],[-.055,1.667,1.136],[0,1.682,1.144],[.055,1.667,1.136],[.098,1.639,1.103],[.095,1.611,1.062]],Array(7).fill([.01,.012]),9,[0,1,0]);
sweep('Gold',[[-.116,1.903,.850],[-.085,1.926,.884],[0,1.915,.934],[.084,1.926,.884],[.117,1.903,.85]],[[.009,.007],[.01,.008],[.013,.009],[.01,.008],[.009,.007]],10,[0,1,0]);
function flank(s,z,dy=0,dr=0){const ww=.276+.019*cos((z+.14)*4.8);return [s*(ww+dr),1.14+dy,z]}
for(const s of [-1,1]){
 use('HorseChest');
 // Wool saddlecloth with a raised curved topline and a scalloped heraldic border.
 const spans=[-.787,-.722,-.644,-.571,-.502,-.439,-.367,-.318,-.262,-.211];
 const hem=spans.map((z,i)=>.095+.045*cos(i*1.8)-(i===2?.132:0)-(i===5?.067:0));
 const blanket=Array.from({length:12},(_,k)=>spans.map((z,i)=>{const u=k/11,theta=Math.min(1.52,u*2.18),r=.342+.008*cos(i*.65);
 return [s*(.018+r*sin(theta)+.013*sin(i*.52)*u),1.515-.338*(1-cos(theta))-.185*Math.max(0,(u-.58)/.42)+hem[i]*u*.52,z]}));panel('Midnight',blanket,[s,1,0]);
 const border=blanket.at(-1);cord('Gold',border,.0085);cord('Ivory',border.map(p=>add(p,[s*.002,.019,0])),.005);
 for(const i of [2,5,8]){const p=border[i];ell('Antique',add(p,[s*.006,-.013,0]),[.011,.014,.011],9,5);sweep('Gold',[add(p,[s*.006,-.022,0]),add(p,[s*.006,-.05,0]),add(p,[s*.007,-.065,0])],[.004,.013,.007],8)}
 // Six-point gilt heraldic star worked into the haunch textile.
 const cx=s*.379,cy=1.205,cz=-.526;for(let i=0;i<8;i++){const a=TAU*i/8;const ring=[0,.012,.071,.012].map((r,j)=>{const ang=a+(j===1?-.65:j===3?.65:0);return [cx,cy+r*cos(ang),cz+r*sin(ang)]});panel('Gold',[[ring[0],ring[1]],[ring[3],ring[2]]],[s,0,0])}
 strap('Mahogany',[flank(s,-.11,.30,-.046),flank(s,-.098,.169),flank(s,-.085,-.012,.016),flank(s,-.07,-.201,-.039),[s*.11,.805,-.065]],.027);
 // Sweeping breast collar follows the shoulder and crosses the front of the brisket.
 const strapPts=[[s*.12,1.46,.296],[s*.224,1.398,.36],[s*.271,1.302,.398],[s*.26,1.199,.486],[s*.206,1.141,.567],[s*.12,1.115,.632],[0,1.103,.654]];
 strap('Mahogany',strapPts,.036);cord('Antique',strapPts.map(p=>add(p,[s*.001,.015,.003])),.004);
 for(let i=2;i<6;i++)ell('Gold',add(strapPts[i],[s*.004,.004,.007]),[.009,.01,.008],9,6)
}
ell('Gold',[0,1.103,.657],[.048,.045,.009],18,8);
ell('Gem',[0,1.105,.671],[.026,.027,.005],13,7);
// Shaped saddle seat, rolled cantle and pommel, layered skirts.
loft('Russet',[[0,1.423,-.367,.10,.049],[0,1.469,-.295,.156,.049],[0,1.468,-.196,.172,.048],[0,1.464,-.02,.15,.039],[0,1.473,.07,.116,.041],[0,1.479,.115,.052,.046]],'z',24);
for(const z of [-.25,.071]){const ps=Array.from({length:15},(_,i)=>{const a=PI*i/14;return [.138*cos(a),1.467+.056*sin(a),z]});cord('Mahogany',ps,.019);cord('Antique',ps.map(p=>add(p,[0,.015,z<0?-.014:.014])),.004)}
for(const s of [-1,1]){ell('Mahogany',[s*.217,1.314,-.079],[.04,.126,.130],18,11);strap('Russet',[[s*.232,1.352,-.071],[s*.275,1.225,-.095],[s*.287,1.074,-.117],[s*.29,.945,-.105]],.011);
 const xx=s*.298;const pp=[[xx,.979,-.105],[xx,.947,-.15],[xx,.90,-.157],[xx,.884,-.115],[xx,.884,-.055],[xx,.905,-.041],[xx,.958,-.074],[xx,.979,-.105]];cord('Antique',pp,.009);
 const stirrupBar=[[xx-.029,.887,-.108],[xx,.886,-.08],[xx+.030,.887,-.066]];
 sweep('Blade',stirrupBar,[[.008,.007],[.013,.008],[.008,.007]],9);
 const keeper=[s*.291,1.105,-.111];
 hoop('Antique',keeper,[0,.017,0],[0,0,.012],.0032);
 taperedLine('Russet',[[s*.240,1.408,-.208],[s*.263,1.362,-.230],
   [s*.289,1.279,-.213],[s*.296,1.235,-.200]],.008);
}
// A properly cut split riding gown. Its top follows the waist; the open hem clears horse and saddle.
function drape(side,u,v,inflate=0){const a=u*PI,sa=sin(a),ca=cos(a),wave=sin(10*a+.92*v+.27*sin(3*a));
 const fall=.115+.568*sa**.88+.060*(1-ca)+.022*wave*sa+.014*sin(3*a)*sa;
 // Extra fullness falls forward over the bent knee, then hangs nearly vertical below it.
 const flare=sin(Math.min(1,v*1.23)*PI/2),pleat=(.022*wave+.005*cos(19*a+.7*v))*v**1.38*sa;
 // Extra ease over the forward knee keeps the embroidered panel outside its steel cop.
 const kneeEase=.065*Math.exp(-(((u-.275)/.111)**2+((v-.67)/.27)**2));
 const xx=(.145+.262*flare+pleat+inflate+kneeEase)*sa;
 const zExtent=.12+v*(ca>=0?.246:.457)+.013*v*wave+inflate+kneeEase*.72;
 const yy=1.684-v*fall+.015*cos(12*a+.4*v)*sa*v;
 return [side*xx,yy,-.082+ca*zExtent]
}
// Mounted lower body.
for(const s of [-1,1]){use('HorseChest');
 const hip=[s*.080,1.56,-.10],thigh=[s*.171,1.447,-.024],knee=[s*.263,1.286,.066],calf=[s*.297,1.128,.016],ankle=[s*.302,.977,-.084];
 sweep('Midnight',[hip,thigh,knee,calf,ankle],[[.078,.085],[.077,.08],[.060,.053],[.058,.062],[.045,.049]],22);
 sweep('Blade',[[s*.273,1.25,.056],[s*.285,1.202,.041],[s*.297,1.146,.016],[s*.306,1.062,-.027],[s*.304,.987,-.072]],[[.057,.058],[.061,.066],[.060,.061],[.048,.05],[.045,.041]],20);
 ell('Silver',add(knee,[s*.013,0,.018]),[.041,.059,.049],19,12);ell('Gold',add(knee,[s*.048,0,.026]),[.006,.011,.010],10);
 for(const k of [0,1,2])sweep('Silver',[[s*.286,1.17+k*.027,.023+k*.011],[s*.288,1.16+k*.028,.025+k*.012]],[[.059,.055],[.061,.056]],18);
 sweep('Mahogany',[ankle,[s*.298,.945,-.076],[s*.3,.917,-.038],[s*.306,.904,.006],[s*.309,.902,.052]],[[.047,.053],[.049,.046],[.052,.047],[.05,.036],[.031,.022]],16);
 for(let k=0;k<3;k++)ell(k===2?'Antique':'Silver',[s*.3,.934-k*.007,-.037+k*.036],[.05-k*.004,.016,.031],16,8);
 // A separate folded skirt panel over each thigh has a narrow cream band in its hem.
 const grid=Array.from({length:15},(_,i)=>Array.from({length:59},(_,j)=>drape(s,j/58,i/14)));
 patch('Royal',grid,(i,j)=>[s*sin((j+.5)*PI/58),.45,cos((j+.5)*PI/58)]);
 const edge=Array.from({length:4},(_,i)=>Array.from({length:49},(_,j)=>drape(s,j/48,.926+i*.025,.0032)));
 patch('Ivory',edge,(i,j)=>[s*sin((j+.5)*PI/48),.5,cos((j+.5)*PI/48)]);
 cord('Gold',edge[0],.004);cord('Antique',edge.at(-1),.004);
 const braid=[.12,.88];for(const u of braid){const seam=Array.from({length:17},(_,i)=>drape(s,u,.06+i*.053,.0028));cord('Lapis',seam,.0042)}
 // A separately cut blue silk gore and a tapering wheat-spray are sewn to the heavy wool.
 for(const center of [.235,.755]){
  const gore=Array.from({length:12},(_,k)=>Array.from({length:9},(_,j)=>{
   const v=.19+.72*k/11,halfWidth=.040+.036*v;
   return drape(s,center+halfWidth*(j/4-1),v,.0038);
  }));
  panel('Lapis',gore,[s,.24,cos(center*PI)]);
  for(const j of [0,8])taperedLine('Antique',gore.map(row=>row[j]),.00235);
  const embroidery=(du,v)=>drape(s,center+du,v,.0066);
  taperedLine('Gold',[.54,.60,.67,.74,.82,.87].map(v=>embroidery(.004*sin(v*14),v)),.0026);
  for(const sign of [-1,1])for(const v of [.64,.72,.80]){
   const leaf=[embroidery(0,v),embroidery(sign*.012,v-.017),embroidery(sign*.026,v-.046)];
   sweep('Gold',smoothPath(leaf,2),[[.0006,.0007],[.0021,.0015],[.0029,.0017],[.0015,.0012],[.00045,.0005]],6);
  }
 }

}
use('Chest');
const torso=[[0,1.49,-.097,.125,.111],[0,1.548,-.093,.155,.121],[0,1.636,-.088,.139,.100],[0,1.721,-.086,.115,.084],[0,1.79,-.084,.127,.09],[0,1.869,-.086,.151,.108],[0,1.939,-.086,.17,.116],[0,1.998,-.083,.178,.106],[0,2.034,-.081,.148,.086],[0,2.061,-.077,.085,.067]];
loft('Royal',torso,'y',30);
loft('Blade',[[0,1.735,-.085,.118,.091],[0,1.775,-.083,.13,.099],[0,1.856,-.079,.152,.116],[0,1.934,-.078,.171,.123],[0,1.987,-.08,.168,.109],[0,2.024,-.077,.126,.086]],'y',30);
for(const s of [-1,1]){
 const plates=[[[.0,1.747,.017],[.05,1.765,.02],[.104,1.804,-.005]],[[.005,1.833,.045],[.071,1.853,.043],[.146,1.861,-.014]],[[.001,1.904,.056],[.084,1.928,.05],[.168,1.938,-.025]],[[.003,1.984,.037],[.088,1.997,.021],[.142,1.981,-.024]]].map(row=>row.map(([x,y,z])=>[s*x,y,z]));panel('Silver',plates,[s*.4,.13,1]);
 cord('Gold',[[0,1.75,.019],[s*.056,1.774,.021],[s*.101,1.804,-.001],[s*.136,1.831,-.012]],.0045);
 cord('Etch',[[s*.009,1.81,.04],[s*.045,1.852,.048],[s*.094,1.88,.038],[s*.142,1.889,.002]],.0022);
 cord('Gold',[[0,1.971,.052],[s*.043,1.992,.044],[s*.085,2.005,.027],[s*.147,1.992,-.011]],.0055);
}
for(let i=0;i<3;i++)loft(i===0?'Antique':'Silver',[[0,1.682+i*.026,-.085,.136-i*.003,.108-i*.002],[0,1.692+i*.026,-.085,.138-i*.003,.108-i*.002],[0,1.708+i*.026,-.085,.13-i*.003,.101-i*.002]],'y',27);
loft('Royal',[[0,2.013,-.078,.119,.094],[0,2.042,-.078,.102,.080],[0,2.077,-.08,.085,.070]],'y',24);loft('Gold',[[0,2.069,-.08,.0855,.07],[0,2.079,-.08,.085,.071]],'y',25);
loft('Skin',[[0,2.065,-.077,.053,.055],[0,2.102,-.078,.055,.055],[0,2.152,-.077,.06,.057]],'y',23);
function flower(t,c,r){const [x,y,z]=c;for(let k=0;k<6;k++){const a=TAU*k/6;const fan=[[x,y,z],[x+r*.27*cos(a-.5),y+r*.27*sin(a-.5),z+.003],[x+r*cos(a),y+r*sin(a),z],[x+r*.27*cos(a+.5),y+r*.27*sin(a+.5),z+.003]];panel(t,[[fan[0],fan[1]],[fan[3],fan[2]]])}ell('Gem',add(c,[0,0,.005]),[r*.20,r*.23,.005],10,6)}
flower('Gold',[0,1.925,.06],.037);
for(const s of [-1,1]){
 use('Chest');
 // Layered pauldron and blue standing shoulder cap.
 ell('Royal',[s*.155,1.994,-.082],[.091,.082,.083],20,10);
 for(let i=0;i<3;i++){
  const x=.184-i*.008,y=1.936+i*.027,rx=.068+i*.004,rz=.073+i*.005;
  const lame=Array.from({length:5},(_,r)=>Array.from({length:23},(_,j)=>{
   const a=-1.86+j/22*3.52,v=r/4;
   const rim=.005*(1-v)+.004*sin(PI*v);
   return [s*(x+(rx+rim)*cos(a)-v*.017),y+.039*v-.013*cos(a)-.006*(1-v)*cos(2*a),-.078+(rz+rim)*sin(a)];
  }));
  patch(i===1?'Blade':'Silver',lame,(r,j)=>[s*cos(-1.86+j/22*3.52),.45,sin(-1.86+j/22*3.52)]);
  cord(i===1?'Antique':'Blade',lame[0],.0038);
  for(const j of [2,18])ell('Gold',lame[2][j],[.004,.0045,.0047],9,6);
 }
 ell('Gold',[s*.18,1.993,.006],[.011,.01,.005],12,7)
}
// Split blue mantle flares behind; visible pale lining sweeps around the rider only, short enough to clear tack.
for(const s of [-1,1]){const pleat=Array.from({length:9},(_,i)=>Array.from({length:13},(_,j)=>{const v=i/8,u=j/12;return [s*(.044+.149*u+v*(.058+.07*u)),2.015-v*(.483+.074*u),-.167-v*(.166+.034*cos(u*PI))+.057*u+.017*v*cos(u*PI*5)]}));panel('Lapis',pleat,[s*.2,0,-1]);cord('Gold',pleat.at(-1),.0056)}
function hand(s,p){ // Gloves formed around the rein, individual curled fingertips straddle the loop.
 ell('Mahogany',p,[.032,.038,.045],16,9);for(let i=0;i<4;i++){const x=p[0]+s*(i-1.5)*.012,z=p[2]+.022,y=p[1]-.004;const tip=[x,y-.028+(i===3?.005:0),z+.003];sweep('Blade',[[x,y,z],[x,y-.009,z+.016],[x,y-.024,z+.02],tip],[.009,.009,.008,.0065],8);ell('Silver',[x,y-.004,z+.008],[.008,.010,.009],9,7)}
 sweep('Mahogany',[[p[0]-s*.021,p[1]+.009,p[2]+.006],[p[0]-s*.032,p[1]-.006,p[2]+.037],[p[0]-s*.016,p[1]-.016,p[2]+.043]],[.012,.011,.009],10);ell('Silver',add(p,[0,.012,.004]),[.029,.03,.037],14,8)}
const grips={};
for(const s of [-1,1]){use(s<0?'GestureArm':'Chest');
 const shoulder=[s*.176,1.977,-.08],el=[s*(s<0?.208:.211),1.803,-.026],fore=[s*.164,1.806,.077],wrist=[s*(s<0?.092:.122),s<0?1.824:1.79,s<0?.171:.157],grip=add(wrist,[0,-.007,.035]);grips[s]=world(grip,'body');
 sweep('Midnight',[shoulder,[s*.192,1.929,-.064],[s*.21,1.849,-.048],el,fore,wrist],[[.063,.072],[.065,.065],[.050,.057],[.049,.048],[.043,.046],[.032,.033]],19);
 ell('Blade',el,[.059,.057,.052],17,9);ell('Gold',add(el,[s*.055,0,0]),[.005,.014,.015],10,7);
 sweep('Silver',[add(el,[0,.008,.020]),fore,mix(fore,wrist,.52),wrist],[[.051,.048],[.057,.052],[.044,.047],[.035,.034]],20);
 sweep('Antique',[mix(fore,wrist,.83),mix(fore,wrist,.90)],[[.039,.037],[.039,.036]],19);
 hand(s,grip)
}
function unworld(p,basis){return add(rot(sub(p,[0,0,-.08]),-frames[basis]*PI/180),[0,0,-.08])}
use('HorseNeck');for(const s of [-1,1]){
 const dest=unworld(grips[s],'horse'),begin=[s*.105,1.609,1.072];
 const route=[begin,[s*.144,1.570,.992],[s*.183,1.548,.857],[s*.205,1.565,.719],
  [s*.198,1.612,.56],[s*.172,1.675,.391],mix([s*.172,1.675,.391],dest,.56),dest];
 strap('Mahogany',smoothPath(route,3),.008);
 cord('Antique',smoothPath(route.slice(0,3),2).map(p=>add(p,[s*.003,.002,0])),.00125);
 // A leather keeper closes the bit-end billet; the slack end emerges from inside the palm.
 const middle=route[1];hoop('Antique',middle,[.009,0,0],[0,.005,.003],.0017);
 use(s<0?'GestureArm':'Chest');const palm=unworld(grips[s],'body');
 const loop=[add(palm,[0,-.005,.021]),add(palm,[s*.007,-.030,.026]),
   add(palm,[s*.014,-.064,.023]),add(palm,[s*.010,-.086,.011]),
   add(palm,[-s*.001,-.094,.000]),add(palm,[-s*.007,-.081,-.004])];
 strap('Russet',smoothPath(loop,2),.0065);
 use('HorseNeck');
}
use('Head');
// Soft tapered face with a projected brow ridge, small integrated nose bridge and pronounced cheek planes.
const faceLevels=[[-.005,2.117,-.059,.019,.033],[-.002,2.133,-.059,.048,.06],[0,2.156,-.065,.075,.079],[0,2.181,-.068,.092,.088],[0,2.209,-.073,.103,.102],[0,2.239,-.077,.115,.110],[0,2.269,-.077,.119,.115],[0,2.299,-.081,.119,.115],[0,2.337,-.086,.115,.110],[0,2.37,-.09,.101,.102],[0,2.395,-.089,.079,.084],[0,2.411,-.086,.051,.065],[0,2.421,-.082,.019,.032]];
const smoothSections=smoothPath(faceLevels,2);
function addNose(z,y,x,facing){
 const blend=clamp((facing-.10)/.36);
 const bump=(cx,cy,wx,wy)=>Math.exp(-(((Math.abs(x)-cx)/wx)**2+((y-cy)/wy)**2));
 const cheek=.0105*bump(.067,2.233,.031,.023),socket=-.010*bump(.051,2.265,.032,.021);
 const brow=.006*bump(.049,2.288,.036,.011),temple=-.004*bump(.096,2.28,.015,.031);
 const bridge=.019*bump(0,2.251,.010,.041),tip=.021*bump(0,2.222,.014,.012);
 const alae=.010*bump(.015,2.218,.009,.007),philtrum=.0028*bump(.006,2.200,.003,.008);
 const chin=.007*bump(0,2.156,.031,.013),mouth=.004*bump(0,2.183,.030,.014);
 return z+(cheek+socket+brow+temple+bridge+tip+alae+philtrum+chin+mouth)*blend*blend*(3-2*blend);
}
function frontAt(x,y){let i=1;while(i<smoothSections.length-1&&smoothSections[i][1]<y)i++;const l=smoothSections[i-1],h=smoothSections[i],t=clamp((y-l[1])/(h[1]-l[1]));const r=l[3]+t*(h[3]-l[3]),z=l[2]+t*(h[2]-l[2]),rz=l[4]+t*(h[4]-l[4]);const facing=Math.sqrt(clamp(1-((x-l[0])/r)**2));return addNose(z+rz*facing,y,x,facing)}
loft('Skin',smoothSections,'y',72,(p,a)=>{
 // Keep the cranial vault inside the hair cap at the occiput and temples.
 const underHair=clamp((p[1]-2.265)/.080)*clamp((.86-cos(a))/.78);
 return[p[0]*(1-.13*underHair),p[1],addNose(p[2]+(-.084-p[2])*.24*underHair,p[1],p[0],cos(a))];
});
for(const s of [-1,1]){ell('Skin',[s*.113,2.238,-.080],[.024,.038,.018],16,10);ell('Blush',[s*.12,2.238,-.065],[.012,.024,.007],14,8);ell('Gold',[s*.117,2.205,-.063],[.006,.007,.005],10);
 const ex=s*.050,y0=2.263;
 function ep(u,v,depth){const x=ex+u*.0317,base=y0+s*u*.0028,arch=Math.max(0,1-u*u)**.74;
 const y=base+v*(v>=0?.0128:.0087)*arch*(1-.12*u*s);
 return [x,y,frontAt(x,y)+.0020+depth+.0030*arch*(1-v*v)]}
 const grid=Array.from({length:10},(_,i)=>Array.from({length:22},(_,j)=>ep(-1+2*j/21,-1+2*i/9,0)));panel('Sclera',grid);
 const irisCenter=ex-.001;const ellEye=(name,rx,ry,zl,cy)=>{const pg=Array.from({length:10},(_,i)=>Array.from({length:23},(_,j)=>{const th=TAU*j/22,r=i/9,x=irisCenter+rx*r*cos(th),y=cy+ry*r*sin(th);return [x,y,frontAt(x,y)+zl+.001*(1-r*r)]}));panel(name,pg)};
 ellEye('EyeDark',.0117,.0104,.0059,y0+.0017);ellEye('Iris',.0102,.0092,.0068,y0+.0016);ellEye('IrisLight',.0073,.0049,.0076,y0-.002);ellEye('EyeDark',.0056,.0064,.0087,y0+.0019);
 ell('Glint',[irisCenter-.003,y0+.005,frontAt(irisCenter-.003,y0+.005)+.0106],[.0023,.002,.0008],10,6);
 for(const dir of [1,-1]){
  const lid=Array.from({length:4},(_,j)=>Array.from({length:23},(_,k)=>{
   const u=k/11-1,v=j/3,p=ep(u,dir,0),arch=Math.max(0,1-u*u);
   p[1]+=dir*(dir===1?.0092:.0050)*v*arch;
   p[2]=frontAt(p[0],p[1])+.0013+.0032*(1-v)*arch+.0025*sin(v*PI)*arch;
   return p;
  }));
  panel('Skin',lid);
  if(dir===1){const rim=lid[0].map(p=>add(p,[0,0,.00075]));
   sweep('Lash',rim,rim.map((_,i)=>.00032+.00105*sin(i/(rim.length-1)*PI)),7);
   taperedLine('Blush',lid.at(-1).filter((_,i)=>i>2&&i<20),.00062);}
 }
 const brow=Array.from({length:16},(_,i)=>{const u=i/15,x=s*(.024+.060*u),y=2.289+.0078*sin(u*PI*.94)-.003*u;return [x,y,frontAt(x,y)+.002]});
 sweep('GoldHair',brow,brow.map((_,i)=>[.0014+.0016*sin(i/15*PI),.0015]),8,[0,1,0]);
 for(let k=1;k<13;k+=2){const p=brow[k];taperedLine('Butter',[add(p,[-s*.001,-.0008,.001]),add(p,[s*.002,.0019,.0015])],.00045)}
}
for(const s of [-1,1]){const x=s*.0125,y=2.215;const nare=[-1,-.5,0,.5,1].map(u=>{const xx=x+s*u*.0048,yy=y+.00125*sin(u*PI/2)+.0009*(1-u*u);return [xx,yy,frontAt(xx,yy)+.00135]});taperedLine('Lip',nare,.00095)}
use('Jaw');const lips=[];for(let row=0;row<5;row++){
 lips.push(Array.from({length:23},(_,i)=>{
  const u=i/11-1,x=u*.030,arch=1-u*u,base=2.185+.0026*u*u+.0005*u;
  const top=(.0046+.0019*Math.exp(-(((Math.abs(u)-.28)/.18)**2)))*(1-.30*Math.exp(-((u/.17)**2)));
  const y=base+[-.0056,-.0031,0,.0027,top][row]*arch;
  return [x,y,frontAt(x,y)+.0011+[0,.0025,.00025,.0019,0][row]*arch];}));
}panel('Lip',lips);taperedLine('Lash',lips[2].slice(1,-1).map(p=>add(p,[0,.0001,.00075])),.00058);
use('Head');
// Crown/hairline is continuous over the skull. Tapered laminar locks follow the skull instead of standing above it.
const hair=Array.from({length:15},(_,i)=>Array.from({length:49},(_,j)=>{
 const u=i/14,a=TAU*j/48,frac=(1-cos(a))/2;
 const hairline=1.03+1.39*frac+.07*cos(3*a)+.043*sin(8*a+.45)*(.9-.5*frac);
 const theta=.012+u*hairline,edge=.002*Math.max(0,(u-.8)/.2);
 return [(.128-edge)*sin(theta)*sin(a),2.286+.147*cos(theta),-.084+(.122-edge)*sin(theta)*cos(a)];
}));patch('Butter',hair,(i,j,p)=>sub(p,[0,2.286,-.084]),true);
function lock(t,ps,widths,zth=.006){
 const curve=smoothPath(ps,2),radii=smoothPath(widths.map(w=>[w]),2).map(([w])=>[Math.max(w,.00035),Math.max(.00045,zth*(.12+.88*Math.min(1,w/.014)))]);
 sweep(t,curve,radii,10);
 if(widths[1]>.015){
  for(const strand of [-.42,.24,.59]){
   const line=curve.slice(2,-2).map((p,i)=>add(p,[strand*radii[i+2][0],.0003,
    radii[i+2][1]*Math.sqrt(1-strand*strand)]));
   taperedLine(strand<0?'GoldHair':'BlondLight',line,strand>.5?.00048:.00069);
  }
 }
}
const fringes=[
 [[-.043,2.41,-.038],[-.058,2.398,-.006],[-.082,2.37,.015],[-.098,2.337,.022],[-.106,2.302,.012],[-.119,2.266,-.008]],
 [[-.029,2.412,-.026],[-.033,2.399,.001],[-.054,2.374,.022],[-.078,2.343,.033],[-.091,2.316,.027],[-.099,2.295,.014]],
 [[-.005,2.421,-.021],[-.011,2.407,.006],[-.022,2.385,.029],[-.046,2.355,.044],[-.057,2.327,.047],[-.072,2.303,.036]],
 [[.016,2.418,-.019],[.019,2.404,.01],[.012,2.38,.033],[.001,2.355,.052],[-.018,2.331,.054],[-.028,2.305,.052]],
 [[.035,2.414,-.022],[.048,2.402,.003],[.052,2.38,.029],[.044,2.351,.048],[.035,2.325,.051],[.026,2.31,.054]],
 [[.058,2.407,-.029],[.074,2.39,-.002],[.08,2.359,.027],[.077,2.338,.041],[.068,2.312,.039],[.057,2.295,.035]],
 [[.082,2.39,-.051],[.099,2.369,-.018],[.11,2.339,.006],[.108,2.304,.013],[.098,2.283,.026],[.092,2.267,.024]]];
fringes.forEach((p,i)=>{
 const strand=p.map(([x,y,z],k)=>[x+.006*sin(k*.6+i*.75),y+.005*(k/5)**2,z-.002]);
 lock(i===3||i===6?'Honey':'Butter',strand,[.016,.024,.022,.016,.007,.0007],.0065);
});
// Curved crown sections gather from the temples into the braided bun.
for(const s of [-1,1])for(let k=0;k<6;k++){
 const angle=.39+.24*k;
 const ridge=Array.from({length:14},(_,i)=>{
  const f=i/13,a=s*(angle+(2.93-angle)*f),theta=.25+k*.145+f*(1.66-k*.116);
  return [.1288*sin(theta)*sin(a),2.286+.148*cos(theta),-.084+.123*sin(theta)*cos(a)];
 });
 taperedLine(k%2?'GoldHair':'BlondLight',ridge,.00115);
}
for(const s of [-1,1]){const lockPath=[[s*.078,2.38,-.058],[s*.11,2.35,-.04],[s*.124,2.304,-.027],[s*.125,2.25,-.024],[s*.119,2.201,-.011],[s*.101,2.166,.013],[s*.103,2.141,.015]];lock('Butter',lockPath,[.025,.027,.024,.019,.015,.006,.0008],.009);
 const lower=[[s*.107,2.318,-.084],[s*.13,2.29,-.072],[s*.135,2.237,-.065],[s*.13,2.187,-.055],[s*.14,2.157,-.038]];lock('GoldHair',lower,[.021,.022,.021,.013,.001],.008);
}
ell('GoldHair',[0,2.303,-.213],[.082,.081,.066],24,15);
// Braided circlet wraps the bun, individual interlocking golden locks cast a silhouette.
for(let i=0;i<27;i++){const a=TAU*i/27,xx=.098*sin(a),yy=2.311+.060*cos(a),zz=-.183-.027*cos(a);const ps=[[xx-.012*cos(a),yy+.012*sin(a),zz+.009],[xx,yy,zz-.008],[xx+.012*cos(a),yy-.012*sin(a),zz-.005]];
 sweep(i%3?'Butter':'BlondLight',ps,[[.006,.008],[.011,.012],[.006,.007]],9)}
for(let i=0;i<11;i++){const a=TAU*i/11,pts=Array.from({length:10},(_,j)=>{const f=j/9,th=a+f*1.7;return [.085*sin(PI*f)*cos(th),2.304+.077*cos(PI*f),-.214-.061*sin(PI*f)*sin(th)]});cord(i%2?'Butter':'GoldHair',pts,.0034)}
// Blue ribbon has folded loops and two loose ends, visible in three-quarter and rear views.
for(const s of [-1,1]){const bow=[[0,2.276,-.239],[s*.047,2.289,-.252],[s*.091,2.294,-.248],[s*.066,2.260,-.24],[s*.014,2.269,-.245]];sweep('Royal',bow,[[.009,.008],[.015,.009],[.013,.008],[.015,.008],[.007,.005]],10);
 const ribbon=[[s*.011,2.268,-.241],[s*.046,2.232,-.243],[s*.052,2.187,-.231],[s*.079,2.151,-.212],[s*.086,2.107,-.226]];sweep('Lapis',ribbon,[[.009,.004],[.015,.005],[.015,.005],[.012,.004],[.007,.002]],8)}
ell('Royal',[0,2.273,-.254],[.019,.013,.016],12,8);
lock('Butter',[[-.008,2.421,-.071],[-.023,2.432,-.066],[-.045,2.435,-.07],[-.063,2.43,-.080],[-.071,2.423,-.093]],[.006,.006,.004,.002,.0005],.003);
// Fitted silver chamfron. Each cross-section rides the dorsal profile of the muzzle.
use('HorseNeck');
const profile=[[1.929,.817,.119,.126,.48],[1.903,.868,.135,.146,.73],[1.875,.906,.134,.15,.65],[1.833,.943,.123,.145,.54],[1.783,.973,.111,.12,.54],[1.726,1.022,.099,.097,.63],[1.683,1.054,.086,.083,.66],[1.649,1.089,.081,.076,.47],[1.621,1.111,.078,.071,.12]];
const chamfron=profile.map(([y,z,rx,rr,w],i)=>Array.from({length:19},(_,j)=>{const x=(2*j/18-1)*w,scale=rr*Math.sqrt(1-x*x)+.020,ridge=.010*(1-Math.abs(x/w));return [rx*x,y+.70*(scale+ridge),z+.72*(scale+ridge)]}));
panel('Silver',chamfron,[0,.68,.72]);for(const s of [0,18])cord('Antique',chamfron.map(r=>add(r[s],[0,.002,.002])),.004);
cord('Gold',chamfron.map(r=>add(r[9],[0,.003,.003])),.005);
for(const j of [3,15])cord('Blade',chamfron.slice(1,7).map(row=>add(row[j],[0,.001,.001])),.0027);
const chr=chamfron[2][9];ell('Gold',add(chr,[0,.008,.008]),[.026,.014,.016],15,8);ell('Gem',add(chr,[0,.017,.018]),[.015,.008,.009],14,7);
// The detached poll strap is concealed beneath the brow armour and behind the ears.
for(const s of [-1,1]){const guard=[[s*.072,2.012,.811],[s*.095,1.973,.846],[s*.115,1.931,.874],[s*.135,1.903,.884]];sweep('Blade',guard,[[.022,.011],[.029,.016],[.023,.011],[.008,.008]],12);cord('Gold',guard.map(p=>add(p,[s*.009,.007,.006])),.0036)}
use('HorseChest');
// Articulated peytral with raised, overlapping scalloped plates across the brisket and the shoulder.
function pey(s,a,k,u){const sinA=sin(a),cosA=cos(a);const yy=1.046+.145*sinA+k*.078+.02*cos(3*a)-.047*(1-u)*cosA;return [s*(.360+.005*k)*sinA,yy,.344+(.354+.033*k)*cosA]}
for(const s of [-1,1]){
 for(let layer=0;layer<3;layer++){const band=Array.from({length:5},(_,r)=>Array.from({length:26},(_,j)=>{const u=j/25,a=u*1.46;const p=pey(s,a,layer,u),v=r/4;p[1]+=.092*v-.011*cos(4*PI*u)*(1-v);p[0]+=s*.009*sin(a)*sin(PI*v);p[2]+=.011*cos(a)*sin(PI*v);return p}));
  panel(layer===1?'Blade':'Silver',band,[s,.15,.7]);cord(layer===1?'Silver':'Gold',band[0],layer===0?.0065:.0042);
  if(layer===2){cord('Antique',band.at(-1),.007);for(const j of [3,9,17,23])ell('Gold',add(band[2][j],[s*.004,0,.005]),[.009,.009,.007],10,7)}
 }
}
// Central pointed escutcheon overlaps both halves of the peytral.
const bib=[[[0,1.004,.692],[0,1.003,.698],[0,1.004,.69]],[[-.08,1.093,.706],[0,1.078,.738],[.08,1.093,.706]],[[-.12,1.213,.731],[0,1.203,.771],[.12,1.213,.731]],[[-.085,1.302,.75],[0,1.326,.788],[.085,1.302,.75]]];panel('Blade',bib);cord('Gold',bib.map(row=>row[0]),.005);cord('Gold',bib.map(row=>row[2]),.005);flower('Gold',[0,1.237,.78],.039);
for(const s of [-1,1]){
 // Three tapering silver tassets flank the rider's overskirt. Lower edge fans away from the blue textile.
 for(let plate=0;plate<3;plate++){
 const grid=Array.from({length:6},(_,i)=>Array.from({length:21},(_,j)=>{const a=j/20,b=i/5;
 const u=.29+.40*a+.018*(.5-a)*b;
 const v=.022+(2-plate)*.112+b*(.145+.062*sin(a*PI))+.016*cos(2*PI*a);
 const flutes=.0035*cos(5*PI*a)*sin(PI*b);
 const p=drape(s,u,v,.014+.008*sin(b*PI)+.002*plate+flutes);p[1]+=.004;return p}));
 patch(plate===1?'Blade':'Silver',grid,(i,j)=>[s*.87,.45,cos((.29+.4*j/20)*PI)]);
 cord('Gold',grid.at(-1),.0043);
 taperedLine('Etch',grid[4].slice(2,-2).map(p=>add(p,[s*.0036,.0026,0])),.0016);
 for(const j of [3,17])ell('Antique',add(grid[2][j],[s*.005,.004,0]),[.006,.005,.006],10,6);
 }
 // Narrow fluted girdle and tabs physically connect the tassets with the waist.
 const cinch=Array.from({length:3},(_,i)=>Array.from({length:22},(_,j)=>drape(s,.14+.72*j/21,.010+i*.017,.011)));
 patch('Antique',cinch,(i,j)=>[s,.45,cos((.14+.72*j/21)*PI)]);

}
// Upturned slim silver shoulder cops overlap the upper lames; the throat has a visible gorget.
use('Chest');
for(const s of [-1,1]){
 ell('Blade',[s*.164,2.026,-.083],[.083,.051,.085],22,12);
 const cap=Array.from({length:9},(_,r)=>Array.from({length:33},(_,j)=>{
  const v=r/8,a=TAU*j/32,outer=cos(a);
  const shell=.006+.994*sin(v*PI/2),ridge=.004*Math.max(0,cos(4*a))*sin(PI*v);
  const drop=.061+.019*outer;
  return [s*(.169+(.103+ridge)*shell*outer),2.103-drop*(1-cos(v*PI/2))+.002*cos(4*a)*v,
   -.081+(.092+ridge)*shell*sin(a)];
 }));
 patch('Silver',cap,(r,j)=>[s*cos(TAU*j/32),1-r/10,sin(TAU*j/32)],true);
 cord('Gold',cap.at(-1),.0040);
 for(const j of [3,8,28]){const a=TAU*j/32;
  const emboss=cap.slice(2,8).map(row=>add(row[j],[s*.0024*cos(a),.0018,.0024*sin(a)]));
  taperedLine(j===8?'Gold':'Blade',emboss,.0018);
 }
 for(const j of [3,10])ell('Gold',cap[7][j],[.004,.004,.004],9,6);
 sweep('Silver',[[s*.062,2.072,-.02],[s*.055,2.058,-.011],[s*.029,2.043,.001],[0,2.037,.006]],[[.019,.009],[.019,.007],[.016,.008],[.013,.01]],12,[0,1,0])
}

use('HorseNeck');
// Layered sweeping near-side mane, breaking up the nape contour.
for(let i=0;i<12;i++){const t=i/11,top=[.035,1.448+.51*t,.31+.406*t];const p=[top,add(top,[.034,.03,-.016]),add(top,[.088,.019,-.026]),add(top,[.121,-.023,-.046]),add(top,[.126,-.077,-.056]),add(top,[.114,-.122-.016*cos(i),-.056])];
 sweep(i%4===1?'Ash':'Graphite',p,[[.018,.013],[.026,.022],[.030,.023],[.026,.019],[.014,.011],[.001,.001]],12);
 if(i%3===0)cord('Ash',p.slice(1,5).map(x=>add(x,[.011,.005,.004])),.0025)
}
for(const s of [-1,1]){use('HorseChest');
 // End the shoulder blades in a narrow riveted vertical edge.
 const edge=[];for(let i=0;i<7;i++){const v=i/6;edge.push([s*(.350+.014*v),1.175+.276*v,.363+.011*v])}cord('Antique',edge,.006);
}

// One shared UV atlas and packed metallic/roughness texture for every articulated part.
const g={asset:{version:'2.0',generator:'Eventide Rider — castle knight and bay horse'},scene:0,scenes:[{name:'Eventide Rider',nodes:[0]}],nodes:[],meshes:[],materials:[{name:'Eventide steel, embroidered wool, skin and bay coat',pbrMetallicRoughness:{baseColorTexture:{index:0},metallicRoughnessTexture:{index:1},metallicFactor:1,roughnessFactor:1}}],textures:[{sampler:0,source:0},{sampler:0,source:1}],samplers:[{magFilter:9729,minFilter:9987,wrapS:33071,wrapT:33071}],images:[],buffers:[],bufferViews:[],accessors:[]};
let bytes=0,chunks=[],count=0;
function view(b,target){const idx=g.bufferViews.length;g.bufferViews.push({buffer:0,byteOffset:bytes,byteLength:b.length,...(target?{target}:{})});chunks.push(b);bytes+=b.length;const pad=(4-bytes%4)%4;if(pad){chunks.push(Buffer.alloc(pad));bytes+=pad}return idx}
function acc(vals,n,type,target,bounds){const arr=type===5126?Float32Array.from(vals):type===5123?Uint16Array.from(vals):Uint32Array.from(vals),idx=g.accessors.length;const ac={bufferView:view(Buffer.from(arr.buffer),target),componentType:type,count:vals.length/n,type:['','SCALAR','VEC2','VEC3'][n]};if(bounds){ac.min=new Array(n).fill(Infinity);ac.max=new Array(n).fill(-Infinity);for(let u=0;u<vals.length;u++){const k=u%n;ac.min[k]=Math.min(ac.min[k],arr[u]);ac.max[k]=Math.max(ac.max[k],arr[u])}}g.accessors.push(ac);return idx}
for(const p of parts){const n={name:p.name,translation:p.parent?sub(p.org,p.parent.org):p.org};const kids=parts.map((c,i)=>c.parent===p?i:-1).filter(x=>x>=0);if(kids.length)n.children=kids;
 if(p.inds.length){const prim={attributes:{POSITION:acc(p.verts,3,5126,34962,true),NORMAL:acc(p.norms,3,5126,34962),TEXCOORD_0:acc(p.uv,2,5126,34962)},indices:acc(p.inds,1,p.verts.length/3>65535?5125:5123,34963,false),material:0};n.mesh=g.meshes.length;g.meshes.push({name:p.name+' sculpt',primitives:[prim]});count+=p.inds.length/3}g.nodes.push(n)
}
for(const [name,buf] of [['Eventide hand-painted palette',albedo],['Eventide surface response',pbr]])g.images.push({name,bufferView:view(png(tw,th,buf)),mimeType:'image/png'});
g.buffers=[{byteLength:bytes}];const raw=Buffer.from(JSON.stringify(g)),pad=(4-raw.length%4)%4,headb=Buffer.alloc(20),binh=Buffer.alloc(8);headb.write('glTF');headb.writeUInt32LE(2,4);headb.writeUInt32LE(28+raw.length+pad+bytes,8);headb.writeUInt32LE(raw.length+pad,12);headb.writeUInt32LE(0x4e4f534a,16);binh.writeUInt32LE(bytes);binh.writeUInt32LE(0x004e4942,4);
const full=Buffer.concat([headb,raw,Buffer.alloc(pad,32),binh,...chunks]);fs.mkdirSync(path.dirname(outputPath),{recursive:true});fs.writeFileSync(outputPath,full);
console.log('Triangles',count,'materials',g.materials.length,'draw primitives',g.meshes.length,'bytes',full.length);
const limits=[[Infinity,Infinity,Infinity],[-Infinity,-Infinity,-Infinity]];for(const p of parts)for(let i=0;i<p.verts.length;i+=3)for(let k=0;k<3;k++){const value=p.verts[i+k]+p.org[k];limits[0][k]=Math.min(limits[0][k],value);limits[1][k]=Math.max(limits[1][k],value)}
console.log('Authoring bounds',limits,'Rider eye',world([0,2.263,.024],'face'));
