"""Generate the castle merchant GLB using Python 3, NumPy and Pillow.

Run: python generate_merchant.py --output Merchant.glb
The rigid arm nodes use glTF coordinates: GestureArm (-X) quotes a price,
while OffArm (+X) supports the bound ledger. Both shoulder pivots are stable.
"""

import argparse
from pathlib import Path
from io import BytesIO
import struct, json, math
import numpy as np
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", required=True, type=Path, help="Destination .glb file")
out = parser.parse_args().output
out.parent.mkdir(parents=True, exist_ok=True)
T=384; W=T*4; tau=math.tau; sin=math.sin; cos=math.cos; pi=math.pi
base=[(28,65,94),(40,86,104),(222,205,170),(130,61,43),(102,57,36),(167,109,75),(88,88,82),(217,206,176),(198,156,74),(45,34,34),(201,178,128),(53,128,128),(230,215,178),(59,49,45),(141,78,67),(137,88,50)]
atlas=Image.new("RGB",(W,W)); mr=Image.new("RGB",(W,W)); rng=np.random.default_rng(853)
def pathdraw(draw,pts,color,width=3):
    draw.line([(round(x),round(y)) for x,y in pts],fill=color,width=width,joint="curve")
def sprig(d,x,y,s,flip,ink):
    p=[]
    for t in np.linspace(0,1,30):
        p.append((x+flip*s*(.20*sin(t*pi*1.25)+.35*t),y-s*t))
    pathdraw(d,p,ink,max(1,int(s*.035)))
    for t in [.23,.43,.62,.79]:
        for side in [-1,1]:
            a=np.array(p[int(t*29)])
            dx=side*s*(.16+.10*(1-t)); dy=-s*.23
            leaf=[tuple(a+np.array([dx*v+side*sin(pi*v)*s*.053,dy*v])) for v in np.linspace(0,1,10)]
            leaf += [tuple(a+np.array([dx*v-side*sin(pi*v)*s*.030,dy*v])) for v in np.linspace(1,0,10)]
            d.polygon(leaf,fill=ink)
    end=p[-1]; r=s*.034; d.ellipse((end[0]-r,end[1]-r,end[0]+r,end[1]+r),fill=ink)
