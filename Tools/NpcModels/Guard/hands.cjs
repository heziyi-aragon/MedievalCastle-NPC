const {tau, dot, cross, norm, sub, add, mul, mix, grid, path} = require('./guard-forms.cjs');

// Cross-sections are perpendicular to the finger's centreline. A fixed lateral
// reference keeps the hide and knuckle ridges continuous as the joints bend.
function gloveSurface(material, rows, lateral, segments = 16) {
  const rings = rows.map((row, i) => {
    const center = row.slice(0, 3);
    const before = rows[Math.max(0, i - 1)].slice(0, 3);
    const after = rows[Math.min(rows.length - 1, i + 1)].slice(0, 3);
    const along = norm(sub(after, before));
    const width = norm(sub(lateral, mul(along, dot(lateral, along))));
    const depth = cross(width, along);
    return {center, width, depth, radius: row[3], thickness: row[4]};
  });
  const rounded = value => Math.sign(value) * Math.pow(Math.abs(value), .86);
  grid(material, segments, rows.length - 1, (u, v) => {
    const ring = rings[Math.round(v * (rings.length - 1))];
    return add(ring.center, add(mul(ring.width, rounded(Math.sin(tau*u))*ring.radius),
      mul(ring.depth, rounded(Math.cos(tau*u))*ring.thickness)));
  }, {closed:true});
}

function openHand(wrist, elbow) {
  const forearm = norm(sub(wrist, elbow));
  // The palm continues out of the gauntlet, slightly below the forearm's rise.
  const forward = norm(add(forearm, [-.055, -.18, .10]));
  // Supinate around the forearm so that when the shoulder raises the hand,
  // the cupped palm faces both upward and outward to the approaching traveler.
  const across = norm(cross(forward, [-.80, .08, .60]));
  const palmUp = cross(across, forward);
  const place = (x, y, z) => add(wrist, add(mul(across,x), add(mul(palmUp,y), mul(forward,z))));
  const station = (x, y, z, w, d) => [...place(x,y,z), w,d];

  // The large aperture of the steel vambrace rolls over a soft cuff. Its
  // section narrows continuously to the two heel pads of the leather palm.
  gloveSurface('leather', [
    station(0,0,-.014,.039,.038), station(0,0,-.009,.042,.040),
    station(.001,0,.003,.038,.032), station(.001,0,.019,.032,.022)
  ], across, 24);
  gloveSurface('oxhide', [
    station(0,0,-.011,.042,.041), station(0,0,-.009,.044,.042),
    station(0,0,-.006,.044,.041)
  ], across, 24);
  // Four adjacent strips are sewn continuously across the hand. Their upper
  // and lower surfaces converge to shared webs, then separate to form the
  // fingers. No buried cylinders or end cap interrupts the palm silhouette.
  const edges = [-1,-.50,-.025,.47,1];
  const palmRows = [
    // Length, half-width, palmar/dorsal thickness, knuckle arch, web blend.
    [.006,.030,.018,.021,0,0], [.017,.032,.022,.019,0,0],
    [.033,.036,.023,.018,.05,0], [.049,.041,.023,.017,.20,0],
    [.062,.043,.020,.016,.40,.08], [.072,.043,.017,.015,.66,.30],
    [.078,.043,.014,.013,.87,.68], [.084,.043,.012,.012,1,1]
  ];
  const gaussian = t => Math.exp(-t*t);
  const knuckleOffset = s => .005*s-.009*s*s;
  // Pinky, ring, middle, index. The arch, spread and flexion are asymmetric.
  const fingers = [
    [.062,-.016,.030,.0076], [.075,-.008,.026,.0088],
    [.084,.004,.020,.0093], [.077,.013,.009,.0090]
  ];
  const sections = [
    // Fraction of length, flexion height, joint radius scale.
    [.09,.003,1.01], [.18,.02,.97], [.30,.055,.89], [.43,.15,.97],
    [.52,.25,.90], [.65,.40,.80], [.73,.54,.86], [.85,.75,.76],
    [.94,.92,.69], [.98,.98,.50], [.995,.997,.25], [1,1,0]
  ];
  for (let finger=0; finger<4; finger++) {
    const [len,splay,flex,radius] = fingers[finger];
    const left=edges[finger], right=edges[finger+1], middle=(left+right)/2;
    const baseZ=.084+knuckleOffset(middle);
    function surface(u, row, side) {
      const lateral=-Math.cos(Math.PI*u);
      const rim=Math.pow(Math.max(0,Math.sin(Math.PI*u)),.87);
      const s=mix(left,right,(1+lateral)/2);
      if (row < palmRows.length) {
        const [z,w,inside,back,arch,web] = palmRows[row];
        const x=w*s;
        const round=Math.pow(Math.max(0,1-s*s),.40);
        const heel=gaussian((z-.042)/.026);
        const pads=heel*(.006*gaussian((x-.019)/.013)+.0035*gaussian((x+.023)/.016));
        const hollow=.004*gaussian((z-.061)/.027)*gaussian(x/.019);
        const depth=side>0 ? (inside+pads-hollow)*round : back*round;
        const y=side*mix(depth,radius*.91*rim,web);
        return place(x,y,z+knuckleOffset(s)*arch);
      }
      const i=row-palmRows.length;
      const [t,rise,fat]=sections[i];
      const previous=i ? sections[i-1] : [0,0,1];
      const angle=Math.atan2(flex*(rise-previous[1]),len*(t-previous[0]));
      const rootHalfWidth=.043*(right-left)/2;
      const width=mix(rootHalfWidth,radius,Math.min(1,t/.15))*fat;
      const depth=side*rim*radius*.91*fat;
      const spread=splay*(.4*t+.6*t*t);
      return place(.043*middle+spread+lateral*width,flex*rise+depth*Math.cos(angle),
        baseZ+len*t+(knuckleOffset(s)-knuckleOffset(middle))*Math.max(0,1-t/.22)-depth*Math.sin(angle));
    }
    const intervals = palmRows.length+sections.length-1;
    for(const side of [1,-1]) {
      grid('leather',10,intervals,(u,v)=>surface(u,Math.round(v*intervals),side),{reverse:side<0});
    }
    // Fine welts on the two perimeter seams, following the glove itself.
    if(finger===0 || finger===3) {
      const u=finger===0 ? 0 : 1;
      const seam=palmRows.slice(1).map((_,i)=>surface(u,i+1,1));
      path('welt',seam,.00095,7);
    }
  }

  // The thumb saddle begins inside the thenar pad, filling the web before
  // the thumb opposes the four fingers.
  const thumb = [
    [.014,.008,.026,.020,.011], [.025,.011,.036,.019,.014],
    [.037,.013,.047,.016,.013], [.046,.016,.054,.013,.012],
    [.055,.019,.061,.0105,.0098], [.061,.020,.070,.0108,.0093],
    [.066,.019,.080,.0093,.0084], [.068,.018,.086,.007,.0064],
    [.069,.018,.090,.0005,.0005]
  ];
  gloveSurface('leather', thumb.map(v=>station(...v)),palmUp,16);
}

