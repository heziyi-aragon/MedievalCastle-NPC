// Build with Node.js: node guard-figure.cjs <output.glb>. All geometry and textures
// are generated locally. The only disk write is the explicitly named output.
const F=require('./guard-forms.cjs');
const {tau,sin,cos,clamp,add,sub,mul,norm,mix,legs,chest,head,jaw,left,right,use,tri,grid,loft,egg,sweep,path,faceplate,pin,disc,band}=F;
use(legs);
// A grounded stance: the shield-side hip bears the weight; the free foot is further forward.
for(const s of [-1,1]){
 const x=s<0?-.187:.142, z=s<0?.142:-.083, knee=x*(s<0?.86:.98), boot=x*1.06;
 loft('cloak',[[x*.82,.40,z,.059,.062],[knee,.52,z+.015,.074,.081],
  [x*.83,.641,z*.83,.085,.082],[x*.65,.732,z*.39,.082,.077],
  [x*.51,.805,z*.22,.083,.080],[x*.38,.90,z*.08,.087,.085]],24);
 loft('leather',[[boot,.036,z+.029,.077,.106],[boot,.074,z+.038,.078,.122],[boot,.111,z+.014,.066,.084],[x,.155,z-.004,.058,.066],[x,.244,z-.014,.069,.066],[knee,.343,z-.012,.074,.074],[knee,.423,z,.063,.069]],24);
 loft('oxhide',[[boot,.000,z+.069,.075,.149],[boot,.013,z+.067,.081,.155],[boot,.027,z+.069,.079,.151],[boot,.037,z+.067,.078,.143]],26);
 loft('leather',[[boot,.026,z+.069,.077,.144],[boot,.052,z+.071,.077,.139],[boot,.073,z+.061,.071,.128],[boot,.094,z+.047,.06,.097],[boot,.105,z+.023,.045,.064]],26);
 // A shaped raised greave with a longitudinal ridge over a soft, separately modelled boot.
 const greave=(u,v)=>{const q=u*2-1,w=.049+.013*sin(v*Math.PI*.75);
   return[knee+q*w, mix(.112+.027*q*q,.414-.022*q*q,v),
     z+.060+.031*(1-q*q)+.009*Math.exp(-q*q/.06)+.006*sin(v*Math.PI)];};
 grid('steel',15,14,greave);
 for(const u of [0,1])path('edge',Array.from({length:10},(_,j)=>greave(u,j/9)),.0021,6);
 for(const v of [0,1])path('iron',Array.from({length:13},(_,j)=>greave(j/12,v)),.0033,6);
 for(let i=0;i<2;i++){band('oxhide',knee,z,.175+i*.188,.067+i*.004,.071,.015);pin('gold',knee+s*.053,.177+i*.188,z+.053,.006);}
 for(let j=0;j<3;j++){const yy=.061+j*.013,zz=z+.11-j*.021;path('welt',[[boot-.056,yy-.005,zz-.012],[boot-.028,yy,zz+.012],[boot,yy+.005,zz+.020],[boot+.032,yy,zz+.009],[boot+.056,yy-.005,zz-.012]],.0028,7);}
 egg('iron',knee,.469,z+.060,.070,.072,.035,20,10);
 grid('steel',22,7,(u,v)=>{const a=tau*u,r=.001+.999*v;
   return[knee+.060*sin(a)*r,.474+.057*cos(a)*r+.006*(1-r*r),z+.082+.031*(1-r*r)+.003*sin(a)**2*(1-r)];},{closed:true});
}
// Undersuit and leather pteruges. Each skirt element curves around the hip independently.
use(chest);
loft('cloak',[[0,.72,-.004,.175,.121],[0,.83,-.005,.170,.128],[0,1.02,-.005,.155,.113],[.004,1.19,-.009,.212,.135],[.004,1.35,-.009,.237,.142],[.006,1.414,-.010,.155,.098]],32);
for(let layer=0;layer<2;layer++){
 const count=layer?17:18;
 for(let i=0;i<count;i++){const a=tau*i/count+(layer?.11:0),width=(layer?.42:.46)*tau/count;
  const stop=.647+(layer?.068:0)+.037*cos(a+.2)+.016*cos(a*3)-.031*sin(a),top=layer?.949:.918;
  const patchAt=(u,v,raise=0)=>{const uu=(u*2-1),a1=a+uu*width*(.90+.10*v)+.055*(1-v);const y=mix(stop+.029*Math.pow(Math.abs(uu),2.2),top,v);
   const R=(layer?.197:.188)+.061*(1-v),Z=(layer?.148:.137)+.080*(1-v);
   return [R*sin(a1)+raise*sin(a1),y,-.002+.014*(1-v)+Z*cos(a1)+raise*cos(a1)];};
  grid(layer?'leather':'oxhide',5,4,patchAt);
  for(const v of [0,.92])path(layer?'welt':'leather',Array.from({length:7},(_,j)=>patchAt(j/6,v,.0008)),.00215,5);
  if(layer){const p=patchAt(.5,.84,.0014);egg('gold',...p,.0042,.0042,.0042,8,5);}
 }
}
// Torso armor, cast musculature and silhouette derived from one continuous, shaped shell.
const torso=[
 [0,.948,-.008,.145,.108],[0,.984,-.008,.157,.119],[0,1.028,-.010,.154,.123],
 [.003,1.086,-.012,.160,.133],[.004,1.148,-.013,.184,.148],[.008,1.213,-.015,.220,.169],
 [.011,1.259,-.017,.247,.178],[.012,1.309,-.018,.256,.173],[.008,1.354,-.017,.246,.157],
 [.003,1.388,-.015,.207,.131],[0,1.418,-.013,.149,.104],[0,1.431,-.012,.098,.084]];