for i,c in enumerate(base):
    yy,xx=np.mgrid[0:T,0:T]
    noise=rng.normal(0,2.4,(T,T))+1.7*np.sin(xx*tau/3)+2.0*np.cos(yy*tau/4)
    if i in [2,7]:noise+=4*np.cos(yy*.12+2.4*np.sin(xx*.025))+2.4*np.sin(xx*.08+yy*.2)
    if i in [4,15]: noise+=4*np.sin(xx*.043+1.4*np.sin(yy*.057))+3*np.cos(yy*.07+np.sin(xx*.071))
    if i==5:noise*=.36
    if i==6:noise+=5*np.sin(xx*.48+yy*.033+2*np.sin(.019*yy))+3*np.sin(xx*.25+.1*yy)
    im=Image.fromarray(np.uint8(np.clip(np.array(c)+noise[:,:,None],0,255)))
    d=ImageDraw.Draw(im)
    if i==5:
        # Reserve the last few rows of the skin swatch for warm, translucent nails.
        n=T//8
        nail=np.array([187.,131,105])+np.random.default_rng(3801).normal(0,1.2,(n,T,1))
        nail[:3,:,:]+=np.array([12,14,13])
        im.paste(Image.fromarray(np.uint8(np.clip(nail,0,255))),(0,T-n))
    if i==15:
        # UV-space painted facial planes: warm cheeks, cooler beard shadow and eye sockets.
        u=np.minimum(xx/T,1-xx/T); h=1.476+(1-yy/T)*.292
        def glaze(cx,cy,wx,wy):return np.exp(-((u-cx)/wx)**2-((h-cy)/wy)**2)[:,:,None]
        skin=np.array([174,119,84])+rng.normal(0,1.8,(T,T,1))
        skin+=glaze(.081,1.595,.046,.026)*[19,-2,-5]
        skin+=glaze(.064,1.621,.044,.014)*[-16,-19,-16]
        skin+=glaze(.068,1.650,.045,.013)*[-8,-12,-10]
        skin+=glaze(.037,1.546,.09,.027)*[-17,-17,-11]
        skin+=glaze(.008,1.666,.036,.037)*[7,7,5]
        skin+=glaze(.052,1.620,.040,.006)*[-8,-6,-4]
        skin+=glaze(.115,1.614,.021,.014)*[11,8,6]
        # Short, broken whiskers underneath the sculpted hair continue onto the cheeks.
        a=u*tau
        cheekline=1.532+.086*(a/1.98)**1.28+.008*np.exp(-((a-.58)/.28)**2)
        density=np.clip((cheekline+.016-h)/.018,0,1)*np.clip((2.04-a)/.15,0,1)
        whiskers=np.random.default_rng(1297)
        skin=skin*(1-.87*density[:,:,None])+np.array([93,91,82])*.87*density[:,:,None]
        grain=whiskers.normal(0,8,(T,T))+5*np.cos(20*a+xx*.29+yy*.13)
        skin+=density[:,:,None]*grain[:,:,None]
        # Fine pigment and grey roots lead into the sweep below the nose.
        # Unpack the atlas padding so hairs on each side of the UV seam meet.
        fu=np.clip((xx/T-.008)/.984,0,1)
        fy=1.476+.292*(1-np.clip((yy/T-.008)/.984,0,1))
        angle=np.minimum(fu,1-fu)*tau
        fx=np.abs(.091*np.sin(angle))
        track=np.clip((fx-.002)/.052,0,1)
        hairline=1.568+.002*np.sin(pi*track)-.014*track**1.5
        roots=np.exp(-((fy-hairline)/.0065)**2)*np.clip((.062-fx)/.015,0,1)
        roots*=np.clip((1.2-angle)/.3,0,1)*(.67+.33*np.clip(fx/.007,0,1))
        skin+=(np.array([-64,-35,-14])+grain[:,:,None])*roots[:,:,None]
        im=Image.fromarray(np.uint8(np.clip(skin,0,255)));d=ImageDraw.Draw(im)
    if i in [0,1]:
        for row in range(-1,6):
            for col in range(-2,7):
                x=col*96+(row%2)*48; y=row*96
                col1=(149,126,77) if i==0 else (218,182,113)
                col2=(76,113,123) if i==0 else (144,164,140)
                sprig(d,x,y,69,1,col1);sprig(d,x+42,y-18,44,-1,col2)
                for side in [-1,1]:
                    pts=[(x+side*24*sin(t*pi), y-4-73*t) for t in np.linspace(0,1,24)]
                    pathdraw(d,pts,col2,2)
        if i==1:
            im=Image.new("RGB",(T,T),(33,72,91));d=ImageDraw.Draw(im)
            for y in range(-100,T+100,96):
                for s in [-1,1]:
                    pathdraw(d,[(192+s*131*sin(pi*t),y+94*t) for t in np.linspace(0,1,45)],(213,169,91),3)
                    pathdraw(d,[(192+s*95*sin(pi*t),y+94*t) for t in np.linspace(0,1,45)],(123,156,145),2)
                    for k in range(4):
                        x=192+s*(38+19*k);cy=y+24+10*k
                        sprig(d,x,cy+29,41,-s,(207,173,110))
                for s in [-1,1]:
                    for k in range(4):d.ellipse((180+k*3,y+41+k*3,204-k*3,y+65-k*3),outline=(206,169,100),width=2)
            for x in [6,12,20,T-20,T-12,T-6]:d.line((x,0,x,T),fill=(225,184,102),width=3)
    if i==3:
        for y in range(-40,T+40,28):
            pathdraw(d,[(x, y+5*sin(x*pi/16)) for x in range(T)],(204,143,73),3)
    if i==4:
        for y in [12,T-13]:
            for x in range(0,T,18):pathdraw(d,[(x,y),(x+10,y-1)],(173,118,71),2)
    if i==10:
        for y in range(3,T,6):d.line((0,y,T,y),fill=(160,138,102),width=1)
    if i==6:
        # Fine salts and silvery strands follow the tapered locks along the vertical UV.
        for _ in range(760):
            x,y=rng.integers(-12,T+1,size=2);l=rng.integers(10,52);k=int(rng.integers(80,131))
            pathdraw(d,[(x+2.1*sin(q*1.8+y*.06),y+l*q) for q in np.linspace(0,1,10)],(k+4,k+2,k),1)
    if i==7:
        for y in range(12,T,36):
            pathdraw(d,[(x,y+1.7*sin(x*.024)) for x in range(0,T,3)],(197,184,153),1)
        # Paired selvedge lines are woven into the cloth, with stitches only near the edge.
        for y in [24,34,T-35,T-25]:
            d.line((0,y,T,y),fill=(183,166,135))
        for x in range(-4,T,14):
            pathdraw(d,[(x,T-29),(x+7,T-31)],(239,225,191),2)
    off=(i%4*T,i//4*T);atlas.paste(im,off)
    rough=[211,184,229,198,166,178,218,226,104,174,231,91,142,205,178,182][i]
    metal=190 if i==8 else 0
    mr.paste(Image.new("RGB",(T,T),(255,rough,metal)),off)

names=["Figure","Legs","Chest","Head","Jaw","GestureArm","OffArm"]
orig=np.array([[0,0,0],[0,0,0],[0,1.00,0],[0,1.47,.005],[0,1.535,.068],[-.20,1.355,0],[.20,1.355,0]],float)
parent=[None,0,0,2,3,2,2]; g=2
groups={k:[[],[],[],[]] for k in range(1,7)}
def unit(a):
    a=np.array(a,float);return a/max(1e-11,np.linalg.norm(a))
def surface(tile,fn,nu,nv,reverse=False,uvfn=None):
    a,n,t,ids=groups[g]; first=len(a)
    if nu>=18:nu=max(14,round(nu*.79))
    if nv>=18:nv=max(14,round(nv*.75))
    ys=np.linspace(0,1,nv+1);xs=np.linspace(0,1,nu+1)
    pts=np.array([[fn(u,v) for u in xs] for v in ys])
    du=np.gradient(pts,axis=1);dv=np.gradient(pts,axis=0)
    if np.max(np.abs(pts[:,0]-pts[:,-1])) < .00005:du[:,0]=du[:,-1]=(pts[:,1]-pts[:,-2])/2
    norms=np.cross(du,dv);length=np.linalg.norm(norms,axis=2,keepdims=True)
    for row,col in np.argwhere(length[:,:,0]<1e-12):
        near=np.argwhere(length[:,:,0]>=1e-12)
        if len(near):
            r,c=near[np.argmin(np.sum((near-[row,col])**2,axis=1))]
            norms[row,col]=norms[r,c]
        else:norms[row,col]=[0,1,0]
    norms/=np.maximum(np.linalg.norm(norms,axis=2,keepdims=True),1e-15)
    if reverse:norms*=-1
    a.extend((pts-orig[g]).reshape(-1,3));n.extend(norms.reshape(-1,3))
    for v in ys:
        for u in xs:
            u1,v1=(u,1-v) if uvfn is None else uvfn(u,v)
            cell=5 if tile==16 else tile
            if tile==16:v1=.878+.114*v1
            elif tile==5:v1*=.86
            t.append(((cell%4+.008+.984*u1)/4,(cell//4+.008+.984*v1)/4))
    for j in range(nv):
        for i in range(nu):
            q=first+j*(nu+1)+i;b=q+1;c=b+nu+1;d=q+nu+1
            ids.extend([q,c,b,q,d,c] if reverse else [q,b,c,q,c,d])
def ring(tile,levels,fold=0,waves=10,start=0,end=tau,hem=0,nu=48,nv=20,twirl=0,part=None):
    L=np.array([list(v)+[0]*(5-len(v)) for v in levels],float);lo,hi=L[0,0],L[-1,0]
    def fn(u,v):
        y=lo+(hi-lo)*v;rx,rz,x,z=[np.interp(y,L[:,0],L[:,k]) for k in range(1,5)]
        aa,bb=(start,end) if part is None else part(v)
        ang=aa+(bb-aa)*u
        amplitude=fold(v) if callable(fold) else fold
        fl=amplitude*(.67*sin(waves*ang+twirl*v)+.28*sin((waves+3)*ang-1.8*v)+.2*sin(3*ang+v))
        return [x+(rx+fl)*sin(ang),y+(1-v)**4*hem*(.6*cos(6*ang)+.3*sin(3*ang)),z+(rz+fl)*cos(ang)]
    surface(tile,fn,nu,nv)
    return fn
def ell(tile,ctr,r,nu=22,nv=12,angle=0):
    def fn(u,v):
        a=u*tau; b=(v-.5)*(pi-.0008); x=r[0]*sin(a)*cos(b);y=r[1]*sin(b);z=r[2]*cos(a)*cos(b)
        return [ctr[0]+cos(angle)*x-sin(angle)*y,ctr[1]+sin(angle)*x+cos(angle)*y,ctr[2]+z]
    surface(tile,fn,nu,nv)
def pipe(tile,pts,rads,nu=12):
    pts=np.array(pts,float);rads=np.asarray(rads if isinstance(rads,(list,tuple,np.ndarray)) else [rads]*len(pts))
    if rads.ndim==1:rads=np.stack([rads,rads],axis=1)
    sections=[]
    for i,p in enumerate(pts):
        axis=unit(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)])
        e=unit(np.cross([0,0,1],axis)) if abs(axis[2])<.93 else unit(np.cross([0,1,0],axis))
        if i and np.dot(e,sections[-1][1])<0:e=-e
        f=unit(np.cross(axis,e)); sections.append((p,e,f))
    def fn(u,v):
        j=min(len(pts)-1,round(v*(len(pts)-1)));p,e,f=sections[j]
        return p+rads[j,0]*cos(tau*u)*e+rads[j,1]*sin(tau*u)*f
    surface(tile,fn,nu,len(pts)-1)
def curved(tile,pts,rads,nu=9):
    # Cubic interpolation keeps stitched lines, fingers and curls rounded at the joints.
    pts=np.array(pts,float);rads=[rads]*len(pts) if isinstance(rads,(float,int)) else rads
    vv=[];rr=[]
    for j in range(len(pts)-1):
        a,b,c,d=pts[max(0,j-1)],pts[j],pts[j+1],pts[min(j+2,len(pts)-1)]
        for t in [0,.25,.5,.75]:
            vv.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t**3))
            rr.append(rads[j]+(rads[j+1]-rads[j])*t)
    pipe(tile,vv+[pts[-1]],rr+[rads[-1]],nu)