function shieldHand(wrist, grip) {
  const [gx, gz] = grip;
  // Palm is fanned across the rear quadrant of the vertical handle. Four
  // nested finger arcs run round one side; the thumb closes from the other.
  const palmRows = [
    [...sub(wrist,[.002,-.006,.006]),.041,.035], [...wrist,.038,.031],
    [gx-.031,1.025,gz-.042,.032,.023], [gx-.034,1.018,gz-.031,.034,.020],
    [gx-.032,1.015,gz-.018,.039,.017], [gx-.029,1.015,gz-.007,.038,.014],
    [gx-.027,1.016,gz+.002,.035,.012], [gx-.026,1.016,gz+.007,.030,.007]
  ];
  gloveSurface('leather',palmRows,[0,1,0],26);

  // Generated about the actual shield handle, so a change in the grip anchor
  // moves both the mount and the finger contact patches.
  const fingers = [
    [1.044,.0096,12], [1.025,.0101,-5], [1.006,.0092,-9], [.988,.0078,14]
  ];
  for (const [y, r, endDegrees] of fingers) {
    const stations = [
      [178,.030,0,1.04], [154,.026,-.001,1], [127,.024,.001,.92],
      [105,.025,.002,.99], [83,.024,.002,.89], [61,.023,.001,.82],
      [43,.024,0,.87], [27,.022,-.001,.78], [endDegrees,.021,-.002,.62],
      [endDegrees-7,.020,-.002,.20], [endDegrees-8,.020,-.002,.035]
    ];
    const finger = stations.map(([deg,radius,lift,fat]) =>
      [gx+Math.cos(deg*Math.PI/180)*radius, y+lift,
        gz+Math.sin(deg*Math.PI/180)*radius, r*fat,r*fat*.94]);
    gloveSurface('leather', finger,[0,1,0],14);
  }
  // The heel of the thumb moulds into the back of the palm. Its pad presses
  // the opposite rear quadrant, below the top bracket and above the index.
  const thumbRows = [
    [gx-.026,1.040,gz-.035,.017,.015], [gx-.018,1.050,gz-.032,.016,.014],
    [gx-.005,1.054,gz-.026,.014,.012], [gx+.007,1.055,gz-.023,.012,.011],
    [gx+.015,1.052,gz-.018,.011,.010], [gx+.021,1.048,gz-.012,.010,.009],
    [gx+.022,1.044,gz-.005,.0083,.0074], [gx+.022,1.043,gz+.001,.002,.002]
  ];
  gloveSurface('leather',thumbRows,[0,1,0],16);
}

module.exports={openHand,shieldHand};
