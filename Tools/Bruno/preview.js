import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
const canvas=document.querySelector('canvas'), renderer=new THREE.WebGLRenderer({canvas,antialias:true,preserveDrawingBuffer:true});
renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
const scene=new THREE.Scene();scene.background=new THREE.Color('#e8e3d9');scene.fog=new THREE.Fog('#e8e3d9',9,20);
const camera=new THREE.PerspectiveCamera(32,1,.05,50);camera.position.set(3.4,2.2,5.7);
const controls=new OrbitControls(camera,canvas);controls.target.set(0,1.15,0);controls.enableDamping=true;controls.minDistance=2.2;controls.maxDistance=9;controls.maxPolarAngle=Math.PI*.53;
const hemi=new THREE.HemisphereLight('#e9efff','#73717e',1.4);scene.add(hemi);
const sun=new THREE.DirectionalLight('#fff1df',2.1);sun.position.set(-3,6,5);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);sun.shadow.camera.left=-3;sun.shadow.camera.right=3;sun.shadow.camera.top=4;sun.shadow.camera.bottom=-3;sun.shadow.normalBias=.025;scene.add(sun);
const floor=new THREE.Mesh(new THREE.PlaneGeometry(100,100),new THREE.MeshStandardMaterial({color:'#ded9ce',roughness:1}));floor.rotation.x=-Math.PI/2;floor.receiveShadow=true;floor.position.y=-.002;scene.add(floor);
const platform=new THREE.Mesh(new THREE.CylinderGeometry(1.16,1.19,.075,80),new THREE.MeshStandardMaterial({color:'#d0c8b9',roughness:1}));platform.position.y=-.042;platform.receiveShadow=true;scene.add(platform);
const ring=new THREE.Mesh(new THREE.TorusGeometry(1.16,.009,8,96),new THREE.MeshBasicMaterial({color:'#c46a32'}));ring.rotation.x=Math.PI/2;ring.position.y=.002;scene.add(ring);
const clock=new THREE.Clock();let mixer,model,head,lids,actions={},current,paused=false,gaze=true,blink=0,nextBlink=2.1,elapsed=0,greetUntil=0;
let currentName='Idle';const outlines=[];let handL,handR,restL,restR,toolKnife,toolPry;const gripMeshes=[];const heldBox=new THREE.Mesh(new THREE.BoxGeometry(.42/.82,.38/.82,.32/.82),toon(new THREE.MeshBasicMaterial({color:'#d48b48'})));heldBox.position.set(0,1.23/.82,.54/.82);heldBox.visible=false;scene.add(heldBox);const heldBoxes=[heldBox];for(let i=1;i<10;i++){const box=heldBox.clone();scene.add(box);heldBoxes.push(box)}let bundleCount=1;
function toon(original){
 const m=new THREE.MeshBasicMaterial({color:original.color});
 m.onBeforeCompile=s=>{
  s.vertexShader=s.vertexShader.replace('#if defined ( USE_ENVMAP ) || defined ( USE_SKINNING )','#if 1');
  s.vertexShader=s.vertexShader.replace('#include <common>','#include <common>\nvarying vec3 vBrunoNormal;');
  s.vertexShader=s.vertexShader.replace('#include <begin_vertex>', 'vBrunoNormal = normalize(inverseTransformDirection(transformedNormal, viewMatrix));\n#include <begin_vertex>');
  s.fragmentShader=s.fragmentShader.replace('#include <common>','#include <common>\nvarying vec3 vBrunoNormal;');
  s.fragmentShader=s.fragmentShader.replace('#include <opaque_fragment>',`float ndl=dot(normalize(vBrunoNormal),normalize(vec3(-.5,.85,.65))); float band=ndl>.42?1.0:(ndl>-.15?.77:.49); outgoingLight=diffuseColor.rgb*band; outgoingLight=mix(outgoingLight, outgoingLight*vec3(.76,.72,1.1),1.0-step(.42,ndl));\n#include <opaque_fragment>`);
 };return m;
}
function setState(name){if(!actions[name])return;greetUntil=0;currentName=name;current?.fadeOut(.22);current=actions[name];current.reset().fadeIn(.22).play();document.querySelectorAll('[data-state]').forEach(b=>b.classList.toggle('active',b.dataset.state===name));}
const data=Uint8Array.from(atob(window.BRUNO_GLB),c=>c.charCodeAt(0));
new GLTFLoader().parse(data.buffer,'',gltf=>{
 model=gltf.scene;scene.add(model);
 model.traverse(o=>{if(o.isMesh){o.material=Array.isArray(o.material)?o.material.map(toon):toon(o.material);o.castShadow=true;o.frustumCulled=false;if(o.morphTargetDictionary?.Blink_L!==undefined)lids=o;if(o.morphTargetDictionary?.Grip_R!==undefined)gripMeshes.push(o);}if(o.name==='Head')head=o;});
 // Inverted hull ink follows the same skeleton and morph weights.
 const originals=[];model.traverse(o=>{if(o.isSkinnedMesh)originals.push(o)});
 originals.forEach(o=>{
  const ink=new THREE.MeshBasicMaterial({color:'#282630',side:THREE.BackSide});
  ink.onBeforeCompile=s=>{s.vertexShader=s.vertexShader.replace('#include <begin_vertex>','vec3 transformed = vec3(position) + normal * .0018;')};
  const outline=new THREE.SkinnedMesh(o.geometry,ink);outline.name=o.name+'_Ink';outline.position.copy(o.position);outline.quaternion.copy(o.quaternion);outline.scale.copy(o.scale);outline.bind(o.skeleton,o.bindMatrix);outline.morphTargetInfluences=o.morphTargetInfluences;outline.morphTargetDictionary=o.morphTargetDictionary;outline.frustumCulled=false;o.parent.add(outline);outlines.push(outline);
 });
 model.updateMatrixWorld(true);
 const bone=name=>model.getObjectByName(name)||model.getObjectByName(name.replaceAll('.',''));
 handL=bone('Hand.L');handR=bone('Hand.R');const skin=originals.find(o=>o.skeleton.bones.includes(handR));
 const bindQ=hand=>{const matrix=skin.skeleton.boneInverses[skin.skeleton.bones.indexOf(hand)].clone().invert();return new THREE.Quaternion().setFromRotationMatrix(matrix)};
 restL=bindQ(handL);restR=bindQ(handR);
 new GLTFLoader().parse(Uint8Array.from(atob(window.BRUNO_TOOLS_GLB),c=>c.charCodeAt(0)).buffer,'',tools=>{
  tools.scene.traverse(o=>{if(o.isMesh){o.material=Array.isArray(o.material)?o.material.map(toon):toon(o.material);o.castShadow=true}});
  toolKnife=tools.scene.getObjectByName('BrunoBoxCutter');toolPry=tools.scene.getObjectByName('BrunoPryBar');
  [toolKnife,toolPry].forEach(o=>{scene.add(o);o.visible=false;o.scale.setScalar(1/.82)});
  window.bruno.toolsReady=true;
 });
 mixer=new THREE.AnimationMixer(model);gltf.animations.forEach(c=>{actions[c.name]=mixer.clipAction(c);});
 window.bruno={model,actions,renderer,scene,camera,setState,mixer,controls,heldBoxes};
 document.querySelector('#loading').remove();document.querySelector('#clipcount').textContent=Object.keys(actions).length+' hareket';setState('Idle');
},e=>{document.querySelector('#loading').textContent='Model yüklenemedi: '+e;console.error(e)});
document.querySelectorAll('[data-state]').forEach(b=>b.onclick=()=>setState(b.dataset.state));
document.querySelector('#count').oninput=e=>{bundleCount=+e.target.value;document.querySelector('#countLabel').textContent=bundleCount;setState('CarryIdle')};
document.querySelector('#greet').onclick=()=>{setState('Greet');greetUntil=elapsed+2.4};
document.querySelector('#pause').onclick=e=>{paused=!paused;e.target.textContent=paused?'Devam et':'Duraklat'};
document.querySelector('#gaze').onchange=e=>gaze=e.target.checked;
document.querySelector('#rotate').onchange=e=>controls.autoRotate=e.target.checked;controls.autoRotateSpeed=.8;
document.querySelector('#front').onclick=()=>{camera.position.set(0,1.6,6.6);controls.target.set(0,1.13,0)};
document.querySelector('#face').onclick=()=>{camera.position.set(.8,2.03,2.0);controls.target.set(0,1.9,0)};
document.querySelector('#reset').onclick=()=>{camera.position.set(3.4,2.2,5.7);controls.target.set(0,1.15,0)};
function resize(){const w=canvas.clientWidth,h=canvas.clientHeight;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix()}window.addEventListener('resize',resize);resize();
function solveArm(hand,target,pole,rest){
 const lower=hand.parent,upper=lower.parent;model.updateMatrixWorld(true);
 const a=upper.getWorldPosition(new THREE.Vector3()),b=lower.getWorldPosition(new THREE.Vector3()),c=hand.getWorldPosition(new THREE.Vector3());
 const l1=a.distanceTo(b),l2=b.distanceTo(c),dir=target.clone().sub(a).normalize(),d=Math.max(Math.abs(l1-l2)+.001,Math.min(l1+l2-.001,a.distanceTo(target)));
 const along=(l1*l1-l2*l2+d*d)/(2*d),bend=pole.clone().sub(a);bend.addScaledVector(dir,-bend.dot(dir)).normalize();const elbow=a.clone().addScaledVector(dir,along).addScaledVector(bend,Math.sqrt(Math.max(0,l1*l1-along*along)));
 function worldQ(o,q){o.quaternion.copy(o.parent.getWorldQuaternion(new THREE.Quaternion()).invert().multiply(q));model.updateMatrixWorld(true)}
 worldQ(upper,new THREE.Quaternion().setFromUnitVectors(b.clone().sub(a).normalize(),elbow.sub(a).normalize()).multiply(upper.getWorldQuaternion(new THREE.Quaternion())));
 const b2=lower.getWorldPosition(new THREE.Vector3()),c2=hand.getWorldPosition(new THREE.Vector3()),reachable=a.clone().addScaledVector(dir,d);
 worldQ(lower,new THREE.Quaternion().setFromUnitVectors(c2.sub(b2).normalize(),reachable.sub(b2).normalize()).multiply(lower.getWorldQuaternion(new THREE.Quaternion())));
 worldQ(hand,new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(1,0,0),-Math.PI/2).multiply(rest));
}
function props(){
 const cutting=currentName==='BoxCut',prying=currentName==='Pry',carrying=currentName.startsWith('Carry');
 gripMeshes.forEach(o=>{o.morphTargetInfluences[o.morphTargetDictionary.Grip_L]=cutting||prying?.35:carrying?.25:0;o.morphTargetInfluences[o.morphTargetDictionary.Grip_R]=cutting||prying?1:carrying?.25:0});
 const cols=bundleCount===1?1:bundleCount<=4?2:3,rows=bundleCount<=2?1:2,scale=bundleCount===1?1:bundleCount===2?.65:bundleCount<=4?.52:.4;
 heldBoxes.forEach((b,i)=>{b.visible=carrying&&i<bundleCount;b.scale.setScalar(scale);b.position.set(((i%cols)-(cols-1)*.5)*.46*scale/.82,(1.04+Math.floor(i/(cols*rows))*.44*scale+.19*scale)/.82,(.54+(Math.floor(i/cols)%rows-(rows-1)*.5)*.42*scale)/.82)});
 const gripX=Math.max(.11,cols*.46*scale*.32);
 if(carrying&&handL){solveArm(handL,new THREE.Vector3(-gripX,.97,.47).multiplyScalar(1/.82),new THREE.Vector3(-1,.75,.1).multiplyScalar(1/.82),restL);solveArm(handR,new THREE.Vector3(gripX,.97,.47).multiplyScalar(1/.82),new THREE.Vector3(1,.75,.1).multiplyScalar(1/.82),restR)}
 if(toolKnife){toolKnife.visible=cutting;toolPry.visible=prying;const active=cutting?toolKnife:toolPry;
  model.updateMatrixWorld(true);const q=handR.getWorldQuaternion(new THREE.Quaternion()).multiply(restR.clone().invert()).multiply(new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(1,0,0),Math.PI/2));
  active.quaternion.copy(q);active.position.copy(handR.getWorldPosition(new THREE.Vector3())).add(new THREE.Vector3(0,.055,.04).multiplyScalar(1/.82).applyQuaternion(q));
 }
}
function animate(){requestAnimationFrame(animate);const dt=Math.min(clock.getDelta(),.05);if(!paused&&mixer){elapsed+=dt;mixer.update(dt);if(greetUntil&&elapsed>greetUntil)setState('Idle');if(head&&gaze)head.rotation.y+=Math.sin(elapsed*.61)*.13;
 if(elapsed>nextBlink){blink=.18;nextBlink=elapsed+2.7+Math.random()*2.5}blink=Math.max(0,blink-dt);const b=blink>0?Math.sin((1-blink/.18)*Math.PI):0;if(lids){lids.morphTargetInfluences[lids.morphTargetDictionary.Blink_L]=b;lids.morphTargetInfluences[lids.morphTargetDictionary.Blink_R]=b;}}
 if(model)props();controls.update();renderer.render(scene,camera)}animate();