def band(tile,y,rx,rz,h,x=0,z=0,tilt=0):
    surface(tile,lambda u,v:[x+rx*sin(tau*u),y+(v-.5)*h+tilt*sin(tau*u),z+rz*cos(tau*u)],48,3)
def disk(tile,c,r,w):
    ell(tile,c,(r,r,w),20,10)
def cub(tile,ctr,size,ang=0):
    def cv(p):
        x,y,z=np.array(p)*np.array(size)*.5
        return np.array(ctr)+[x*cos(ang)-y*sin(ang),x*sin(ang)+y*cos(ang),z]
    for ori in [([0,0,1],[1,0,0],[0,1,0]),([0,0,-1],[-1,0,0],[0,1,0]),([1,0,0],[0,0,-1],[0,1,0]),([-1,0,0],[0,0,1],[0,1,0]),([0,1,0],[1,0,0],[0,0,-1]),([0,-1,0],[1,0,0],[0,0,1])]:
        p,e,f=map(np.array,ori);surface(tile,lambda u,v:cv(p+(2*u-1)*e+(2*v-1)*f),1,1)

# The boot shape has a low toe box, raised instep and separate welt.
g=1
for side in [-1,1]:
    x=side*.105;z=.018*side
    ring(4,[(0,.066,.097,x,z+.059),(.014,.078,.131,x,z+.079),(.034,.08,.135,x,z+.079),(.056,.077,.133,x,z+.072),(.075,.074,.116,x,z+.059),(.107,.068,.091,x,z+.029),(.144,.063,.066,x,z),(.27,.071,.071,x,z),(.30,.079,.072,x,z)],.0016,12,nu=32)
    band(4,.029,.08,.135,.012,x,z+.079)
    for h in [.125,.146]:band(4,h,.067,.071,.012,x,z)
    pipe(9,[(x,.25,z),(x,.57,z-.015),(x,.95,0)],[.066,.083,.084],18)

# Continuous under-robe + wrapped crossover bodice + split, independently draped coat.
g=2
levels=[(.145,.266,.184,0,.006),(.24,.258,.177),(.48,.229,.162),(.73,.202,.146),(.94,.185,.144),(1.08,.171,.142),(1.23,.221,.158),(1.354,.253,.148),(1.405,.17,.114),(1.45,.085,.081)]
under=ring(2,levels,.012,13,hem=.012,nu=78,nv=40,twirl=2.6)
over=[(.193,.313,.224,-.010,-.021),(.32,.297,.208,-.008,-.016),(.59,.245,.185,-.006,-.010),(.84,.212,.162),(.99,.185,.151),(1.05,.183,.151),(1.22,.234,.169),(1.35,.261,.158),(1.406,.204,.135),(1.448,.085,.09)]

# The back gathers into heavier folds below the girdle. Across the instep the
# two open fronts diverge towards the hem, exposing the lighter under-robe.
def openangle(v):return .30+.39*(1-v)**1.7+.12*abs(sin(pi*v*1.2))
coat=ring(0,over,lambda v:.005+.022*max(0,1-v/.65)**1.2,9,hem=.023,
          nu=100,nv=48,twirl=2.9,part=lambda v:(openangle(v)+.045*(1-v),tau-openangle(v)))
for v in [.022,.043]:
    surface(1,lambda u,h: np.array(coat(u,v+(h-.5)*.008))+
            .0018*unit(np.array(coat(u,v))*[1,0,1]),80,3)
