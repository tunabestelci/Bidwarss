"""Reproducible Bruno model. Blender 4.2+, no external assets or add-ons.
blender -b --python Tools/Bruno/build_bruno.py
Coordinates: Z up, -Y forward. Metres. Export: Unity FBX + browser GLB.
"""
import bpy, math, os, json
from mathutils import Vector
from math import sin, cos, pi
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/Bidwarss/Characters/Bruno/Resources/Bruno')
DOC=os.path.join(ROOT,'Documentation/Bruno')
os.makedirs(OUT,exist_ok=True); os.makedirs(DOC,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.materials): bpy.data.materials.remove(data)
M={}; parts=[]
palette={'Fur':'89786C','FurLight':'B7A18A','Muzzle':'CCB59A','Hair':'39343B','Shirt':'25454A',
 'Vest':'C96528','VestEdge':'E5893A','Seam':'693925','Pants':'383A42','PantsLight':'494A51',
 'Boot':'9D703E','BootLight':'BA8D52','Sole':'272B32','Ink':'282630','Eyes':'EEE4B7','Iris':'7B6B3B','Metal':'B79A65'}
def linear(c): return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
for name,h in palette.items():
 m=bpy.data.materials.new(name); rgb=tuple(linear(int(h[i:i+2],16)/255) for i in (0,2,4))
 m.diffuse_color=(*rgb,1); m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*rgb,1); bs.inputs['Roughness'].default_value=.9
 M[name]=m

def finish(obj,name,mat,bone=None,smooth=True):
 obj.name=name; obj.data.materials.clear(); obj.data.materials.append(M[mat])
 if obj.type=='MESH':
  for p in obj.data.polygons:p.use_smooth=smooth
 if bone:
  obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))),1,'REPLACE')
 parts.append(obj);return obj
def mesh(name,vs,fs,mat,bone=None,smooth=True):
 d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update();o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o)
 return finish(o,name,mat,bone,smooth)
def sphere(name,pos,scale,mat,bone,segments=24,rings=16):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=pos)
 o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 return finish(o,name,mat,bone)
def cube(name,pos,scale,mat,bone,bevel=.02,rot=(0,0,0)):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos,rotation=rot);o=bpy.context.object;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  b=o.modifiers.new('Tailored soft edges','BEVEL');b.width=bevel;b.segments=3
  bpy.ops.object.modifier_apply(modifier=b.name)
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 return finish(o,name,mat,bone)
def loft(name,rings,mat,bone,n=32,start=0,end=2*pi):
 # rings: (x,y,z, radius x, radius y); end caps for full closed rings
 vs=[];fs=[];closed=abs(end-start-2*pi)<.001;count=n if closed else n+1
 for x,y,z,rx,ry in rings:
  for j in range(count):
   a=start+(end-start)*j/n;vs.append((x+rx*sin(a),y-ry*cos(a),z))
 for i in range(len(rings)-1):
  for j in range(n if closed else count-1):
   k=(j+1)%count;a=i*count+j;b=i*count+k
   fs.append((a,b,b+count,a+count))
 if closed:fs.extend([tuple(reversed(range(count))),tuple((len(rings)-1)*count+j for j in range(count))])
 o=mesh(name,vs,fs,mat,bone)
 return o