const gauss=(x,w)=>Math.exp(-(x*x)/(w*w));
function torsoAt(u,v){const h=v*(torso.length-1),i=Math.min(torso.length-2,Math.floor(h)),f=h-i;
 const [cx,cy,cz,rx,rz]=torso[i].map((a,k)=>mix(a,torso[i+1][k],f)),a=u*tau,x=rx*sin(a),fr=Math.max(0,cos(a));
 const ax=Math.abs(x);
 const pec=.018*gauss(ax-.089,.100)*gauss(cy-(1.311-.069*ax),.066);
 const stern=.017*gauss(x,.030)*gauss(cy-1.24,.14);
 const ribs=.012*gauss(ax-.119,.058)*(gauss(cy-(1.163+.23*ax),.021)+gauss(cy-(1.106+.20*ax),.024));
 const abs=.011*gauss(ax-.047,.037)*(gauss(cy-1.16,.032)+gauss(cy-1.09,.032)+gauss(cy-1.038,.026));
 const rolled=.0050*gauss(ax-(.040+.80*(cy-1.225)),.012)*gauss(cy-1.298,.065);
 const vee=.042*Math.pow(fr,8)*(1-v)**5;
 return[cx+x,cy-vee,cz+rz*cos(a)+(pec+stern+ribs+abs+rolled)*fr*fr];}
grid('steel',52,27,(u,v)=>torsoAt((u+.5)%1,v),{closed:true});
for(const v of [.015,.029])path(v<.02?'edge':'iron',Array.from({length:65},(_,i)=>torsoAt(i/64,v)),.003,7);
// A separate lipped plackart locks the lower defense under the cuirass; its shaped rim
// follows the breastplate rather than being drawn as a flat chest decal.
const lowerPlate=(u,v)=>{const a=tau*u,front=Math.max(0,cos(a));
 const top=.219+.062*front**2+.107*front**16;
 const p=torsoAt(u,mix(.027,top,v)),bump=.0038+.0038*sin(v*Math.PI);
 return[p[0]+bump*sin(a),p[1],p[2]+bump*cos(a)];};
grid('iron',56,10,(u,v)=>lowerPlate((u+.5)%1,v),{closed:true});path('edge',Array.from({length:61},(_,i)=>lowerPlate(i/60,1)),.0027);
// Upper pectoral crease, subtle pressed edge below each muscle.
// Offset belt and rounded buckle.
loft('leather',[[0,.936,0,.161,.124],[0,.945,0,.169,.13],[0,.984,0,.167,.129],[0,.992,0,.16,.122]],32,
 (p,t)=>[p[0],p[1]+.013*sin(t),p[2]]);