for side in [0,1]:
    # broad woven facing and a double gold cord along the opening
    def lapel(u,v):
        q=(u*.039) if not side else (1-u*.039)
        p=np.array(coat(q,v));p[2]+=.002
        return p
    surface(1,lapel,9,44,reverse=bool(side))
    for q in ([.002,.04] if not side else [.998,.96]):
        pp=[np.array(coat(q,t))+[0,0,.003] for t in np.linspace(0,1,48)]
        pipe(8,pp,.0021,6)
    # continuous shoulder mantle widening into a long patterned stole
    s=1 if not side else -1
    shoulder=[(1.44,.083,.048,.07),(1.398,.112,.078,.126),(1.34,.140,.098,.143),(1.25,.146,.068,.153),(1.12,.132,.045,.143),(.97,.131,.040,.151),(.79,.151,.046,.146),(.65,.158,.05,.154)]
    sl=np.array(shoulder[::-1])
    def stole(u,v):
        y=sl[0,0]+v*(sl[-1,0]-sl[0,0]);x,w,z=[np.interp(y,sl[:,0],sl[:,k]) for k in [1,2,3]]
        f=(u-.5)*2; return [s*(x+f*w),y-.008*(1-v)**5*sin(pi*(u+.1)),z-.054*f*f+.005*sin(v*9)+.008*(1-f*f)]
    surface(1,stole,16,30,reverse=bool(side))
    for u in [.025,.975]:
        pp=[np.array(stole(u,t))+[0,0,.001] for t in np.linspace(0,1,40)];pipe(8,pp,.0025,6)
    for u in np.linspace(.1,.9,9):
        p=stole(u,0);curved(8,[p,[p[0],p[1]-.010,p[2]],[p[0]+.001,p[1]-.016,p[2]+.001]],[.0014,.0015,.0004],5)

# Inner wrap and layered silk sash cover the waist of both garments.
for side in [-1,1]:
    ss=side
    def fold(u,v):
        x=ss*(.07+.006*u)*(1-v) + ss*(-.077+.076*u)*v
        y=1.439-.35*v-.03*u;z=.094+.07*sin(pi*min(1,.85*v))-.012*u
        return [x,y,z]
    surface(2,fold,8,24,reverse=side>0)
ring(3,[(.943,.201,.161),(.958,.201,.163),(.991,.196,.165),(1.026,.19,.158),(1.054,.188,.156)],.0015,14,nu=56)
band(4,.993,.202,.169,.047,tilt=.009)
for sx in [-1,1]:curved(8,[(sx*.034,.991,.173),(sx*.029,1.01,.177),(0,1.016,.180),(-sx*.029,1.01,.177),(-sx*.034,.991,.173),(0,.968,.18),(sx*.034,.991,.173)],.0034)
disk(11,[0,.992,.183],.013,.006)
for s in [-1,1]:
    for k in range(2):disk(8,[s*(.053+.023*k),.994+s*.004,.166-.009*k],.004,.003)
curved(8,[(.072,.974,.157),(.098,.89,.173),(.129,.853,.183)],.003)
ell(4,[.213,.845,.133],[.081,.095,.046],26,16,angle=.17)
ell(4,[.207,.892,.156],[.077,.041,.034],22,12,angle=.17)
disk(8,[.211,.865,.187],.011,.004)
for s in [-1,1]:
    curved(4,[(.209+.041*s,.90,.146),(.197+.027*s,.95,.125),(.173+.018*s,.988,.102)],.009)
    curved(8,[(.211,.860,.181),(.212+.026*s,.844,.179),(.221+.037*s,.851,.175)],.0018,6)

# Cheekbones, eye sockets, brow pads and bridge are sculpted into a single face surface.
g=3
ring(5,[(1.394,.058,.055,0,.004),(1.444,.065,.061),(1.504,.066,.063),(1.54,.063,.057)],nu=30)
g=2
ring(2,[(1.392,.140,.100),(1.412,.108,.100),(1.44,.088,.084),(1.462,.083,.077),(1.465,.067,.068)],.001,12)
for s in [-1,1]:
    def facing(u,v):
        x=s*(.012+.07*v+.006*sin(pi*v)+(u-.5)*(.032+.018*v))
        y=1.264+.193*v+.012*(u-.5); z=.156-.09*(v**1.55)+.009*sin(pi*v)+.004*cos((u-.5)*pi)
        return [x,y,z]
    surface(2,facing,10,20,reverse=s<0)
    pipe(2,[facing(0,t) for t in np.linspace(0,1,19)],.0018,6)
g=3
headlev=np.array([(1.476,.034,.043,.034),(1.492,.058,.062,.025),(1.516,.074,.074,.018),(1.552,.087,.088,.011),(1.589,.10,.093,.008),(1.617,.106,.098,.007),(1.652,.109,.096,.008),(1.684,.107,.095,.009),(1.718,.101,.087,.006),(1.749,.081,.07,.009),(1.768,.04,.044,.007)])
def sculpt(u,v):
    a=u*tau;y=headlev[0,0]+v*(headlev[-1,0]-headlev[0,0]);rx,rz,cz=[np.interp(y,headlev[:,0],headlev[:,k]) for k in [1,2,3]]
    x=rx*sin(a);front=max(0,cos(a))**2;z=cz+rz*cos(a)
    # The philtrum and upper lip support the moustache as they pass into the cheeks.
    z+=front*.018*math.exp(-(x/.04)**2-((y-1.565)/.019)**2)
    for side in [-1,1]:
        s=side
        for cx,cy,wx,wy,d in [(s*.049,1.63,.027,.014,-.013),(s*.055,1.607,.027,.020,.009),(s*.047,1.652,.036,.012,.013),(s*.044,1.565,.033,.024,-.006)]:
            z+=front*d*math.exp(-((x-cx)/wx)**2-((y-cy)/wy)**2)
    return [x,y,z]