def tube(name,points,radii,mat,bone,n=10):
 vs=[];fs=[]
 for i,p in enumerate(points):
  p=Vector(p);d=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
  d.normalize();a=d.cross(Vector((0,1,0)))
  if a.length<.01:a=d.cross(Vector((1,0,0)))
  a.normalize();b=d.cross(a).normalized()
  r=radii[i];r=(r,r) if isinstance(r,(int,float)) else r
  for j in range(n):vs.append(tuple(p+a*cos(j*2*pi/n)*r[0]+b*sin(j*2*pi/n)*r[1]))
 for i in range(len(points)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 fs.extend([tuple(reversed(range(n))),tuple((len(points)-1)*n+j for j in range(n))])
 return mesh(name,vs,fs,mat,bone)
def line(name,points,r,mat,bone):return tube(name,points,[r]*len(points),mat,bone,8)
def tuft(name,base,tip,width,depth,mat,bone,bend=None):
 b=Vector(base);t=Vector(tip);mid=b.lerp(t,.46)+Vector(bend or (0,-.01,0))
 return tube(name,[b,mid,b.lerp(t,.8),t],[(width,depth),(width*.88,depth*.86),(width*.42,depth*.44),(.002,.002)],mat,bone,9)

# Rounded, broad torso; clothing is shaped rather than a stack of primitives.
loft('Trousers pelvis',[(0,0,.66,.22,.17),(0,0,.78,.32,.20),(0,0,.98,.33,.205),(0,0,1.04,.30,.18)],'Pants','Hips')
torsoR=[(0,0,.97,.29,.18),(0,-.025,1.05,.35,.23),(0,-.035,1.22,.40,.27),(0,-.018,1.41,.43,.255),(0,0,1.57,.43,.22),(0,0,1.65,.30,.16)]
loft('Petrol tee',torsoR,'Shirt','Chest')
# Open orange vest, back and two front panels.
vestR=[(x,y+.006,z,rx+.022,ry+.016) for x,y,z,rx,ry in torsoR]
vest=loft('Open work vest',vestR,'Vest','Chest',40,start=.66,end=2*pi-.66)
solid=vest.modifiers.new('Heavy cloth thickness','SOLIDIFY');solid.thickness=.014
bpy.context.view_layer.objects.active=vest;bpy.ops.object.modifier_apply(modifier=solid.name)
for s in [-1,1]:
 # Panel edges, collar flaps and shaped pockets.
 edge=[(s*rx*sin(.66),y-ry*cos(.66)-.005,z) for x,y,z,rx,ry in vestR]
 line('Vest piping',edge,.009,'VestEdge','Chest')
 cube('Chest pocket',(s*.30,-.222,1.37),(.165,.028,.19),'Vest','Chest',.018,rot=(0,s*.08,s*.1))
 cube('Pocket flap',(s*.30,-.246,1.425),(.174,.024,.064),'VestEdge','Chest',.012)
 sphere('Pocket snap',(s*.30,-.266,1.425),(.012,.007,.012),'Metal','Chest',12,8)
 tuft('Folded collar',(s*.23,-.137,1.635),(s*.19,-.242,1.47),.075,.02,'VestEdge','Chest')
 line('Pocket stitching',[(s*.37,-.244,1.38),(s*.37,-.244,1.294),(s*.24,-.244,1.294)],.003,'Seam','Chest')
 for z in [1.075,1.22]:sphere('Vest rivet',(s*.236,-.204,z),(.009,.006,.009),'Metal','Chest',12,8)
# Tee neck rim, belt and restrained seams.
loft('Neck fur',[(0,0,1.56,.19,.14),(0,0,1.74,.20,.15),(0,0,1.8,.16,.13)],'FurLight','Chest')
loft('Belt',[(0,0,.982,.334,.211),(0,0,1.035,.332,.211)],'Sole','Hips')
cube('Buckle',(0,-.225,1.01),(.105,.035,.068),'Metal','Hips',.01)
cube('Buckle inset',(0,-.245,1.01),(.063,.009,.038),'Seam','Hips',.006)
for s in [-1,1]:
 cube('Belt loop',(s*.22,-.167,1.015),(.035,.024,.086),'PantsLight','Hips',.008)
 # Strong short legs with knees, cuffs and large designed work boots.
 leg=loft('Trouser leg',[(s*.205,.012,.20,.128,.12),(s*.21,0,.29,.142,.145),(s*.205,0,.40,.143,.144),(s*.195,0,.54,.161,.15),(s*.18,0,.74,.18,.18),(s*.15,0,.84,.16,.16)],'Pants',None,24)
 for v in leg.data.vertices:
  t=max(0,min(1,(v.co.z-.39)/.17))
  for bn,w in [('Thigh',t),('Shin',1-t)]:
   g=leg.vertex_groups.get(bn+('.L' if s<0 else '.R')) or leg.vertex_groups.new(name=bn+('.L' if s<0 else '.R'))
   if w:g.add([v.index],w,'REPLACE')
 side='.L' if s<0 else '.R';foot='Foot'+side
 loft('Rolled cuff',[(s*.205,0,.23,.139,.137),(s*.205,0,.285,.146,.145),(s*.205,0,.305,.139,.14)],'PantsLight','Shin'+side,24)
 cube('Cargo pocket',(s*.331,-.01,.665),(.034,.18,.16),'PantsLight','Thigh'+side,.023)
 cube('Cargo flap',(s*.35,-.012,.72),(.026,.19,.045),'Pants','Thigh'+side,.01)
 boot=loft('Leather work boot',[(s*.21,-.082,.055,.159,.242),(s*.21,-.082,.09,.163,.244),(s*.21,-.073,.145,.158,.225),(s*.21,-.045,.205,.138,.17),(s*.21,0,.28,.112,.116)],'Boot',foot,32)
 loft('Rubber sole',[(s*.21,-.08,.018,.165,.248),(s*.21,-.08,.045,.17,.252),(s*.21,-.08,.073,.168,.248)],'Sole',foot,32)
 # vamp toe seam
 line('Toe stitching',[(s*.21+.14*cos(a),-.09-.19*sin(a),.126+.02*sin(a)) for a in [i*pi/16 for i in range(17)]],.004,'Seam',foot)
 for i in range(3):
  z=.19+i*.022;y=-.20+i*.033
  line('Boot lace',[(s*.21-.056,y,z),(s*.21+.057,y-.005,z+.006)],.008,'BootLight',foot)
 for dx in [-.105,-.035,.035,.105]:cube('Sole tread',(s*.21+dx,-.315,.039),(.023,.018,.031),'PantsLight',foot,.003)
 # Continuous arms, extra large forearm, articulated palms and four fingers + thumb.
 arm=loft('Sculpted arm',[(s*.725,-.025,.52,.112,.106),(s*.735,-.01,.65,.166,.151),(s*.70,0,.83,.202,.177),(s*.625,.015,1.04,.178,.175),(s*.565,.012,1.22,.173,.177),(s*.49,0,1.43,.217,.201),(s*.405,0,1.55,.125,.139)],'Fur',None,28)
 for v in arm.data.vertices:
  t=max(0,min(1,(v.co.z-.96)/.24))
  for bn,w in [('UpperArm',t),('Forearm',1-t)]:
   g=arm.vertex_groups.get(bn+side) or arm.vertex_groups.new(name=bn+side)
   if w:g.add([v.index],w,'REPLACE')
 # Directional fur clumps articulate with matching bones.
 for k,(x,z,w) in enumerate([(.51,1.48,.065),(.62,1.32,.067),(.69,1.15,.062),(.76,.98,.075),(.82,.83,.070),(.84,.68,.06)]):
  bn=('UpperArm' if z>1.12 else 'Forearm')+side
  tuft('Arm silhouette fur',(s*x,.02,z),(s*(x+.07),-.006,z-.16),w,.055,'Fur',bn)
 for x,z in [(.53,1.40),(.62,1.17),(.73,.88),(.73,.74)]:
  tuft('Forearm fur planes',(s*x,-.156,z),(s*(x+.02),-.157,z-.13),.052,.019,'FurLight' if z>1.2 else 'Fur',('UpperArm' if z>1.12 else 'Forearm')+side)
 hand='Hand'+side
 sphere('Palm',(s*.73,-.039,.48),(.145,.113,.157),'FurLight',hand)
 for j in range(4):
  x=s*(.631+j*.064);z=.387+(.009 if j in (0,3) else -.016)
  tube('Finger',[(x,-.052,.46),(x,-.082,z),(x,-.099,z-.055),(x,-.129,z-.052)],[.032,.032,.029,.023],'FurLight',hand,10)
  line('Knuckle crease',[(x-.018,-.119,z),(x+.014,-.12,z-.005)],.0028,'Fur',hand)
 tube('Thumb',[(s*.625,-.066,.52),(s*.593,-.121,.47),(s*.595,-.15,.421)],[.05,.044,.032],'FurLight',hand,12)

# Head, cheek planes and visible ears: distinct from the reference's human face.
sphere('Head core',(0,0,1.858),(.285,.218,.265),'Fur','Head',32,24)
sphere('Lower face',(0,-.142,1.792),(.245,.143,.165),'Muzzle','Head',32,20)
sphere('Upper muzzle',(0,-.203,1.845),(.172,.105,.092),'Muzzle','Head',28,18)
for s in [-1,1]:
 sphere('Ear',(s*.267,.006,1.96),(.077,.045,.086),'FurLight','Head',20,14)
 sphere('Ear inset',(s*.276,-.036,1.963),(.043,.012,.053),'Seam','Head',18,12)
 sphere('Eye socket',(s*.109,-.183,1.973),(.094,.036,.073),'Fur','Head',24,16)
 sphere('Eye',(s*.109,-.218,1.975),(.078,.032,.055),'Eyes','Head',24,18)
 # Separate eye bones make subtle gaze possible without moving the whole head.
 sphere('Iris',(s*.109,-.249,1.976),(.023,.008,.027),'Iris','Eye'+('.L' if s<0 else '.R'),20,14)
 sphere('Pupil',(s*.109,-.256,1.976),(.013,.005,.021),'Ink','Eye'+('.L' if s<0 else '.R'),18,12)
 sphere('Eye glint',(s*.109-.006,-.261,1.988),(.005,.002,.006),'Eyes','Eye'+('.L' if s<0 else '.R'),12,8)
 # Brow is a chunky designed ridge, mildly asymmetrical.
 tube('Heavy brow',[(s*.026,-.225,2.042),(s*.094,-.225,2.055+(s*.007)),(s*.184,-.203,2.058),(s*.207,-.18,2.04)],[(.018,.019),(.034,.025),(.028,.022),(.008,.01)],'Hair','Head',12)
 for k in range(3):
  z=1.9-k*.093
  tuft('Cheek silhouette',(s*(.214-k*.008),-.07,z),(s*(.295-k*.027),-.081,z-.118),.078-k*.012,.044,'FurLight','Head')
 for k in range(3):
  x=s*(.042+k*.06);z=1.704+abs(x)*.25
  tuft('Beard points',(x,-.171,z+.055),(x*1.2,-.177,z-.075),.047,.028,'Muzzle','Head')
# Flattened broad nose: a custom rounded triangular mesh with subdivision.
nose=mesh('Broad charcoal nose',[(-.118,-.265,1.9),(.118,-.265,1.9),(.071,-.306,1.853),(0,-.326,1.839),(-.071,-.306,1.853),(0,-.327,1.897),(0,-.25,1.865)],[(0,1,5),(1,2,5),(2,3,5),(3,4,5),(4,0,5),(1,0,6),(2,1,6),(3,2,6),(4,3,6),(0,4,6)],'Hair','Head')
bpy.context.view_layer.objects.active=nose;bpy.ops.object.select_all(action='DESELECT');nose.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
for v in nose.data.vertices:v.co.y-=.035;v.co.z=1.88+(v.co.z-1.88)*1.45;v.co.x*=1.13
sub=nose.modifiers.new('Nose sculpt','SUBSURF');sub.levels=2;bpy.context.view_layer.objects.active=nose;bpy.ops.object.modifier_apply(modifier=sub.name)
for s in [-1,1]:sphere('Nostril',(s*.068,-.344,1.870),(.023,.009,.010),'Ink','Head',16,10)
line('Knowing smile',[(-.166,-.22,1.804),(-.127,-.249,1.782),(-.063,-.275,1.772),(0,-.282,1.773),(.071,-.273,1.782),(.139,-.241,1.802),(.159,-.223,1.82)],.007,'Ink','Head')
line('Lower lip',[(-.056,-.276,1.746),(0,-.283,1.74),(.063,-.268,1.753)],.003,'Fur','Head')
for s in [-1,1]:line('Smile crease',[(s*.153,-.23,1.805),(s*.169,-.218,1.827)],.004,'Fur','Head')
# Sculpted swept hair, large coherent locks rather than many noisy spikes.
for i in range(7):
 x=-.215+i*.063;z=2.058+.018*(1-abs(x)/.24)
 tuft('Swept hair',(x,.001,z),(x+.074,.045,z+.077+(i%3)*.017),.075,.084,'Hair','Head',bend=(.02,-.018,.028))
for s in [-1,1]:
 tuft('Silver temple',(s*.214,-.039,2.064),(s*.247,-.066,1.974),.047,.02,'FurLight','Head')
 for k in range(3):tuft('Back hair',(s*.12,.155,1.99-k*.08),(s*.19,.195,1.88-k*.07),.06,.04,'Fur','Head')

# One separate skinned eyelid object with actual closing lids (eye whites stay spherical).
vs=[];closedvs=[];fs=[];side_indices={}
for s in [-1,1]:
 start=len(vs);side_indices[s]=[]
 for j in range(9):
  v=j/8
  for i in range(17):
   u=-1+2*i/16;arc=math.sqrt(max(0,1-u*u));x=s*.109+u*.079
   # top boundary to open half-lid; closed boundary sweeps to bottom eye edge
   zopen=1.975+arc*(.057*(1-v)+.009*v)
   zclosed=1.975+arc*(.057*(1-v)-.057*v)
   def yy(z):return -.218-.034*math.sqrt(max(.01,1-u*u-((z-1.975)/.058)**2))-.016
   vs.append((x,yy(zopen),zopen));closedvs.append((x,yy(zclosed),zclosed));side_indices[s].append(len(vs)-1)
 for j in range(8):
  for i in range(16):a=start+j*17+i;fs.append((a+17,a+18,a+1,a))
lids=mesh('Bruno_Eyelids',vs,fs,'Fur','Head')
lids.shape_key_add(name='Basis')
for s,label in [(-1,'Blink_L'),(1,'Blink_R')]:
 key=lids.shape_key_add(name=label)
 for i in side_indices[s]:key.data[i].co=closedvs[i]

# Refine the sculpt and broaden the upper silhouette; interpolate weights through subdivision.
for o in parts:
 if o.name.startswith(('Sculpted arm','Petrol tee','Trouser leg','Trousers pelvis','Open work vest','Leather work boot')):
  bpy.context.view_layer.objects.active=o
  sub=o.modifiers.new('Sculpt finish','SUBSURF');sub.levels=1
  bpy.ops.object.modifier_apply(modifier=sub.name)
 for v in o.data.vertices:
  if v.co.z>1.06 and o!=lids:
   v.co.x*=1.12
 if o==lids:
  for key in o.data.shape_keys.key_blocks:
   for v in key.data:v.co.x*=1.12
 # Recalculate outward-facing closed-surface normals. Eyelids are an open shell.
 if o!=lids:
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')

# Assemble everything except lids into one mesh: 2 skinned renderers / character.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
 if o!=lids:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='Bruno_Body'
# First-person meshes are extracted from this same sculpt; no primitive placeholders.
fp_objects=[]
for sign,suffix in [(-1,'.L'),(1,'.R')]:
 indices={g.index for g in body.vertex_groups if g.name in ['Hand'+suffix,'Forearm'+suffix]}
 allowed={v.index for v in body.data.vertices if v.co.z<1.01 and sum(g.weight for g in v.groups if g.group in indices)>.95}
 polys=[p for p in body.data.polygons if all(i in allowed for i in p.vertices)]
 used=sorted({i for p in polys for i in p.vertices});remap={old:i for i,old in enumerate(used)}
 verts=[]
 for i in used:
  v=body.data.vertices[i].co;verts.append((v.x-sign*.73,v.z-.50,-(v.y+.04)))
 faces=[tuple(remap[i] for i in p.vertices) for p in polys]
 data=bpy.data.meshes.new('Bruno hand'+suffix);data.from_pydata(verts,[],faces);data.update()
 ob=bpy.data.objects.new('HandLeft' if sign<0 else 'HandRight',data);bpy.context.collection.objects.link(ob)
 for mat in body.data.materials:data.materials.append(mat)
 for p,original in zip(data.polygons,polys):p.material_index=original.material_index;p.use_smooth=True
 fp_objects.append(ob)
bpy.ops.object.select_all(action='DESELECT')
for o in fp_objects:
 o.shape_key_add(name='Basis');grip=o.shape_key_add(name='Grip')
 for v in grip.data:
  if v.co.y<-.06:
   d=-.06-v.co.y;v.co.y=-.06-d*.38;v.co.z+=.025*sin(min(1,d/.16)*pi)
 o.select_set(True)
bpy.context.view_layer.objects.active=fp_objects[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'BrunoHands.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=False,mesh_smooth_type='FACE')
for o in fp_objects:bpy.data.objects.remove(o,do_unlink=True)

# Independent finger curl shapes for tool grip; preserve the idle silhouette.
body.shape_key_add(name='Basis')
for suffix,label in [('.L','Grip_L'),('.R','Grip_R')]:
 key=body.shape_key_add(name=label);group=body.vertex_groups['Hand'+suffix].index
 for v in body.data.vertices:
  if v.co.z<.44 and any(g.group==group and g.weight>.9 for g in v.groups):
   d=.44-v.co.z;key.data[v.index].co.z=.44-d*.38;key.data[v.index].co.y-=.025*sin(min(1,d/.16)*pi)

# Rig: generic, deliberately short limbs, not Unity Humanoid proportions.
arm=bpy.data.armatures.new('Bruno_Skeleton');rig=bpy.data.objects.new('Bruno_Rig',arm);bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
spec=[('Root',(0,0,0),(0,0,.2),None),('Hips',(0,0,.84),(0,0,1.02),'Root'),('Chest',(0,0,1.02),(0,0,1.58),'Hips'),('Head',(0,0,1.64),(0,0,2.08),'Chest')]
for s,suf in [(-1,'.L'),(1,'.R')]:
 spec += [('UpperArm'+suf,(s*.40,0,1.48),(s*.63,0,1.02),'Chest'),('Forearm'+suf,(s*.63,0,1.02),(s*.73,-.025,.57),'UpperArm'+suf),('Hand'+suf,(s*.73,-.025,.57),(s*.73,-.08,.38),'Forearm'+suf),('Thigh'+suf,(s*.18,0,.81),(s*.21,0,.46),'Hips'),('Shin'+suf,(s*.21,0,.46),(s*.21,0,.16),'Thigh'+suf),('Foot'+suf,(s*.21,0,.16),(s*.21,-.23,.10),'Shin'+suf),('Eye'+suf,(s*.109,-.218,1.975),(s*.109,-.29,1.975),'Head')]
for name,h,t,parent in spec:
 b=arm.edit_bones.new(name);b.head=(h[0]*(1.12 if h[2]>1.06 else 1),h[1],h[2]);b.tail=(t[0]*(1.12 if t[2]>1.06 else 1),t[1],t[2])
 if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
for o in [body,lids]:
 o.parent=rig;mod=o.modifiers.new('Bruno skin','ARMATURE');mod.object=rig
 # UVs support future paintwork; all present surface detail is modelled.
 bpy.context.view_layer.objects.active=o;rig.select_set(False);o.select_set(True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig

# Generic rig clips, in-place. Animate world-axis deltas in bone rest space.
def rot(name,xyz):
 p=rig.pose.bones[name];q=mathutils.Euler(xyz,'XYZ').to_quaternion();basis=arm.bones[name].matrix_local.to_quaternion()
 p.rotation_mode='QUATERNION';p.rotation_quaternion=basis.inverted()@q@basis
def keyframe(frame):
 for p in rig.pose.bones:
  p.keyframe_insert('rotation_quaternion',frame=frame);p.keyframe_insert('location',frame=frame);p.keyframe_insert('scale',frame=frame)
import mathutils
rig.animation_data_create();actions=[]
for name,duration in [('Idle',6),('Walk',1),('CarryIdle',6),('CarryWalk',1),('Greet',2.4),('BoxCut',1.6),('Pry',1.8)]:
 action=bpy.data.actions.new(name);rig.animation_data.action=action
 end=round(duration*30)+1
 for f in range(1,end+1):
  t=(f-1)/30;phase=t/duration*2*pi;walk='Walk' in name;carry='Carry' in name;amount=1 if walk else 0
  for p in rig.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
  breathe=sin(t*2*pi/3) if not walk else sin(phase*2)
  rig.pose.bones['Chest'].scale=(1+.009*breathe,1+.008*breathe,1+.012*breathe)
  rig.pose.bones['Hips'].location.y=.008*breathe+.012*abs(sin(phase))*amount
  rot('Chest',(0,.014*sin(phase),.022*sin(phase)))
  rot('Head',(.016*sin(phase),0,.055*sin(phase)))
  for s,suf in [(-1,'.L'),(1,'.R')]:
   step=sin(phase)*s
   rot('Thigh'+suf,(step*.42*amount,0,0))
   rot('Shin'+suf,(-max(0,-step)*.42*amount,0,0))
   rot('Foot'+suf,(max(0,step)*.12*amount,0,0))
   rot('UpperArm'+suf,(-.88 if carry else -step*.28*amount-.02*breathe,0,s*(.06 if carry else .015)))
   rot('Forearm'+suf,(-.45 if carry else -.055-.025*breathe,0,0))
  if name in ['BoxCut','Pry']:
   rot('UpperArm.L',(-1.0,0,-.12));rot('Forearm.L',(-.65,0,0))
   rot('UpperArm.R',(-1.0+.05*sin(phase),0,.08))
   rot('Forearm.R',(-.55+(.10 if name=='BoxCut' else .27)*sin(phase),0,0))
   rot('Hand.R',(.06*sin(phase),0,.06*sin(phase) if name=='BoxCut' else 0))
   rot('Head',(.1,0,-.035))
  if name=='Greet':
   blend=sin(pi*min(1,t/duration))**.55
   rot('UpperArm.R',(-.4*blend,-.98*blend,-.30*blend))
   rot('Forearm.R',(-1.3*blend,0,.22*sin(t*12)*blend))
   rot('Head',(.06*blend,0,-.09*blend))
  keyframe(f)
 for fc in action.fcurves:
  for k in fc.keyframe_points:k.interpolation='LINEAR'
 action.use_fake_user=True;actions.append(action)
rig.animation_data.action=actions[0];bpy.context.scene.frame_set(1)
bpy.context.scene.render.fps=30
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=181
# NLA named tracks give GLB one selectable clip per behavior.
rig.animation_data.action=None
for action in actions:
 tr=rig.animation_data.nla_tracks.new();tr.name=action.name;st=tr.strips.new(action.name,1,action);tr.mute=True
rig.animation_data.action=actions[0]
# FBX uses each action. GLB exports muted tracks when export_nla_strips is enabled.
bpy.ops.object.select_all(action='DESELECT')
for o in [rig,body,lids]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Bruno.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.2,use_mesh_modifiers=False,mesh_smooth_type='FACE')
rig.animation_data.action=None
for tr in rig.animation_data.nla_tracks:tr.mute=False
bpy.ops.export_scene.gltf(filepath=os.path.join(DOC,'Bruno.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,export_anim_slide_to_zero=True)
for tr in rig.animation_data.nla_tracks:tr.mute=True
rig.animation_data.action=actions[0];bpy.context.scene.frame_set(1)
# Blender source retains the editable rig, named material palette, UVs and clips.
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'Tools/Bruno/Bruno.blend'),compress=True)
stats={'vertices':len(body.data.vertices)+len(lids.data.vertices),'triangles':sum(len(p.vertices)-2 for o in [body,lids] for p in o.data.polygons),'bones':len(arm.bones),'materials':len(body.data.materials),'clips':[a.name for a in actions],'height_m':round(max(v.co.z for v in body.data.vertices),3)}
with open(os.path.join(DOC,'model-stats.json'),'w') as f:json.dump(stats,f,indent=2)
print('BRUNO_STATS',stats)
