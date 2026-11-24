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
 const px=.004,plate=[[.099,1.687],[.125,1.684],[.136,1.647],[.124,1.604],[.105,1.571],[.070,1.516],
  [.058,1.525],[.070,1.576],[.074,1.607],[.089,1.613],[.101,1.632]];
 const z=(x,y)=>.065+.37*(.124-Math.abs(x-.004))+.011*Math.exp(-((y-1.61)**2)/.0016);
 faceplate('oxhide',plate.map(([x,y])=>[px+s*(x-.002),y-.002]),(x,y)=>z(x,y)-.005,.019,.003);
 faceplate('steel',plate.map(([x,y])=>[px+s*x,y]),z,.017,.009);
 path('edge',[[px+s*.100,1.632,z(px+s*.100,1.632)+.004],[px+s*.087,1.612,.092],
 [px+s*.075,1.605,.095],[px+s*.071,1.576,.094],[px+s*.060,1.526,.091]],.0028);
 pin('iron',px+s*.106,1.654,.083,.013);pin('gold',px+s*.106,1.654,.086,.0063);
}
faceplate('iron',[[-.011,1.698],[.004,1.709],[.019,1.698],[.012,1.665],[.011,1.635],[.004,1.620],[-.003,1.634],[-.005,1.666]],(x,y)=>.124+(1.694-y)*.21,.009,.005);
for(const s of [-1,1])path('edge',[[.004+s*.016,1.706,.112],[.004+s*.045,1.755,.105],[.004+s*.051,1.81,.070],
 [.004+s*.042,1.843,.008],[.004+s*.037,1.824,-.062],[.004+s*.077,1.747,-.112],[.004+s*.087,1.694,-.105]],.0029);

// A flared horsehair comb locked into two forged cheek rails. Each side is a continuous
// pleated surface; tapered, overlapping fiber group tips break up only the outer edge.
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
 grid('crest',80,9,(t,v)=>crest(t,v,side),{reverse:side>0});
 const rail=Array.from({length:33},(_,i)=>{const t=i/32,p=crest(t,0,side);return[p[0]+.0025*side,p[1]+.001,p[2]];});
 path('iron',rail,.010,10);
 const gilt=rail.map(([x,y,z])=>[x+.0065*side,y+.005,z]);
 path('welt',gilt,.0022,6);
}
grid('crest',80,4,(u,v)=>{const a=crest(u,1,-1),b=crest(u,1,1);return[mix(a[0],b[0],v),a[1]+.003*Math.sin(Math.PI*v),a[2]-.002*Math.sin(Math.PI*v)];});
for(const t of [0,1]){
 grid('crest',4,12,(u,v)=>{const a=crest(t,v,-1),b=crest(t,v,1);return[mix(a[0],b[0],u),a[1],a[2]];},{reverse:t===0});
}
for(const s of [-1,1]){
 for(let i=0;i<16;i++){
   const a=.018+.060*i,span=.037+.010*Math.cos(i*1.4);
   grid('crest',4,6,(u,v)=>{
     const t=clamp(a+span*u-.014*v,.001,.998),top=.85+.10*Math.sin(Math.PI*u)+.08*(1-u)**2;
     const p=crest(t,mix(.42,top,v),s),lift=(.002+.003*Math.sin(u*Math.PI))*Math.sin(Math.PI*v*.95);
     return[p[0]+s*lift,p[1]+.003*v*v*Math.sin(Math.PI*u),p[2]-.007*v*v];
   },{reverse:s>0});
 }
}