surface(15,sculpt,72,55)
for s in [-1,1]:
    ell(5,[s*.103,1.59,.003],[.021,.038,.015],20,14,angle=-.12*s)
    ell(14,[s*.111,1.593,.015],[.0085,.017,.0030],16,10)
    curved(5,[(s*.110,1.567,.015),(s*.120,1.583,.014),(s*.122,1.604,.013),
        (s*.116,1.618,.012),(s*.105,1.614,.015)],[.0018,.0031,.0034,.0027,.0012],7)
    curved(5,[(s*.109,1.580,.018),(s*.116,1.596,.019),(s*.111,1.603,.018)],[.0012,.0022,.0010],7)
    # Closed almond outline, convex cornea, individually shaped upper & lower eyelids.
    ex=s*.047;ey=1.632+(.0007 if s>0 else 0)
    def eyelid(u,v):
        x=(u-.5)*.050;dy=sin(pi*u)**.85
        y=ey+x*s*.066+dy*((1-v)*-.0057+v*.0073)
        z=.0931-.23*s*x+dy*(.004+.005*sin(pi*v))
        return [ex+x,y,z]
    surface(12,eyelid,18,6)
    # The upper lid has a fleshy fold above a separate fine lash line. The lower lid
    # shades the lower part of the eye rather than tracing a dark outline around it.
    for lid in [0,1]:
        def lidfold(u,v):
            p=np.array(eyelid(u,lid));full=sin(pi*u)**.8
            p[1]+=(1 if lid else -1)*(.0055 if lid else .0035)*v*full
            p[2]+=(.0005+.0030*sin(pi*v)-.0030*v)*full
            return p
        surface(5,lidfold,20,4,reverse=not lid)
    for edge,rad,tile in [(1,.0014,13),(0,.0011,14)]:
        pp=[np.array(eyelid(u,edge))+[0,0,.0007] for u in np.linspace(0,1,20)]
        pipe(tile,pp,[rad*(.35+.65*sin(pi*j/19)) for j in range(20)],7)
    ell(13,[ex,ey+.0004,.1027],[.0068,.0062,.0016],17,9)
    ell(4,[ex,ey+.0004,.1038],[.0058,.0051,.0014],16,8)
    ell(9,[ex,ey+.0006,.1050],[.0035,.0039,.0011],14,8)
    ell(12,[ex-.0019,ey+.0028,.1061],[.0013,.0012,.0006],10,6)
    brow=[(s*.017,1.647,.108),(s*.032,1.653,.108),(s*.051,1.656,.101),(s*.071,1.653,.083),(s*.080,1.649,.072)]
    curved(13,brow,[.002,.0045,.0048,.003,.0007],9)
    for j in range(7):
        t=j/8; x=s*(.022+t*.048);y=1.649+.0065*sin(t*pi);z=.108-.019*t*t
        curved(6,[(x,y,z),(x+s*.006,y+.003,z+.001),(x+s*.01,y+.0035,z-.001)],[.0007,.0008,.0002],5)
    curved(5,[(s*.025,1.619,.101),(s*.046,1.615,.098),(s*.069,1.62,.084)],[.0004,.0015,.0004])
    curved(5,[(s*.019,1.579,.121),(s*.032,1.578,.113),(s*.041,1.561,.103)],[.001,.0022,.0006])
ring(5,[(1.575,.008,.005,0,.122),(1.58,.019,.012,0,.124),(1.589,.018,.017,0,.127),(1.60,.012,.018,0,.122),(1.625,.009,.012,0,.111),(1.655,.008,.005,0,.104)],nu=24,nv=28)
for s in [-1,1]:
    ell(5,[s*.016,1.584,.119],[.012,.008,.011],16,10)
    ell(13,[s*.016,1.580,.128],[.0056,.0024,.0019],13,7)
g=4
# A scalloped upper edge follows the mouth and sideburns. Twisting the ridges as
# they descend produces overlapping waves rather than a row of parallel spikes.
def beard(u,v):
    a=(u-.5)*pi*1.26; side=abs(a)/1.98
    ylo=1.434+.092*side**1.4+.004*cos(9*a-.5)+.003*sin(4*a)
    yhi=1.532+.086*side**1.28+.008*math.exp(-((abs(a)-.58)/.28)**2)
    flow=a+.06*(1-v)**1.6*sin(3*a+1.3*v)+.025*(1-v)
    ridge=(.0039*cos(13*flow+1.4*v)+.0018*sin(23*flow-2*v))*(.45+.55*sin(pi*v))
    x=sin(flow)*(.043+.059*v+.019*sin(pi*v)+ridge)
    z=.035+cos(flow)*(.047+.044*v+.022*sin(pi*v)+ridge)
    y=ylo+(yhi-ylo)*v
    # Bring the perimeter into the actual jaw surface. Sideburns follow the cheek
    # rather than ending in the hard raised rim of a separate beard shell.
    t=np.clip((v-.58)/.42,0,1)
    blend=t*t*(3-2*t)
    skin=np.array(sculpt(a/tau,(y-1.476)/.292))
    sink=.0022*(v**7)
    return [(1-blend)*x+blend*(skin[0]-sink*sin(a)),y,
            (1-blend)*z+blend*(skin[2]-sink*cos(a))]
surface(6,beard,56,23)
for j in range(17):
    u=.06+.055*j
    hi=.63+.20*sin(j*1.8)**2
    def lock(v,offset=0):
        w=u+.023*(1-v)**1.6*sin(j*.8+2.2*v)+offset
        p=np.array(beard(w,v));a=(w-.5)*pi*1.26
        return p+np.array([sin(a)*.0005,0,cos(a)*.0005])
    def ridge(u1,v):
        w=.016*sin(pi*v)**.63+.0009
        p=lock(hi*v,(u1-.5)*2*w)
        ang=(u-.5)*pi*1.26
        return p+np.array([sin(ang),0,cos(ang)])*.0030*sin(pi*u1)*sin(pi*v)
    surface(6,ridge,5,13,uvfn=lambda u1,v:(u+(u1-.5)*.035,1-hi*v))
g=3
# Position the hairs on the same facial surface used to cut the head mesh.
def cheek_point(x,y):
    radius=np.interp(y,headlev[:,0],headlev[:,1])
    a=math.asin(np.clip(x/radius,-1,1))
    return np.array(sculpt(a/tau,(y-1.476)/.292))

for s in [-1,1]:
    # A flattened sweep parts under the nose. Each lock begins at the skin and
    # fans down and out to an uneven tip, revealing the upper lip at the centre.
    def moustache(u,v):
        x=.002+.052*u
        top=1.571+.002*sin(pi*u)-.014*u**1.5
        depth=.001+.016*sin(pi*u)**.60
        p=cheek_point(s*(x+.006*v*sin(pi*u)),top-v*depth)
        p[2]+=(.0007+.0048*sin(pi*v)**.7)*sin(pi*u)**.6
        return p
    surface(6,moustache,24,8,reverse=s==1)
    for strand in range(12):
        along=.025+.078*strand
        def whisker(u,v):
            drift=(.037+.07*sin(along*pi)) * v
            width=(.043+.012*sin(strand*1.71))*(.3+.7*sin(pi*v)**.55)*(1-v**2)+.0008
            w=np.clip(along+drift+(2*u-1)*width,0,.996)
            h=.09+v*(.82+.19*sin(strand*1.67))
            p=moustache(w,min(.997,h))
            p[1]-=.0025*v**2
            p[2]+=.0006+.0023*sin(pi*u)*sin(pi*v)**.65
            return p
        surface(6,whisker,4,8,reverse=s==1,
                uvfn=lambda u,v,j=strand:(.06+j*.071+u*.012,.28+v*.31))
