const {tau,sin,cos,clamp,mix,head,use,loft,sweep,path,faceplate,pin,grid}=require('./guard-forms.cjs');
use(head);
// An open Corinthian-style crest helmet; the brow and articulated cheeks leave the entire expression exposed.
for(let i=0;i<3;i++){
 const y0=1.54+.038*i,rad=.104+.005*i;
 grid(i===2?'iron':'oxhide',28,4,(u,v)=>{const a=1.20+(tau-2.4)*u,r=rad+.003*sin(Math.PI*v);
   return[.004+r*sin(a),y0+.059*v-.018*(-cos(a)),-.014+.105*cos(a)];});
}
loft('iron',[[.004,1.684,-.012,.119,.118],[.004,1.702,-.013,.127,.122],[.004,1.741,-.014,.125,.123],
 [.004,1.779,-.014,.115,.115],[.004,1.811,-.015,.094,.099],[.004,1.838,-.015,.055,.069],[.004,1.851,-.019,.021,.038]],44,
 (p,t,v)=>[p[0],p[1]-.076*(1-v)**4*clamp((1-cos(t))*.7),p[2]]);
for(const s of [-1,1]){
 // Sculpt the brow in 3D across the temples, avoiding paper-flat planes.
 const gridB=(u,v)=>{const xx=.004+s*(.002+.117*u),yy=mix(1.669+.023*sin(u*Math.PI)-.021*u**2,1.711+.026*sin(u*Math.PI)-.030*u*u,v),
 zz=.126-.089*u**2+.010*sin(v*Math.PI);return[xx,yy,zz];};
 grid('steel',25,4,gridB,{reverse:s<0});path('edge',Array.from({length:20},(_,i)=>gridB(i/19,.09)),.0030,7);
 const px=.004;
 // Hammered cheek plate curves back around the face. The welt follows the curved
 // inside edge and turns under at the tip instead of a fan of flat metal facets.
 const profile=[[.062,.071,1.521],[.064,.083,1.537],[.074,.111,1.580],
   [.080,.125,1.604],[.098,.134,1.626],[.104,.137,1.648],[.099,.130,1.672],[.096,.121,1.689]];
 const cheek=(u,v,offset=0)=>{
  const h=v*(profile.length-1),i=Math.min(profile.length-2,Math.floor(h)),f=h-i;
  const [inside,outside,y]=profile[i].map((a,k)=>mix(a,profile[i+1][k],f));
  const x=mix(inside,outside,u);
  return[px+s*x,y-.005*(1-v)*sin(Math.PI*u),
   .070+.37*(.124-x)-.019*u*u+.011*sin(Math.PI*u)+.006*sin(Math.PI*v)+offset];
 };
 grid('oxhide',10,16,(u,v)=>cheek(u,v,-.008),{reverse:s>0});
 grid('steel',12,21,cheek,{reverse:s<0});
 for(const edge of [0,1])grid('iron',2,16,(u,v)=>cheek(edge,v,mix(-.008,0,u)),{reverse:edge===0?s<0:s>0});
 path('edge',Array.from({length:18},(_,i)=>cheek(0,i/17,.001)),.0024,7);
 path('iron',Array.from({length:14},(_,i)=>cheek(.65,.10+.72*i/13,.0006)),.0017,6);
 pin('iron',px+s*.106,1.654,.083,.013);pin('gold',px+s*.106,1.654,.086,.0063);
}
faceplate('iron',[[-.011,1.698],[.004,1.709],[.019,1.698],[.012,1.665],[.011,1.635],[.004,1.620],[-.003,1.634],[-.005,1.666]],(x,y)=>.124+(1.694-y)*.21,.009,.005);
for(const s of [-1,1])path('edge',[[.004+s*.016,1.706,.112],[.004+s*.045,1.755,.105],[.004+s*.051,1.81,.070],
 [.004+s*.042,1.843,.008],[.004+s*.037,1.824,-.062],[.004+s*.077,1.747,-.112],[.004+s*.087,1.694,-.105]],.0029);

// The short packed base fills the forged rails. Separate swept horsehair tufts
// above it leave actual daylight between the tapered ends at grazing angles.
function crestBase(t){
 return 1.848-.057*(2*t-1)**2-.038*(1-t)**3;
}
function crest(t,v,side){
 const a=Math.PI*t,fan=Math.sin(a)**.55;
 const rake=.024+.111*(1-t)**2+.025*Math.sin(a),rise=.105+.056*fan-.043*(1-t)**4;
 const serration=(.0039*Math.sin(t*93+.3)+.0018*Math.sin(t*163))*(.2+.8*fan);
 const w=(.006+.019*fan)*(1-v)+(.005+.046*fan)*v*(1-.13*v);
 const flute=.0029*Math.sin(t*108+v*3.1)+.0015*Math.cos(t*173-v*1.7);
 return [.004+side*(w+flute*fan*Math.sin(Math.PI*v*.87))+.005*v*Math.sin(t*9),
    crestBase(t)+v*(rise+serration*v*v),mix(-.157,.107,t)-rake*v**1.47];
}
for(const side of [-1,1]) {
 grid('crest',38,4,(t,v)=>crest(t,v*.35,side),{reverse:side>0});
 const rail=Array.from({length:33},(_,i)=>{const t=i/32,p=crest(t,0,side);return[p[0]+.0025*side,p[1]+.001,p[2]];});
 path('iron',rail,.010,10);
 const gilt=rail.map(([x,y,z])=>[x+.0065*side,y+.005,z]);
 path('welt',gilt,.0022,6);
}
grid('crest',38,3,(u,v)=>{const a=crest(u,.35,-1),b=crest(u,.35,1);return[mix(a[0],b[0],v),a[1]+.003*Math.sin(Math.PI*v),a[2]];});
for(const t of [0,1]){
 grid('crest',4,5,(u,v)=>{const a=crest(t,v*.35,-1),b=crest(t,v*.35,1);return[mix(a[0],b[0],u),a[1],a[2]];},{reverse:t===0});
}
for(const side of [-1,0,1]){
 const count=side===0?12:14;
 for(let i=0;i<count;i++){
  const t=clamp((i+.36+.12*side)/(count-.25)),base=crestBase(t);
  const fan=sin(Math.PI*t),z=mix(-.157,.107,t);
  const length=.089+.082*fan+.024*t+.012*cos(i*2.36+side)-.012*Math.abs(side);
  const rake=.034+.168*(1-t)**1.5+.020*fan;
  const width=.94+.11*sin(i*2.75+side);
  const rows=[0,.22,.45,.67,.84,.95,1].map((v,j)=>{
   const x=.004+side*(.011+.034*v**1.3)*(.48+.52*fan)+.004*v*sin(8*t+side);
   const y=base+.012+length*v-.013*v**4*(1-t);
   const front=z-rake*v**1.5-.010*v*sin(t*6+side);
   return[x,y,front,[.008,.011,.012,.009,.006,.003,.0009][j]*width,
     [.014,.018,.023,.021,.014,.007,.001][j]*width];});
  sweep(i%4===0?'plume':'crest',rows,9);
 }
}