const buckle=[[-.054,.979],[-.044,.988],[.049,.98],[.054,.971],[.044,.936],[-.044,.943],[-.054,.953]];
faceplate('gold',buckle,.134,.011,.002);
faceplate('oxhide',buckle.map(([x,y])=>[x*.77,.963+(y-.963)*.62]),.138,.006);
path('gold',[[-.029,.964,.144],[.007,.962,.147],[.039,.958,.144]],.0033);
for(let i=0;i<3;i++)pin('gold',.077+i*.027,.966+i*.003,.121-i*.013,.0044);
// Pleated heavy cloak, front lapels over the shoulders and two embossed gold medallions.
const cloakAt=(u,v,inset=0)=>{const a=mix(.925,2*Math.PI-.925,u);
 const flare=1-v,w=.61+.47*sin(v*Math.PI*.85);
 const x=(.215+.19*flare**1.6)*sin(a)*w/.90;
 const folds=(.024*cos(7*a+1.15*v)+.007*cos(16*a-1.7*v))*(.12+.88*flare);
 const z=(-.075-.156*flare+.107*cos(a))+(folds+inset)*cos(a);
 const y=.36+1.02*v+(.032*cos(7*a+.3)-.088*sin(a)+.024*sin(a*2))*flare**2+.032*cos(a)*v;
 return[x+.056*flare**1.7,y,z];};
grid('cloak',42,16,(u,v)=>cloakAt(u,v));grid('fold',42,16,(u,v)=>cloakAt(u,v,-.003),{reverse:true});
for(const u of [0,1])path('fold',Array.from({length:31},(_,i)=>cloakAt(u,i/30)),.005,8);
for(const s of [-1,1]){
 grid('cloak',24,27,(u,v)=>{
  const w=.044+.085*sin(Math.PI*v)**.75;
  const x=s*(.138+.041*sin(Math.PI*v)+(u-.50)*w);
  const z=.134-.264*v+.004*cos(tau*u)*sin(Math.PI*v);
  const y=1.342+.091*sin(Math.PI*v)**.80-.044*u*sin(Math.PI*v)+.005*cos(3*Math.PI*u+v)*sin(Math.PI*v);
  return[x,y,z];
 },{reverse:s<0});
 const x=s*.136,y=1.349+(s<0?.004:-.002),z=.138;
 disc('iron',x,y,z,.035,.008);disc('gold',x,y,z+.005,.031);disc('welt',x,y,z+.007,.024);disc('gold',x,y,z+.008,.0205);
 for(let i=0;i<9;i++){const a=i*tau/9,dx=.013*cos(a),dy=.013*sin(a);path('welt',[[x+.48*dx,y+.48*dy,z+.009],[x+dx,y+dy,z+.010],[x+1.32*dx-.2*dy,y+1.32*dy+.2*dx,z+.01]],.00125,5);}
 pin('gold',x,y,z+.011,.0055);
}
loft('fold',[[0,1.39,-.018,.116,.092],[0,1.422,-.018,.104,.087],[0,1.450,-.022,.084,.074]],26);
const collar=(u,v)=>{const a=tau*u,front=(1+cos(a))/2,r=mix(.153,.088,v),d=mix(.115,.080,v);
 return[r*sin(a),1.397+.002*cos(a)+v*(.025+.067*(1-front)),d*cos(a)-.014];};
grid('iron',38,8,collar,{closed:true});path('edge',Array.from({length:40},(_,i)=>collar(i/39,1)),.0026);
// The raised, angled gorget bottom lip folds under the cloak clasps and frames the neck.
const gorget=(u,v)=>{const a=(u-.5)*tau,front=(1+cos(a))/2;
 const w=mix(.146,.078,v),d=mix(.123,.077,v),drop=.040*(1-v)*Math.max(0,cos(a))**3;
 return [.002+w*sin(a),1.407+v*(.050+.027*(1-front))-drop,-.010+d*cos(a)];};