g=4
def soul_patch(u,v):
    x=(u-.5)*(.006+.020*v)
    y=1.527+.011*v-.003*(1-sin(pi*u))*v
    p=cheek_point(x,y)
    p[2]+=.001+.010*v+.002*sin(pi*u)*sin(pi*v)
    return p
surface(6,soul_patch,10,6,uvfn=lambda u,v:(.40+u*.17,.12+(1-v)*.15))
g=3
curved(14,[(-.029,1.549,.105),(-.012,1.551,.113),(0,1.549,.117),(.01,1.551,.114),(.031,1.549,.105)],[.001,.0033,.0025,.0032,.0008])
# The inner mouth stays behind the lip. Lower-lip motion reveals it during speech.
def inner_mouth(u,v):
    t=2*u-1;edge=max(0,1-t*t)
    return [.026*t,1.548-.0058*(1-v)*edge,.105+.0093*edge]
surface(9,inner_mouth,17,4)
g=4
curved(14,[(-.024,1.545,.107),(0,1.542,.114),(.024,1.545,.107)],[.0008,.0031,.0008])
g=3
# Wraps change direction in groups. Each band turns under its neighbour at the
# lower edge and swells along its middle; asymmetric tension pulls toward the knot.
ring(7,[(1.663,.11,.099),(1.69,.120,.103),(1.714,.130,.111),(1.775,.139,.119),(1.82,.126,.108),(1.84,.113,.10),(1.855,.084,.086),(1.861,.047,.054),(1.863,.001,.002)],nu=45)
ell(3,[0,1.848,-.004],[.062,.019,.057],26,10)
for j,(h,rx,cant) in enumerate([(1.681,.116,-.026),(1.704,.129,-.019),
        (1.722,.138,.030),(1.750,.141,.022),(1.779,.139,-.020),(1.807,.129,-.016),(1.830,.113,-.009)]):
    rz=rx*.86
    def wrap(u,v):
        a=u*tau
        taper=.78+.22*cos(a+.32*j)
        ridge=.0088*sin(pi*v)**.8+.0018*sin(3*pi*v+.35*sin(2*a))*sin(pi*v)
        tuck=.0021*sin(2*a+.5*j)*sin(pi*v)
        sideDrape=(.012*(cos(a)-1)+.008*(cos(2*a)-1))*(1-j/8)
        return [(rx+ridge+tuck)*sin(a),h+(v-.5)*.058*taper+cant*sin(a+.13)+.004*cos(2*a+.4*j)+sideDrape,
            (rz+ridge+tuck)*cos(a)+.002]
    surface(7,wrap,72,13)
    pp=[np.array(wrap(u,.18))+[.0008*sin(tau*u),0,.0008*cos(tau*u)] for u in np.linspace(0,1,77)]
    pipe(7,pp,.0011,6)
disk(8,[.012,1.729,.132],.015,.004);disk(11,[.012,1.729,.137],.009,.003)
curved(8,[(.013,1.735,.134),(.025,1.762,.132),(.044,1.791,.123)],[.0023,.004,.0008])
for k in range(2):
    def tail(u,v):
        x=.120+.012*sin(pi*v)+.009*k+(u-.5)*(.032-.006*k)
        y=1.703-v*(.166+.027*k)+.006*(u-.5)*v
        z=-.052-.035*sin(pi*v)-.012*k+.008*sin(tau*u+2*v)
        return [x,y,z]
    surface(7,tail,12,16,reverse=True)
    for u in [.06,.94]:
        pipe(7,[tail(u,t) for t in np.linspace(0,1,18)],.0014,6)

