const {head,jaw,use,grid,egg,sweep,tri,mix,clamp,tau} = require('./guard-forms.cjs');

// Each station specifies the width and depth of the cranium at an anatomical landmark.
// The front is a single continuous surface through the brow, nose, midface and chin.
const stations=[
  [1.465,.018,.029,.049], [1.477,.050,.064,.040], [1.494,.073,.082,.026],
  [1.513,.087,.090,.012], [1.544,.097,.090,.007], [1.571,.102,.097,.003],
  [1.599,.110,.101,-.001], [1.627,.114,.103,-.005], [1.652,.111,.103,-.004],
  [1.684,.111,.108,-.005], [1.724,.107,.108,-.007], [1.758,.100,.101,-.009],
  [1.782,.079,.078,-.010], [1.804,.028,.043,-.014]
];
const gauss=(x,y)=>Math.exp(-x*x-y*y);
const smooth=t=>(t=clamp(t),t*t*(3-2*t));
function section(y){
  let i=1;while(i<stations.length-1 && y>stations[i][0])i++;
  const lo=stations[i-1],hi=stations[i],prev=stations[Math.max(0,i-2)],next=stations[Math.min(stations.length-1,i+1)];
  const len=hi[0]-lo[0],t=clamp((y-lo[0])/len),s=t*t;
  return [1,2,3].map(k=>(2*t*s-3*s+1)*lo[k]+(t*s-2*s+t)*len*(hi[k]-prev[k])/(hi[0]-prev[0])
      +(-2*t*s+3*s)*hi[k]+(t*s-s)*len*(next[k]-lo[k])/(next[0]-lo[0]));
}
function surface(x,y){
  const [w,d,z]=section(y),a=Math.abs(x);
  const f=clamp(a/w),front=Math.sqrt(Math.max(0,1-f**2.45));
  let h=0;
  h+=.012*gauss((a-.067)/.036,(y-(1.611+.06*a))/.024); // zygomatic arch
  h-=.007*gauss((a-.075)/.025,(y-1.564)/.028);          // buccal hollow
  h+=.012*gauss((a-.043)/.034,(y-(1.657+.045*a))/.012);
  h-=.014*gauss((a-.046)/.024,(y-1.638)/.011);
  h+=.008*gauss((a-.048)/.025,(y-1.621)/.009);
  h+=.006*gauss(x/.051,(y-1.559)/.023);
  h+=.009*gauss(x/.046,(y-1.497)/.019);                 // chin boss
  h-=.005*gauss(x/.033,(y-1.531)/.005);
  // Dorsum, tip, wings and philtrum are molded into the same normals as the cheeks.
  h+=.022*gauss(x/.016,(y-1.619)/.035);
  h+=.027*gauss(x/.0175,(y-1.596)/.013);
  h+=.018*gauss((a-.017)/.010,(y-1.589)/.008);
  h-=.005*gauss((a-.020)/.007,(y-1.584)/.004);
  h+=.004*gauss((a-.0045)/.004,(y-1.570)/.009);
  h-=.005*gauss(x/.030,(y-1.548)/.0047);
  return z+d*front+h*(1-smooth((f-.72)/.27));
}
use(head);
const y0=stations[0][0], y1=stations.at(-1)[0];
grid('portrait',80,67,(u,v)=>{
  const angle=(u-.5)*tau,a=angle-.24*Math.sin(angle)*Math.cos(angle),c=Math.cos(a);
  const row=v*67,y=row<=10?mix(y0,1.519,row/10):row<=55?mix(1.519,1.668,(row-10)/45):mix(1.668,y1,(row-55)/12);
  const [w,d,z]=section(y),x=w*Math.sin(a);
  return [.004+x,y,c>=0?surface(x,y):z+d*c];
},{closed:true});

