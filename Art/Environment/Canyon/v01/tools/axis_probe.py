import bpy
from mathutils import Vector
from pathlib import Path

root = Path('F:/UnityRepositories/UC_BallGame')
collection = bpy.data.collections.get('Canyon Axis Verification')
if collection is None:
    collection = bpy.data.collections.new('Canyon Axis Verification')
    bpy.context.scene.collection.children.link(collection)
for obj in list(collection.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
vertices, faces = [], []
centers = [(0, 0, 0), (1, 0, 0), (0, 3, 0), (0, 0, 2)]
for x, y, z in centers:
    base = len(vertices)
    for dx, dy, dz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
        # Empirically verified FBX convention: Unity (X,Y,Z) -> Blender (X,Z,Y).
        ux, uy, uz = x+dx*.05, y+dy*.05, z+dz*.05
        vertices.append((ux, uz, uy))
    for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:
        faces.append(tuple(base+i for i in reversed(f)))
mesh = bpy.data.meshes.new('AxisProbe')
mesh.from_pydata(vertices, [], faces)
mesh.update()
obj = bpy.data.objects.new('AxisProbe', mesh)
collection.objects.link(obj)
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
target = root/'Assets/_Game/Art/Environment/Canyon/Models/AxisProbe.fbx'
bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', use_space_transform=True, bake_space_transform=True,
    mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
result = {'file': str(target), 'expected_unity_bounds': {'min':[-.05,-.05,-.05], 'max':[1.05,3.05,2.05]}, 'blender_version':bpy.app.version_string}
