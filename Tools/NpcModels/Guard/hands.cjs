const {tau,clamp,dot,cross,norm,sub,add,mul,mix,grid,sweep,path,egg} = require('./guard-forms.cjs');

function buildHand(side,wrist){
  if(side>0){
    const [x,y,z]=wrist;
    // The closed hand wraps around the shield's vertical leather-wrapped grip.
    sweep('leather',[[x,y,z,.038,.030],[x+.007,y-.020,z+.023,.043,.031],
      [x+.007,y-.050,z+.034,.036,.030],[x+.013,y-.057,z+.038,.022,.020]],19);
    for(let i=0;i<4;i++){
      const yy=y-.013-i*.014,xx=x+.018-.001*i;
      sweep('leather',[[xx,yy,z+.020,.009,.010],[xx+.020,yy-.002,z+.035,.010,.010],
        [xx+.023,yy-.001,z+.053,.009,.008],[xx+.015,yy+.002,z+.061,.007,.008],
        [xx+.006,yy+.003,z+.061,.004,.005]],10);
    }
    sweep('welt',[[x-.013,y-.004,z+.030,.013],[x-.013,y-.006,z+.049,.014],
      [x+.001,y-.015,z+.064,.012],[x+.012,y-.023,z+.063,.007]],13);
    return;
  }
  // Open gauntlet viewed from above and in front. Fingers taper independently and
  // curl towards the palm as the guard beckons the visitor to present a pass.
  const forward=norm([-.83,.018,.52]),initial=norm([.15,.67,.81]);
  const up=norm(sub(initial,mul(forward,dot(initial,forward)))),across=norm(cross(forward,up));
  const p=(x,y,z)=>add(wrist,add(mul(across,x),add(mul(up,y),mul(forward,z))));
  const stations=[
    [0,.032,.021],[.018,.035,.025],[.043,.041,.029],[.065,.042,.025],[.082,.038,.018],[.091,.032,.014]
  ];
  const palm=(u,v)=>{
    const nv=v*(stations.length-1),i=Math.min(stations.length-2,Math.floor(nv)),t=nv-i;
    const [z,w,d]=stations[i].map((v,k)=>mix(v,stations[i+1][k],t));
    const a=tau*u,x=w*Math.sin(a),c=Math.cos(a);
    return p(x,d*c+.003*Math.max(0,c)*Math.sin(v*Math.PI),z+.005*(1-(x/w)**2)*v);
  };
  grid('leather',28,10,palm,{closed:true});
  grid('welt',22,8,(u,v)=>add(palm((u-.5)*.42,.18+.70*v),mul(up,.00075)));
  // A thumb and four fingers share the palm surface at their bases.
  const length=[.049,.067,.078,.068],spread=[-.016,-.007,.003,.009];
  for(let i=0;i<4;i++){
    const center=(i-1.5)*.0178,base=.078+.008*Math.sin((i+.5)*Math.PI/4),L=length[i];
    const positions=[[0,0,-.002,.0093],[.20,.002,.000,.0098],[.47,.007,.002,.0084],
      [.67,.016,.002,.0083],[.88,.027,.001,.0070],[1,.032,.000,.0041]];
    const fingers=positions.map(([t,lift,roll,r])=>[...p(center+spread[i]*t, .005+lift+roll,base+L*t),r*(i===0?.84:1),r*.92]);
    sweep('leather',fingers,11);
    for(const t of [.45,.68]){
      const x=center+spread[i]*t,z=base+L*t,y=.005+(t<.5?.006:.016);
      const seam=[-.0045,0,.0045].map(dx=>p(x+dx,y+.008+.001*Math.cos(dx*200),z+.0008*Math.cos(dx*250)));
      path('welt',seam,.00085,5);
    }
  }
  const thumb=[ [.026,.000,.028,.016],[.038,.004,.041,.016],[.051,.014,.053,.013],
    [.061,.023,.065,.012],[.067,.031,.080,.011],[.066,.033,.089,.0055]];
  sweep('leather',thumb.map(([x,y,z,r])=>[...p(x,y,z),r,r*.88]),14);
  path('oxhide',[p(-.026,.020,.067),p(-.008,.028,.057),p(.014,.028,.057),p(.026,.020,.065)],.0014,6);
  path('oxhide',[p(.027,.022,.041),p(.017,.029,.048),p(.012,.030,.056)],.0013,6);
  for(let i=0;i<3;i++){
    const x=(i-1)*.015;
    sweep('iron',[[...p(x,-.018,.030),.0055],[...p(x,-.023,.050),.0065],
       [...p(x,-.017,.072),.0056],[...p(x,-.016,.079),.0027]],9);
  }
  // Soft leather return under the vambrace.
  sweep('oxhide',[[...p(0,0,-.008),.039,.038],[...p(0,0,.004),.042,.040],[...p(0,0,.016),.038,.035]],16);
}
module.exports={buildHand};