# Elbow bends and generous tapered sleeves; the left-hand pitch presents an open palm.
for s in [-1,1]:
    g=5 if s<0 else 6
    sh=np.array([s*.209,1.352,-.005]); elbow=np.array([s*(.308 if s<0 else .324),1.102,.016])
    wrist=np.array([s*(.365 if s<0 else .268),1.167 if s<0 else 1.082,.168 if s<0 else .225])
    def sleeve_center(t):
        upper=elbow-sh;fore=wrist-elbow
        if t<.39:return sh+upper*(t/.49)
        if t>.66:return elbow+fore*((t-.53)/.47)
        v=(t-.39)/.27
        p0=sh+upper*(.39/.49);p2=elbow+fore*((.66-.53)/.47)
        return (1-v)**2*p0+2*v*(1-v)*elbow+v*v*p2
    def sleeve_point(u,t,extra=0):
        center=sleeve_center(t);axis=unit(sleeve_center(t+.001)-sleeve_center(t-.001))
        e=unit(np.cross([0,0,1],axis));f=unit(np.cross(axis,e));a=tau*u
        r=np.interp(t,[0,.17,.36,.53,.70,.90,1],[.091,.096,.080,.073,.071,.049,.043])
        pleats=(.0018+.0028*sin(pi*t))*(.6*cos(7*a+4*t)+.4*sin(5*a-t))
        # Short tension folds follow the bent elbow; long soft flutes broaden toward the cuff.
        crease=0
        for peak,width in [(.37,.034),(.48,.026),(.58,.022),(.70,.033)]:
            ridge=t-peak-.037*cos(a+.5)-.012*sin(2*a)
            crease+=(.0054*math.exp(-(ridge/width)**2)-.0028*math.exp(-((ridge-.029)/.022)**2))*(.55+.45*cos(a+1.0))
        return center+(r+pleats+crease+extra)*(e*cos(a)+f*sin(a))
    def sleeve_patch(tile,lo,hi,extra):
        surface(tile,lambda u,v:sleeve_point(u,lo+(hi-lo)*v,extra),42,max(4,round((hi-lo)*60)))
    ell(0,sh,[.092,.093,.09],28,15)
    sleeve_patch(2,0,1,0)
    sleeve_patch(0,.012,.30,.006)
    for lo,hi in [(.230,.310),(.786,.941)]:
        sleeve_patch(1,lo,hi,.0070)
        # Woven bands have an outline and one continuous strand of raised herringbone.
        for t in [lo+.005,hi-.005]:
            pipe(8,[sleeve_point(u,t,.0080) for u in np.linspace(0,1,45)],.00165,6)
        pp=[sleeve_point(u,(lo+hi)*.5+(hi-lo)*.29*sin(tau*u*9),.0085) for u in np.linspace(0,1,110)]
        pipe(8,pp,.0013,6)
    sleeve_patch(2,.946,.999,.0022)
    # Inside the cuff, the turned-back linen meets a continuous wrist and palm.
    # The underside must remain filled even when looking straight into the bent sleeve.
    cuff_center=sleeve_center(1)
    toward_hand=unit(wrist-elbow)
    def cuff_facing(u,v):
        edge=sleeve_point(u,1,.0022)
        return cuff_center+(edge-cuff_center)*(1-.52*v)-toward_hand*(.008*v)
    surface(2,cuff_facing,36,5)
    if s<0:
        # Articulated open palm: thumb stands apart, the first two fingers carry the quote.
        joint=sleeve_center(.90)
        curved(5,[joint,wrist,(-.372,1.170,.182),(-.382,1.171,.194)],
            [.029,.026,.025,.025],17)
        ell(5,[-.383,1.172,.193],[.043,.022,.048],24,14,angle=.16)
        for k in range(4):
            x=-.409+.016*k; y=1.173+.002*k;z=.211+.004*k; ln=[.046,.057,.051,.040][k]
            flare=[-.015,-.008,.001,.010][k]
            pts=[(x,y,z),(x-.002+flare*.19,y+.004,z+.015),
                (x-.004+flare*.65,y+.012+.002*k,z+ln*.72),
                (x-.006+flare,y+[.016,.023,.029,.029][k],z+ln)]
            curved(5,pts,[.0101,.0094,.0081,.0051],10)
            ell(5,[pts[-1][0],pts[-1][1]+.001,pts[-1][2]-.002],[.0042,.0020,.0050],10,6)
        curved(5,[(-.352,1.171,.19),(-.344,1.184,.204),(-.337,1.197,.222),(-.345,1.208,.232)],[.015,.012,.01,.006])
        disk(8,[-.403,1.191,.230],.005,.002)
    else:
        # The palm bears against the back board. Four fingers wrap under the foot;
        # the thumb reaches round the spine to brace the front cover.
        bx,by,bz=.306,1.136,.268; ang=-.24
        def bp(x,y,z):
            return np.array([bx+x*cos(ang)-y*sin(ang),by+x*sin(ang)+y*cos(ang),bz+z])
        curved(5,[sleeve_center(.89),wrist,bp(-.031,-.054,-.047),bp(-.014,-.068,-.044)],
            [.029,.025,.022,.024],17)
        ell(5,bp(-.014,-.067,-.043),[.045,.041,.022],26,16,angle=ang)
        ell(5,bp(-.045,-.057,-.042),[.027,.027,.018],22,14,angle=ang-.52)
        cub(10,[bx,by,bz],[.152,.206,.023],ang)
        for zz in [-.015,.015]:cub(4,bp(0,0,zz),[.160,.216,.008],ang)
        # Rounded spine and gold tooling form a legible vertical edge in silhouette.
        pipe(4,[bp(-.077,y,0) for y in np.linspace(-.107,.107,9)],[(.017,.011)]*9,12)
        for k,xx in enumerate([-.062,-.056]):
            pipe(8,[bp(xx,y,.0198) for y in [-.089,.089]],.0011-.0002*k,6)
        for xx in [-.069,.069]:
            for yy in [-.096,.096]:disk(8,bp(xx,yy,.021),.004,.0018)
        path=[bp(x,y,.020) for x,y in [(-.066,-.091),(.066,-.091),(.066,.091),(-.066,.091),(-.066,-.091)]]
        pipe(8,path,.0016,6)
        for yy in [-.076,-.042,.046,.079]:cub(8,bp(-.077,yy,0),[.012,.007,.038],ang)
        pp=[bp(x,y,.020) for x,y in [(0,.034),(.018,.009),(.026,0),(.015,-.012),(0,-.034),(-.018,-.01),(-.026,0),(-.017,.011),(0,.034)]]
        pipe(8,pp,.0024,7);disk(11,bp(0,0,.020),.01,.003)
        cub(4,bp(.065,.006,.021),[.030,.013,.005],ang)
        disk(8,bp(.055,.006,.025),.0035,.0018)
        def book_digit(centres,widths,pads,lateral):
            # Keep the pad facing the board as it wraps an edge. Independent
            # transverse widths and pad depths give the tips their flattened shape.
            pts=np.array(centres); axis=np.gradient(pts,axis=0)
            side=[]; normal=[]
            for tangent in axis:
                tangent=unit(tangent)
                basis=unit(lateral-np.dot(tangent,lateral)*tangent)
                side.append(basis);normal.append(np.cross(tangent,basis))
            def section(u,v):
                t=v*(len(pts)-1);i=min(int(t),len(pts)-2);f=t-i
                def edge(k):
                    return pts[k]+widths[k]*cos(tau*u)*side[k]+pads[k]*sin(tau*u)*normal[k]
                return bp(*((1-f)*edge(i)+f*edge(i+1)))
            surface(5,section,14,2*(len(pts)-1))

        # The bottom of the covers is at -.108 and their outside faces are
        # at ±.019. Wrap the pads around those two corners with a tangent arc.
        for k,(knuckle,length,width) in enumerate([
                (-.046,.028,.0092),(-.024,.031,.0098),(-.003,.021,.0092),(.016,.009,.0072)]):
            depth=.0064-.00034*k
            path=[(knuckle-.002,-.075-.002*k,-.039),(knuckle,-.094,-.030)]
            for a in np.linspace(0,pi/2,5):
                path.append((knuckle,-.108-depth*sin(a),-.019-depth*cos(a)))
            for a in np.linspace(0,pi/2,5):
                path.append((knuckle+.0007*k,-.108-depth*cos(a),.019+depth*sin(a)))
            tip=knuckle+.002*(k-1)
            path.extend([(tip,-.108+length*.52,.019+depth),
                         (tip,-.108+length-.003,.019+depth*.77),
                         (tip,-.108+length,.019+depth*.65)])
            widths=[width*1.04,width]+[width*.93]*5+[width*.88]*5+[width*.85,width*.72,.0003]
            pads=[.010,.008]+[depth]*10+[depth,depth*.77,.0003]
            book_digit(path,widths,pads,np.array([1.,0,0]))
            def fingernail(u,v):
                h=length*(.28+.48*v)
                lat=(2*u-1)*width*.54*(.20+.80*sin(pi*v)**.52)
                x,_,zc=[np.interp(h,[p[1]+.108 for p in path[-4:]],
                                  [p[i] for p in path[-4:]]) for i in range(3)]
                thick=np.interp(h,[0,length*.52,length-.003,length],[depth,depth,depth*.77,.0003])
                span=np.interp(h,[0,length*.52,length-.003,length],
                               [width*.88,width*.85,width*.72,.0003])
                z=zc+thick*math.sqrt(max(0,1-(lat/span)**2))+.00055
                return bp(x+lat,-.108+h,z)
            surface(16,fingernail,6,8)
        # At the spine the leather reaches x=-.094. Wrap outside its widest
        # section, then rotate the thumb pad onto the front above the fingers.
        thumb=[(-.041,-.058,-.043),(-.060,-.052,-.039),(-.078,-.046,-.034),
               (-.089,-.042,-.025),(-.099,-.040,-.014),(-.105,-.039,-.002),
               (-.102,-.037,.010),(-.095,-.034,.020),(-.084,-.031,.027),
               (-.073,-.027,.027),(-.064,-.024,.0255),(-.060,-.024,.024)]
        book_digit(thumb,[.016,.015,.013,.012,.011,.010,.010,.010,.010,.010,.007,.0004],
                   [.014,.013,.011,.010,.009,.009,.008,.008,.007,.008,.0065,.0004],np.array([0.,1,0]))
        def thumbnail(u,v):
            x=-.081+.017*v
            lat=(1-2*u)*(.0056*sin(pi*v)**.52+.001)
            y=np.interp(x,[-.084,-.073,-.064],[-.031,-.027,-.024])
            thick=np.interp(x,[-.084,-.073,-.064],[.007,.008,.0065])
            z=np.interp(x,[-.084,-.073,-.064],[.027,.027,.0255])
            span=np.interp(x,[-.084,-.073,-.064],[.010,.010,.007])
            return bp(x,y+lat,z+thick*math.sqrt(max(0,1-(lat/span)**2))+.0005)
        surface(16,thumbnail,6,9)