grid('steel',44,6,gorget,{closed:true});
path('edge',Array.from({length:45},(_,i)=>gorget(i/44,.10)),.0020,7);
// Asymmetric folds drape between the shoulder fastening and the rear of the cloak.
for(const s of [-1,1]){
 const flap=(u,v)=>{const width=.058+.048*Math.sin(Math.PI*v),outer=.157+.044*Math.sin(Math.PI*v);
   const x=s*(outer+(u-.5)*width);
   const y=1.344+.065*Math.sin(Math.PI*v)**.80 + .006*(1-u)*Math.sin(Math.PI*v)
     -.021*u+.004*Math.sin(2.6*Math.PI*u+.7*v)*Math.sin(Math.PI*v);
   const z=.130-.286*v-.010*u*u+.008*Math.sin(4*u+v)*Math.sin(Math.PI*v);
   return[x,y,z];};
 grid('cloak',17,19,flap,{reverse:s<0});path('fold',Array.from({length:20},(_,i)=>flap(1,i/19)),.0045,7);
}
loft('skin',[[.004,1.430,-.009,.069,.065],[.004,1.485,-.006,.067,.067],[.004,1.525,.003,.071,.072]],24);
// Sidearm: scabbard and independent hanger fittings, posed clear of the free hand.
const sheath=[[-.158,.954,.025,.020,.016],[-.187,.839,.008,.024,.018],[-.233,.664,-.031,.023,.016],[-.279,.485,-.065,.019,.015],[-.302,.401,-.08,.006,.008]];
sweep('leather',sheath,11);sweep('edge',[[-.276,.5,-.062,.020,.017],[-.289,.447,-.075,.019,.016],[-.303,.393,-.082,.005,.005]],11);
sweep('gold',[[-.165,.922,.018,.024,.021],[-.158,.95,.026,.024,.021]],12);
sweep('iron',[[-.16,.951,.025,.016,.018],[-.144,1.008,.039,.016,.017],[-.13,1.069,.044,.014,.016]],12);
sweep('gold',[[-.202,.962,.022,.006],[-.181,.961,.024,.011],[-.155,.959,.027,.010],[-.123,.969,.029,.009],[-.105,.978,.031,.004]],10);
pin('gold',-.125,1.079,.049,.020);
// Armored shoulders, nested plates, bent articulated forearms and fingers.
for(const s of [-1,1]){
 use(s<0?left:right);const A=s<0?[-.228,1.35,-.004]:[.240,1.341,-.024],
 E=s<0?[-.324,1.136,.057]:[.310,1.117,.009],
 W=s<0?[-.207,1.202,.190]:[.323,1.031,.077];
 sweep('fold',[[...A,.078,.085],[...add(A,mul(sub(E,A),.36)),.076,.080],[...E,.063,.067]],22);
 // Each overlapping lame follows the upper arm instead of forming a stack of rings.
 for(let j=2;j>=0;j--){
  const cap=(u,v)=>{const a=mix(-1.76,1.80,u),bulge=sin(Math.PI*v);
   const x=.022+j*.014+(.112-j*.009+.008*bulge-.021*v)*cos(a);
   const y=-.025-j*.031+.052*v-.025*cos(a)+.017*sin(a)**2-.009*(1-v)*cos(2*a);
   return[A[0]+s*x,A[1]+y,A[2]+(.116-j*.006+.006*bulge)*sin(a)];};
  grid(j===1?'iron':'steel',24,5,cap,{reverse:s>0});
  path(j===1?'iron':'edge',Array.from({length:22},(_,i)=>cap(i/21,0)),.0034,7);
  for(const u of [.20,.79]){const p=cap(u,.47);pin('gold',...p,.0045);}
 }
 const xx=A[0]+s*.012;
 loft('steel',[[xx,1.329,-.016,.120,.112],[xx,1.344,-.016,.126,.118],
  [xx-s*.007,1.374,-.018,.117,.106],[xx-s*.020,1.410,-.023,.088,.081],
  [xx-s*.041,1.429,-.029,.051,.052],[xx-s*.060,1.432,-.031,.024,.031]],32,
  (p,t)=>[p[0],p[1]-.033*s*sin(t)+.006*cos(2*t),p[2]]);
 const rim=[];for(let j=0;j<=34;j++){const a=tau*j/34;rim.push([xx+.120*sin(a),1.33-.033*s*sin(a)+.006*cos(2*a),-.016+.112*cos(a)]);}path('edge',rim,.0032,7);
 for(const t of [.52,.74]){const p=add(A,mul(sub(E,A),t));sweep('oxhide',[[...p,.071,.07],[...add(p,mul(sub(E,A),.05)),.07,.069]],18);}
 egg('iron',...E,.069,.062,.071,22,12);
 const fore=sub(W,E),L=t=>add(E,mul(fore,t));
 sweep('leather',[[...L(.02),.059,.063],[...L(.26),.068,.065],[...L(.75),.050,.054],[...W,.042,.046]],22);
 sweep('steel',[[...L(.09),.062,.066],[...L(.16),.072,.073],[...L(.28),.069,.071],[...L(.81),.048,.055],[...L(.95),.046,.05]],24);
 for(const t of [.14,.83])sweep('edge',[[...L(t),t<.3?.073:.049,t<.3?.074:.056],[...L(t+.025),t<.3?.073:.049,t<.3?.074:.055]],20);
 require('./hands.cjs').buildHand(s,W);
}
// Convex shield with dished steel rim, embossed heraldic sun above the city arch.
use(right);
const sx=.385,sy=1.018,sbase=.167;const so=[[-.125,.228],[-.067,.239],[0,.251],[.067,.239],[.125,.228],[.145,.155],[.141,.022],[.121,-.086],[.070,-.174],[0,-.251],[-.070,-.174],[-.121,-.086],[-.141,.022],[-.145,.155]];
const shieldZ=(x,y)=>sbase+.043*(1-((x-sx)/.148)**2)-.022*((y-sy)/.26)**2;
function shield(u,v,scale,lift){
 const a=u*so.length,i=Math.floor(a)%so.length,f=a-Math.floor(a),p=so[i],q=so[(i+1)%so.length];
 const x=sx+mix(p[0],q[0],f)*scale*v,y=sy+mix(p[1],q[1],f)*scale*v;
 return[x,y,shieldZ(x,y)+lift+.012*(1-v*v)];
}
grid('oxhide',56,16,(u,v)=>shield(u,.0001+.9999*v,1,-.027),{closed:true,reverse:true});
grid('oxhide',56,4,(u,v)=>shield(u,1,1,mix(-.027,.006,v)),{closed:true,reverse:true});
grid('edge',56,5,(u,v)=>shield(u,mix(.944,1,v),1,.005+.004*sin(Math.PI*v)),{closed:true});
grid('escutcheon',56,14,(u,v)=>shield(u,.0001+.9999*v,.945,.004),{closed:true});
// Two riveted grip brackets meet the hand behind the panel. The forearm loop, lower
// buckle and turned rim also have depth for a player walking past the guard.
const gripAt=x=>shieldZ(x,sy)-.025;
for(const yy of [.951,1.080]){
 sweep('iron',[[.355,yy,gripAt(.355),.013,.013],[.355,yy,.151,.012,.012],
   [.355,yy,.131,.011,.011]],12);
}
sweep('oxhide',[[.355,.949,.129,.012,.012],[.355,.959,.129,.015,.015],
  [.355,1.067,.129,.016,.016],[.355,1.081,.129,.012,.012]],15);
grid('leather',18,4,(u,v)=>{const x=mix(.277,.418,u);
 return[x,1.105+(v-.5)*.026+.020*sin(u*Math.PI),gripAt(x)-.093*sin(u*Math.PI)];},{reverse:true});
for(let i=0;i<so.length;i++){const [a,b]=so[i];const x=sx+a*.946,y=sy+b*.946;pin('gold',x,y,shieldZ(x,y)+.014,.0048);}
require('./visage.cjs');
require('./helmet.cjs');
require('./guard-pack.cjs');
