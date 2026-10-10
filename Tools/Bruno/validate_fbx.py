"""Round-trip the actual shipped FBX, not the source .blend."""
import bpy, os, json, math
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
path=root+'/Assets/Bidwarss/Characters/Bruno/Resources/Bruno/Bruno.fbx'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=path)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
assert len(rig.data.bones)==18
assert {'Head','Hand.L','Hand.R','Root'}.issubset(rig.data.bones.keys())
assert len(meshes)==2
shapes=[k.name for o in meshes if o.data.shape_keys for k in o.data.shape_keys.key_blocks]
assert 'Blink_L' in shapes and 'Blink_R' in shapes,shapes
names=[a.name for a in bpy.data.actions]
for expected in ['Idle','Walk','CarryIdle','CarryWalk','Greet','BoxCut','Pry']:
 assert any(n.endswith('|'+expected) or n==expected for n in names),(expected,names)
for o in meshes:
 for v in o.data.vertices:
  assert all(math.isfinite(c) for c in v.co)
  assert abs(sum(g.weight for g in v.groups)-1)<.002, (o.name,v.index)
for action in bpy.data.actions:
 if action.id_root != 'OBJECT': continue
 rig.animation_data.action=action
 for frame in [action.frame_range[0],sum(action.frame_range)/2,action.frame_range[1]]:
  bpy.context.scene.frame_set(int(frame))
  for b in rig.pose.bones:assert all(math.isfinite(c) for row in b.matrix for c in row)
result={'fbx_roundtrip':'passed','mesh_count':len(meshes),'bones':len(rig.data.bones),'blendshapes':shapes,'actions':names,'weights_normalized':True,'finite_animation_samples':True,'unity_editor_tested':False}
with open(root+'/Documentation/Bruno/fbx-validation.json','w') as f:json.dump(result,f,indent=2)
print(json.dumps(result,indent=2))
