"""Blender 4.2: matched cel-shaded utility knife and forked nail puller, metre units."""
import bpy,math,os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=ROOT+'/Assets/Bidwarss/Characters/Bruno/Resources/Bruno'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
materials={}
for name,h in [('ToolSteel','9DAEB5'),('ToolDark','303641'),('ToolOrange','D86B29'),('ToolEdge','DBE2D9')]:
 m=bpy.data.materials.new(name);m.use_nodes=True
 rgb=tuple(int(h[i:i+2],16)/255 for i in [0,2,4]);lin=tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb)
 m.diffuse_color=(*lin,1);m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*lin,1)
 m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8;materials[name]=m
def block(name,p,size,mat,bevel=.004):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.scale=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 mod=o.modifiers.new('Machined edges','BEVEL');mod.width=bevel;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
 o.data.materials.append(materials[mat]);return o
def tube(points,radius,mat):
 vs=[];fs=[];n=12
 for i,point in enumerate(points):
  p=Vector(point);d=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])
  d.normalize();a=Vector((1,0,0));b=d.cross(a).normalized()
  for j in range(n):vs.append(tuple(p+radius*(a*math.cos(j*2*math.pi/n)+b*math.sin(j*2*math.pi/n))))
 for i in range(len(points)-1):
  for j in range(n):a=i*n+j;b=i*n+(j+1)%n;fs.append((a,b,b+n,a+n))
 fs.extend([tuple(reversed(range(n))),tuple((len(points)-1)*n+j for j in range(n))])
 mesh=bpy.data.meshes.new('Forged steel');mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new('Forged steel',mesh);bpy.context.collection.objects.link(o);mesh.materials.append(materials[mat]);return o
roots=[]
for name in ['BrunoBoxCutter','BrunoPryBar']:
 bpy.ops.object.select_all(action='DESELECT');parts=[]
 if name=='BrunoBoxCutter':
  parts.append(block('Ergonomic orange grip',(0,0,0),(.037,.155,.031),'ToolOrange'))
  parts.append(block('Rubber underside',(0,.004,-.014),(.031,.136,.015),'ToolDark'))
  parts.append(block('Blade guide',(0,-.068,.006),(.024,.043,.019),'ToolSteel'))
  parts.append(block('Slider',(0,.011,.020),(.018,.036,.009),'ToolDark',.002))
  # Slanted snap-off blade, actual thickness rather than a one-sided plane.
  vs=[(-.010,-.078,.003),(.010,-.078,.003),(.010,-.198,.003),(-.010,-.177,.003),(-.010,-.078,.006),(.010,-.078,.006),(.010,-.198,.006),(-.010,-.177,.006)]
  fs=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
  mesh=bpy.data.meshes.new('Blade');mesh.from_pydata(vs,[],fs);o=bpy.data.objects.new('Snap blade',mesh);bpy.context.collection.objects.link(o);mesh.materials.append(materials['ToolSteel']);parts.append(o)
  for y in [-.10,-.125,-.15]:parts.append(block('Blade score',(0,y,.007),(.021,.001,.001),'ToolDark',.0001))
  for y in [-.045,-.025,-.005,.015,.035,.055]:parts.append(block('Grip rib',(0,y,-.023),(.031,.004,.003),'ToolDark',.001))
  tip=(.01,-.198,.005)
 else:
  parts.append(tube([(0,.085,0),(0,0,0),(0,-.15,0),(0,-.26,0),(0,-.31,.014),(0,-.335,.035)],.011,'ToolSteel'))
  parts.append(block('Rubber grip',(0,.015,0),(.033,.13,.033),'ToolDark'))
  for y in [-.035,-.010,.015,.04,.065]:parts.append(block('Orange grip rib',(0,y,0),(.035,.009,.035),'ToolOrange',.003))
  # Split fork with open V gap for a nail head; no solid block pretending to be a fork.
  for sign in [-1,1]:
   part=block('Fork tine',(sign*.013,-.346,.030),(.014,.063,.009),'ToolSteel',.002);part.rotation_euler.x=math.radians(14)
  # Add both tines by name from the scene, avoiding duplicate inclusion.
  parts.extend([o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Fork tine') and o not in parts])
  tip=(0,-.377,.023)
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();model=bpy.context.object;model.name=name+'_Mesh'
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root);model.parent=root
 marker=bpy.data.objects.new('ToolTip',None);bpy.context.collection.objects.link(marker);marker.parent=root;marker.location=tip
 bpy.ops.object.select_all(action='DESELECT')
 for o in [root,model,marker]:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
 roots.append(root)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=ROOT+'/Documentation/Bruno/BrunoTools.glb',export_format='GLB',use_selection=True,export_animations=False)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Tools/Bruno/BrunoTools.blend',compress=True)