# Single portable GLB; disjoint material cells share the UV atlas and ORM texture.
chunks=bytearray();views=[];acs=[];meshes=[];nodes=[]
def store(data,target=None):
    while len(chunks)%4:chunks.append(0)
    idx=len(views);v={"buffer":0,"byteOffset":len(chunks),"byteLength":len(data)}
    if target is not None:v["target"]=target
    views.append(v);chunks.extend(data);return idx
def acc(data,kind,dtype,bounds=False):
    arr=np.array(data,dtype=dtype);typ=5126 if arr.dtype.kind=="f" else 5123 if arr.itemsize==2 else 5125
    ac={"bufferView":store(arr.tobytes(),34962 if kind!="SCALAR" else 34963),"componentType":typ,"count":len(arr),"type":kind}
    if bounds:ac.update(min=arr.min(axis=0).tolist(),max=arr.max(axis=0).tolist())
    acs.append(ac);return len(acs)-1
for ni,name in enumerate(names):
    node={"name":name,"translation":(orig[ni]-(orig[parent[ni]] if parent[ni] is not None else 0)).tolist()}
    kids=[idx for idx,par in enumerate(parent) if par==ni]
    if kids:node["children"]=kids
    if ni:
        pp,nn,uv,ii=groups[ni]
        # Eyelid and fabric tips converge. Preserve UV seams, but omit triangles
        # with zero area after the on-disk float32 position conversion.
        positions=np.asarray(pp,dtype="<f4").astype(float)
        triangles=np.asarray(ii).reshape(-1,3)
        corners=positions[triangles]
        area=np.linalg.norm(np.cross(corners[:,1]-corners[:,0],corners[:,2]-corners[:,0]),axis=1)
        ii=triangles[area>1e-13].ravel()
        ix=acc(ii,"SCALAR","<u2" if len(pp)<65536 else "<u4")
        attributes={"POSITION":acc(pp,"VEC3","<f4",True),"NORMAL":acc(nn,"VEC3","<f4"),"TEXCOORD_0":acc(uv,"VEC2","<f4")}
        node["mesh"]=len(meshes);meshes.append({"name":name+" geometry","primitives":[{"attributes":attributes,"indices":ix,"material":0}]})
    nodes.append(node)
images=[]
for imag,nm in [(atlas,"Traveling merchant · dyed silks, skin and leather"),(mr,"Woven cloth, oiled hide and chased brass · ORM")]:
    buf=BytesIO()
    if imag is atlas:
        imag.save(buf,"JPEG",quality=90,subsampling=0,optimize=True);mime="image/jpeg"
    else:imag.save(buf,"PNG",optimize=True);mime="image/png"
    images.append({"name":nm,"mimeType":mime,"bufferView":store(buf.getvalue())})
j={"asset":{"version":"2.0","generator":"Castle NPC tailored merchant"},"scene":0,"scenes":[{"name":"Merchant","nodes":[0]}],"nodes":nodes,"meshes":meshes,"accessors":acs,"bufferViews":views,"materials":[{"name":"Merchant | brocade, linen, leather and brass","doubleSided":True,"pbrMetallicRoughness":{"baseColorTexture":{"index":0},"baseColorFactor":[1,1,1,1],"metallicRoughnessTexture":{"index":1},"metallicFactor":1,"roughnessFactor":1}}],"textures":[{"source":0,"sampler":0},{"source":1,"sampler":0}],"samplers":[{"magFilter":9729,"minFilter":9987,"wrapS":33071,"wrapT":33071}],"images":images,"buffers":[{"byteLength":len(chunks)}]}
raw=json.dumps(j,separators=(",",":")).encode();raw+=b" "*(-len(raw)%4);chunks.extend(b"\0"*(-len(chunks)%4))
payload=struct.pack("<4sII",b"glTF",2,28+len(raw)+len(chunks))+struct.pack("<I4s",len(raw),b"JSON")+raw+struct.pack("<I4s",len(chunks),b"BIN\0")+chunks
out.write_bytes(payload)
allp=np.concatenate([np.asarray(v[0])+orig[k] for k,v in groups.items()])
triangle_count=sum(acs[m["primitives"][0]["indices"]]["count"]//3 for m in meshes)
print("vertices",len(allp),"triangles",triangle_count,"bounds",allp.min(0),allp.max(0),"bytes",len(payload))