function roundPatch(mat,center,radii,depth,n=30){
  const [x,y]=center,[rx,ry]=radii,origin=[.004+x,y,depth(x,y)];
  for(let i=0;i<n;i++){
    const a=tau*i/n,b=tau*(i+1)/n;
    const end=t=>{const xx=x+rx*Math.cos(t),yy=y+ry*Math.sin(t);return [.004+xx,yy,depth(xx,yy)];};
    const uv=t=>[.5+.495*Math.cos(t),.5-.495*Math.sin(t)];
    tri(mat,origin,end(a),end(b),[.5,.5],uv(a),uv(b));
  }
}
for(const s of [-1,1]){
  const cx=s*.0475,cy=1.638+(s<0?.0002:-.0010),cz=surface(cx,cy)+.001;
  const depth=(x,y)=>cz+.0083-.23*s*(x-cx)-.0039*((x-cx)/.022)**2-.002*((y-cy)/.008)**2;
  const rim=(t,top)=>{
    const x=(t*2-1)*.0213;
    const arc=Math.pow(Math.sin(Math.PI*t),.91);
    return [cx+x,cy+s*x*.061+(top?.0065:-.0045)*arc];
  };
  grid('eye',32,5,(u,v)=>{const [x,y0]=rim(u,false),y=mix(y0,rim(u,true)[1],v);return[.004+x,y,depth(x,y)];});
  const idX=cx-.0011,idY=cy+.00055,irisR=.0069;
  // Clip the iris at the upper and lower lids, including during close profile shots.
  grid('irisPaint',28,12,(u,v)=> {
    const dx=(u*2-1)*irisR,x=idX+dx,t=clamp((x-cx+.0213)/.0426);
    const extent=.0070*Math.sqrt(Math.max(.00001,1-(dx/irisR)**2));
    const low=Math.max(idY-extent,rim(t,false)[1]+.0002),high=Math.min(idY+extent,rim(t,true)[1]-.00025);
    const y=mix(low,high,v);
    return[.004+x,y,depth(x,y)+.0009];
  });
  roundPatch('shade',[idX,idY],[.00285,.0038],(x,y)=>depth(x,y)+.0015);
  roundPatch('eye',[idX-.0017,idY+.0024],[.00124,.00134],(x,y)=>depth(x,y)+.0020,12);
  for(const top of [true,false]){
    grid('portrait',34,6,(u,v)=>{
      const [x,ey]=rim(u,top),ends=Math.sin(Math.PI*u),xx=x+(x-cx)*v*.20;
      const yy=ey+(top?1:-1)*v*(.003+.006*ends);
      const rimZ=depth(x,ey)+.0018,shell=surface(xx,yy);
      return[.004+xx,yy,mix(rimZ,shell+.0004,v)+.0021*ends*Math.sin(v*Math.PI)];
    },{reverse:!top});
    if(top){
      grid('oxhide',29,2,(u,v)=>{
        const [x,y]=rim(u,true);return[.004+x,y+(v-.35)*.0018*Math.sin(Math.PI*u),depth(x,y)+.0024];
      });
    }
  }
  // Broad, irregular eyebrow with a tucked tail; hue is subordinate to the face.
  grid('oxhide',30,5,(u,v)=>{
    const x=s*mix(.020,.077,u),ridge=1.654+.010*Math.sin(Math.PI*u*.88)+.003*u,
      y=ridge+(v-.5)*(.0015+.0058*Math.sin(Math.PI*(u*.85+.06)))*(1-.45*u);
    return[.004+x,y,surface(x,y)+.0017+.0015*Math.sin(Math.PI*v)];
  },{reverse:s<0});
  // A dark crescent under each sculpted nostril. Its depth follows the nose surface.
  grid('cheek',14,4,(u,v)=>{
    const x=s*(.009+.011*u),y=1.585+.0028*Math.sin(Math.PI*u)+v*.0019*Math.sin(Math.PI*u);
    return[.004+x,y,surface(x,y)+.0008];
  },{reverse:s<0});
}
const lipAt=(u,v,upper)=>{
  const t=2*u-1,x=t*.033,w=1-t*t,seam=1.548+.0023*t*t+.0008*t;
  const height=upper?.0035+.0027*Math.exp(-(((Math.abs(t)-.29)/.18)**2)):.0064;
  const y=seam+(upper?1:-1)*height*w*v;
  // Tuck both outer edges into the skin. Keep the inner lip in front of the mouth cavity.
  const base=Math.max(surface(x,y),surface(x,y-.00095))+.0006*w;
  return[.004+x, y, base+w*(.002+.0034*Math.sin(Math.PI*v))*(1-.7*v)];
};
grid('shade',32,4,(u,v)=>{const p=lipAt(u,0,true);p[1]-=(.0012+.0042*v)*(1-(2*u-1)**2);p[2]+=.0002;return p;},{reverse:true});
grid('lip',34,6,(u,v)=>lipAt(u,v,true));
use(jaw);
grid('lip',34,6,(u,v)=>{const p=lipAt(u,v,false);p[1]-=.0009;return p;},{reverse:true});
// Small lower vermilion and mental fold can move while the broader cheek stays intact.
grid('portrait',24,4,(u,v)=>{const p=lipAt(u,1,false),x=p[0]-.004;
  p[1]-=v*.003*(1-(2*u-1)**2);p[2]=surface(x,p[1])+.00055*(1-v);return p;},{reverse:true});
