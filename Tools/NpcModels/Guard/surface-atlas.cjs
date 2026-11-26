// Paint into the same UV atlas as the other equipment, keeping a single draw material.
module.exports = function paintGuardSurfaces({ color, orm, normal, D, noise, value }) {
  const clamp = (n, a = 0, b = 1) => Math.min(b, Math.max(a, n));
  const blend = (a, b, t) => a + (b - a) * clamp(t);
  const gauss = (a, b) => Math.exp(-a*a - b*b);
  const tilt = (u, v, a, b, c, d, width) => {
    const dx = c - a, dy = d - b;
    const t = clamp(((u-a)*dx + (v-b)*dy)/(dx*dx+dy*dy));
    const x = u - (a+t*dx), y = v - (b+t*dy);
    return Math.exp(-(x*x + y*y)/(width*width)) * Math.sin(Math.PI*t)**.5;
  };
  function store(x,y,rgb,rough,metal,hx=0,hy=0) {
    const i = 3*(y*D+x), n = Math.hypot(hx,hy,1);
    for(let k=0;k<3;k++) color[i+k] = Math.round(clamp(rgb[k],0,255));
    orm[i]=255;orm[i+1]=Math.round(255*clamp(rough));orm[i+2]=Math.round(255*clamp(metal));
    normal[i]=Math.round(127.5*(1+hx/n));
    normal[i+1]=Math.round(127.5*(1+hy/n));
    normal[i+2]=Math.round(127.5*(1+1/n));
  }

  // Mapping and landmarks are in model meters, so pigmentation follows the sculpt.
  for(let y=0;y<632;y++) for(let x=512;x<D;x++) {
    const X=((x-518)/498-.5)*.238, a=Math.abs(X), Y=1.813-(y-8)/612*.359;
    const broad=value(x,y,32,121)-.5, fine=value(x,y,6,105)-.5;
    const cheek=gauss((a-.069)/.039,(Y-1.609)/.022);
    const nose=gauss(X/.021,(Y-1.590)/.019);
    const orbital=gauss((a-.048)/.027,(Y-1.635)/.014);
    const under=gauss((a-.052)/.028,(Y-1.621)/.009);
    const temple=gauss((a-.094)/.023,(Y-1.648)/.062);
    const cheekLine=1.548+.055*clamp((a-.028)/.073);
    const beard=clamp((cheekLine-Y+.008)/.025)*clamp((a+.035)/.07)
      *clamp((1.58-Y)/.022)*clamp((Y-1.46)/.018);
    const moustache=gauss((a-.017)/.020,(Y-(1.565+.002*Math.cos(a*78)))/.005)
      *clamp((a-.003)/.012);
    let r=184+11*cheek+9*nose-15*orbital-6*under-7*temple;
    let g=135-3*cheek-6*nose-15*orbital-8*under-6*temple;
    let b=106-1*cheek-4*nose-8*orbital-2*under-3*temple;
    const stubble=Math.max(beard*.75,moustache*.85);
    r=blend(r,85,stubble*.39);g=blend(g,78,stubble*.31);b=blend(b,71,stubble*.24);
    const follicle=noise(Math.floor(x/2),Math.floor(y/3),21);
    const whisker=follicle>.77?stubble*(.24+follicle*.11):0;
    const spots=Math.max(0,(noise(x,y,819)-.90)*8)*cheek*.35;
    // Healed slant on the right cheek and compressions close to the nose.
    const scar=tilt(X,Y,-.085,1.622,-.066,1.597,.0011);
    const crease=tilt(a,Y,.022,1.585,.038,1.554,.0021)*.45
      +tilt(a,Y,.062,1.628,.078,1.621,.0011)*.35
      +tilt(a,Y,.017,1.67,.010,1.660,.0016)*.28;
    const tone=1+.055*broad+.037*fine-.16*crease-.22*whisker-.1*spots;
    store(x,y,[r*tone+15*scar,g*tone+11*scar,b*tone+10*scar],
      .58+.10*beard+.023*broad-.045*nose,0,(noise(x,y,46)-.5)*.050,(noise(x,y,93)-.5)*.050);
  }

  for(let y=636;y<831;y++) for(let x=512;x<D;x++) {
    const u=(x-523)/486, v=(y-644)/170, shift=(value(x,y,21,15)-.5)*.32;
    const shafts=(Math.cos(2*Math.PI*(u*17+1.1*Math.sin(v*3.2)+shift))*.5+.5)**5;
    const wisps=(Math.cos(2*Math.PI*(u*37+1.8*Math.sin(v*5.2)+shift))*.5+.5)**10;
    const lum=25+13*v+12*shafts+7*wisps+11*(value(x,y,37,11)-.5);
    store(x,y,[lum*.80,lum*.92,lum*1.10],.74-.13*shafts,0,shafts*.11-.06,0);
  }
  for(let y=838;y<D;y++) for(let x=512;x<765;x++) {
    const u=((x-528)/222-.5)*2,v=((y-854)/155-.5)*2;
    const r=Math.hypot(u,v),a=Math.atan2(v,u);
    const radial=.5+.5*Math.sin(a*58+Math.sin(r*13+a*7)*1.6);
    const honey=clamp(1-Math.abs(r-.53)*2.7),edge=clamp((r-.68)*3.6);
    const lum=(.71+.23*radial+.14*Math.cos(3*a+r*8))*(1-.64*edge);
    store(x,y,[(101+38*honey)*lum,(91+10*honey)*lum,(60-8*honey)*lum],.33,0);
  }

  // Baked heraldry uses the shield face's own planar unwrap. All lettering-free shapes
  // remain legible after mip filtering; rivets, shield return and holding straps are 3D.
  const shieldAt=(X,Y)=>[9+486*(X-.219)/.336,526+486*(1.283-Y)/.522];
  const sx=.385,sy=1.018;
  for(let y=512;y<D;y++) for(let x=0;x<512;x++) {
    const n=value(x,y,27,53)-.5,grain=value(x,y,6,55)-.5;
    const X=.219+(x-9)/486*.336,SY=1.283-(y-526)/486*.522;
    const center=clamp(1-Math.abs(X-sx)/.156);
    const bend=clamp(1-Math.abs(SY-sy-.16)/.24);
    const age=5*n+3*grain+2*Math.sin(.33*x+.025*y);
    store(x,y,[32+8*center+age,45+9*center+2*bend+age,56+12*center+age],.52+.12*n,.54);
  }
  function paintPoly(points,rgb,rough=.47,metal=.65) {
    const xy=points.map(([x,y])=>shieldAt(sx+x,sy+y));
    const xs=xy.map(p=>p[0]),ys=xy.map(p=>p[1]);
    const inside=(x,y)=>{let yes=false;for(let i=0,j=xy.length-1;i<xy.length;j=i++){
      const [a,b]=xy[i],[c,d]=xy[j];if((b>y)!==(d>y) && x<(c-a)*(y-b)/(d-b)+a)yes=!yes;
    }return yes;};
    for(let y=Math.max(512,Math.floor(Math.min(...ys)));y<=Math.min(D-1,Math.ceil(Math.max(...ys)));y++)
      for(let x=Math.max(0,Math.floor(Math.min(...xs)));x<=Math.min(511,Math.ceil(Math.max(...xs)));x++){
        const cover=[[.25,.25],[.75,.25],[.25,.75],[.75,.75]].reduce((c,[a,b])=>c+Number(inside(x+a,y+b)),0)*.25;
        if(!cover)continue;
        const i=3*(y*D+x),age=.91+.14*value(x,y,13,57);
        store(x,y,rgb.map((n,k)=>blend(color[i+k],n*age,cover)),rough,metal);
      }
  }
  const brass=[204,164,86],gold=[225,193,122],oxidized=[131,103,60],dark=[41,52,59];
  // Triple-towered gate upon a stepped plinth, with an open portcullis and lancet windows.
  const tower=(x,base,w,h)=>[[x-w,base],[x+w,base],[x+w,base+h*.74],[x+w*1.18,base+h*.78],
    [x+w*1.18,base+h],[x+w*.60,base+h],[x+w*.60,base+h*.9],[x+w*.20,base+h*.9],
    [x+w*.20,base+h],[x-w*.28,base+h],[x-w*.28,base+h*.9],[x-w*.66,base+h*.9],
    [x-w*.66,base+h],[x-w*1.18,base+h],[x-w*1.18,base+h*.78],[x-w,base+h*.74]];
  paintPoly([[-.051,-.065],[.051,-.065],[.051,-.054],[.047,-.051],[.047,.028],[-.047,.028],[-.047,-.051],[-.051,-.054]],brass);
  for(const s of [-1,1]){const x=s*.037;paintPoly(tower(x,-.057,.015,.126),gold);
    paintPoly([[x-.004,.019],[x+.004,.019],[x+.004,.036],[x,.045],[x-.004,.036]],dark);}
  paintPoly(tower(0,.004,.023,.098),gold);
  paintPoly([[-.006,.047],[.006,.047],[.006,.061],[0,.071],[-.006,.061]],dark);
  paintPoly([[-.017,-.066],[.017,-.066],[.017,-.029],[.011,-.014],[0,-.005],[-.011,-.014],[-.017,-.029]],dark);
  for(let i=-2;i<=2;i++){const x=i*.005;paintPoly([[x-.0007,-.048+Math.abs(i)*.004],[x+.0007,-.044+Math.abs(i)*.004],[x+.0007,-.022],[x-.0007,-.022]],brass);}
  for(const y of [-.029,-.035]) paintPoly([[-.016,y],[.016,y],[.016,y+.0013],[-.016,y+.0013]],oxidized);
  for(const s of [-1,1]) {
    const stem=[];for(let n=0;n<20;n++){const t=n/19;stem.push([s*(.021+.066*Math.sin(t*1.9)), -.112+.171*t]);}
    const lower=stem.map(([a,b])=>[a+s*.0022,b]);paintPoly([...stem,...lower.reverse()],brass);
    for(let j=1;j<=7;j++){
      const t=j/9,x=s*(.021+.066*Math.sin(t*1.9)),y=-.112+.171*t;
      paintPoly([[x,y],[x+s*.005,y+.010],[x+s*.021,y+.016],[x+s*.016,y+.004]],brass);
      paintPoly([[x,y+.002],[x-s*.009,y+.010],[x-s*.012,y+.029],[x-s*.002,y+.021]],gold);
    }
  }
  const star=[];for(let j=0;j<16;j++){const a=Math.PI*j/8,r=(j%2? .006:j%4?.013:.018);star.push([Math.sin(a)*r,.146+Math.cos(a)*r]);}
  paintPoly(star,gold);
};
