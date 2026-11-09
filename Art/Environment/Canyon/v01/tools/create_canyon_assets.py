"""Build the authored low-poly canyon kit in Blender, then export tested Unity FBXs.

Semantic coordinates in this file are Unity coordinates (X right, Y up, Z forward).
Blender storage uses (X,Z,Y); face winding is reversed for that reflection.
The axis_probe round trip verifies this convention against the actual Unity importer.
"""
import bpy
import bmesh
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path('F:/UnityRepositories/UC_BallGame')
SOURCE = ROOT/'Art/Environment/Canyon/v01'
EXPORT = ROOT/'Assets/_Game/Art/Environment/Canyon/Models'
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)

def linear(c):
    return c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4

def rgb(hexcode):
    return tuple(linear(int(hexcode[i:i+2],16)/255) for i in (0,2,4))

def mix(a,b,t):
    return tuple(x*(1-t)+y*t for x,y in zip(a,b))

SAND=rgb('EDBD83'); STONE=rgb('CF916D'); LIGHT=rgb('F2CC9A')
LEAF=rgb('7B9B54'); LEAFTIP=rgb('C0CA72'); TEAL=rgb('4C9991'); GOLD=rgb('F6D69A')
scene = bpy.data.scenes.get('Canyon Asset Studio')
if scene is None:
    scene = bpy.data.scenes.new('Canyon Asset Studio')
bpy.context.window.scene = scene
for obj in list(scene.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
collection = scene.collection
material = bpy.data.materials.get('Canyon Vertex Palette') or bpy.data.materials.new('Canyon Vertex Palette')
material.use_nodes=True
nodes=material.node_tree.nodes
nodes.clear()
out=nodes.new('ShaderNodeOutputMaterial')
shader=nodes.new('ShaderNodeBsdfPrincipled')
shader.inputs['Roughness'].default_value=.86
shader.inputs['Specular IOR Level'].default_value=.15
color=nodes.new('ShaderNodeVertexColor'); color.layer_name='Col'
material.node_tree.links.new(color.outputs['Color'],shader.inputs['Base Color'])
material.node_tree.links.new(shader.outputs['BSDF'],out.inputs['Surface'])

class Shape:
    def __init__(self):
        self.verts=[]; self.faces=[]; self.colors=[]
    def add(self,verts,faces,color):
        offset=len(self.verts)
        self.verts.extend(verts)
        for i,face in enumerate(faces):
            self.faces.append(tuple(offset+j for j in face))
            self.colors.append(color[i] if isinstance(color,list) else color)
    def box(self,center,size,color):
        x,y,z=center; a,b,c=[v*.5 for v in size]
        self.add([(x+dx*a,y+dy*b,z+dz*c) for dx,dy,dz in
            [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],
            [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],color)
    def extrude(self,points,z0,z1,color):
        n=len(points)
        self.add([(x,y,z0) for x,y in points]+[(x,y,z1) for x,y in points],
            [tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],color)
    def finish(self,name,bevel=0,smooth=False):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata([(x,z,y) for x,y,z in self.verts],[],[tuple(reversed(f)) for f in self.faces])
        mesh.update()
        col=mesh.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='CORNER')
        for polygon,shade in zip(mesh.polygons,self.colors):
            for li in polygon.loop_indices: col.data[li].color=(*shade,1)
        uv=mesh.uv_layers.new(name='UVMap')
        for polygon in mesh.polygons:
            for li in polygon.loop_indices:
                v=mesh.vertices[mesh.loops[li].vertex_index].co
                uv.data[li].uv=(v.x*.25,v.y*.25)
            polygon.use_smooth=smooth
        obj=bpy.data.objects.new(name,mesh); collection.objects.link(obj)
        obj.data.materials.append(material)
        if bevel:
            bpy.context.view_layer.objects.active=obj
            mod=obj.modifiers.new('Soft stone edges','BEVEL'); mod.width=bevel; mod.segments=2
            bpy.ops.object.modifier_apply(modifier=mod.name)
        bm=bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free()
        obj.data.update()
        return obj

assets=[]
def register(shape,name,bevel=0,smooth=False):
    obj=shape.finish(name,bevel,smooth); assets.append(obj); return obj

# Flat top at exactly Y=0. Borders fit a 6m repeat along Unity Z.
s=Shape(); rng=random.Random(110)
for iz in range(4):
    for ix in range(8):
        x=-7+ix*2; z=iz*2
        if 0<iz<3 and 0<ix<7: x+=rng.uniform(-.23,.23); z+=rng.uniform(-.23,.23)
        s.verts.append((x,0,z))
for iz in range(3):
    for ix in range(7):
        a=iz*8+ix; b=a+1; c=a+8; d=c+1
        for face in [(a,c,b),(b,c,d)]:
            s.faces.append(face); s.colors.append(mix(SAND,LIGHT,rng.uniform(.00,.10)))
register(s,'GroundTile')

def rock(name,width,height,depth,seed):
    s=Shape(); r=random.Random(seed); count=9
    rings=[(-.10,.77),(.12,1.0),(.57,.89),(.88,.60),(1,.20)]
    for k,(h,scale) in enumerate(rings):
        for i in range(count):
            angle=2*math.pi*i/count+.08*(k%2)
            rr=scale*r.uniform(.91,1.09)
            s.verts.append((math.cos(angle)*width*.5*rr+.07*width*h,
                max(-.1,height*h+r.uniform(-.025,.025)*height),math.sin(angle)*depth*.5*rr))
    s.faces.append(tuple(reversed(range(count)))); s.colors.append(mix(STONE,rgb('A86F5E'),.3))
    for k in range(len(rings)-1):
        for i in range(count):
            j=(i+1)%count
            face=(k*count+i,k*count+j,(k+1)*count+j,(k+1)*count+i)
            s.faces.append(face)
            s.colors.append(mix(STONE,LIGHT,.09+k*.10+r.uniform(-.04,.07)))
    s.faces.append(tuple(range((len(rings)-1)*count,len(rings)*count))); s.colors.append(mix(STONE,LIGHT,.52))
    return register(s,name)

rock('RockWide',2.25,.95,1.55,21)
rock('RockSmall',1.05,.52,.85,22)
rock('RockTall',1.65,2.55,1.5,23)
rock('CliffChunk',4.2,4.3,3.0,25)

# Low, scalloped sandy edge. Far smaller than the sight line to a crystal.
s=Shape(); r=random.Random(31); n=9
for i in range(n):
    z=i*.5
    for x,y in [(-.68,-.03),(-.30,.12),(.15,.24),(.62,.03)]:
        s.verts.append((x+r.uniform(-.07,.07),y+r.uniform(-.035,.035),z))
for i in range(n-1):
    for j in range(3):
        a=i*4+j
        s.faces.append((a,a+4,a+5,a+1)); s.colors.append(mix(SAND,LIGHT,.12+j*.05+r.uniform(-.03,.03)))
register(s,'TrailBank')

def succulent(name,seed,count):
    s=Shape(); r=random.Random(seed)
    for leaf in range(count):
        angle=leaf*2.399+r.uniform(-.2,.2)
        spread=r.uniform(.29,.48) if leaf<count-2 else .10
        height=r.uniform(.42,.76) if leaf<count-2 else .88
        start=len(s.verts); sides=7
        for k in range(5):
            t=k/5; width=math.sin(math.pi*(.10+.90*t))*.135
            cx=math.cos(angle)*spread*(t**1.5); cz=math.sin(angle)*spread*(t**1.5)
            for j in range(sides):
                p=j*2*math.pi/sides
                s.verts.append((cx+math.cos(p)*width, .025+height*t, cz+math.sin(p)*width))
        tip=len(s.verts)
        s.verts.append((math.cos(angle)*spread,.025+height,math.sin(angle)*spread))
        for k in range(4):
            for j in range(sides):
                a=start+k*sides+j; b=start+k*sides+(j+1)%sides
                s.faces.append((a,b,b+sides,a+sides)); s.colors.append(mix(LEAF,LEAFTIP,.06+k*.15+r.uniform(-.025,.025)))
        s.faces.append(tuple(reversed(range(start,start+sides)))); s.colors.append(LEAF)
        for j in range(sides):
            s.faces.append((start+4*sides+j,start+4*sides+(j+1)%sides,tip)); s.colors.append(LEAFTIP)
    return register(s,name,smooth=True)
succulent('SucculentA',45,8); succulent('SucculentB',46,6)

s=Shape(); r=random.Random(62)
for blade in range(9):
    angle=blade*2.4; height=r.uniform(.22,.46); dx=math.cos(angle)*.22; dz=math.sin(angle)*.22
    width=.035
    s.add([(-width,0,-.012),(width,0,-.012),(width,0,.012),(-width,0,.012),(dx,height,dz)],
          [(0,3,2,1),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],mix(rgb('BAA05E'),GOLD,r.uniform(.1,.5)))
register(s,'GrassTuft')

# Faceted voussoirs and rounded footings form a readable, friendly entrance.
s=Shape()
for side in (-1,1):
    s.box((side*1.86,1.03,0),(.56,2.06,.64),mix(STONE,LIGHT,.27))
    s.box((side*1.86,.14,0),(.82,.28,.82),mix(STONE,LIGHT,.45))
    s.box((side*1.86,1.96,0),(.69,.22,.78),LIGHT)
for i in range(11):
    a=i*math.pi/11+.012; b=(i+1)*math.pi/11-.012
    points=[(math.cos(a)*1.57,2.03+math.sin(a)*1.57),(math.cos(a)*2.15,2.03+math.sin(a)*2.15),
            (math.cos(b)*2.15,2.03+math.sin(b)*2.15),(math.cos(b)*1.57,2.03+math.sin(b)*1.57)]
    s.extrude(points,-.34,.34,mix(STONE,LIGHT,.34+.10*math.sin(i*1.7)))
# Keystone and inset mint gem, facing the incoming player (-Unity Z).
s.extrude([(-.25,3.66),(.25,3.66),(.32,4.18),(-.32,4.18)],-.41,-.33,GOLD)
s.extrude([(0,3.77),(.13,3.96),(0,4.10),(-.13,3.96)],-.435,-.405,TEAL)
register(s,'ColonyGateFrame',.035)

for left,name in [(True,'ColonyDoorLeft'),(False,'ColonyDoorRight')]:
    s=Shape(); sign=1 if left else -1
    s.box((sign*.75,1.31,0),(1.49,2.62,.20),mix(TEAL,rgb('345E63'),.24))
    s.box((sign*.75,1.32,-.12),(1.23,2.24,.065),TEAL)
    for y in (.20,2.43): s.box((sign*.75,y,-.17),(1.36,.12,.09),GOLD)
    for x in (.12,1.38): s.box((sign*x,1.31,-.17),(.09,2.34,.09),mix(GOLD,STONE,.15))
    points=[(sign*.75,1.94),(sign*.49,1.50),(sign*.51,1.28),(sign*.75,1.15),(sign*.99,1.28),(sign*1.01,1.50)]
    s.extrude(points,-.215,-.17,mix(GOLD,LIGHT,.2))
    register(s,name,.025)

s=Shape(); segments=24
for i in range(segments):
    a=i*2*math.pi/segments; b=(i+1)*2*math.pi/segments
    def p(radius,y,t): return (math.cos(t)*radius,y,math.sin(t)*radius)
    s.add([p(1.40,.04,a),p(1.40,.04,b),p(1.35,.25,b),p(1.35,.25,a),
           p(1.08,.04,a),p(1.08,.04,b),p(1.10,.25,b),p(1.10,.25,a)],
          [(0,1,2,3),(3,2,6,7),(7,6,5,4),(4,5,1,0)],mix(LIGHT,GOLD,.18+.09*math.sin(i*1.7)))
register(s,'ColonyBasin',.015)

s=Shape(); n=24
s.verts=[(0,.012,0)]+[(math.cos(i*2*math.pi/n)*1.12,.012,math.sin(i*2*math.pi/n)*1.12) for i in range(n)]
for i in range(n): s.faces.append((0,1+(i+1)%n,1+i)); s.colors.append(mix(LIGHT,SAND,.25))
s.extrude([(0,.37),(.18,.09),(.16,-.05),(0,-.14),(-.16,-.05),(-.18,.09)],-.02,.02,TEAL)
# The ground relief is horizontal; rotate the droplet's vertices from the XY plane onto XZ.
for i in range(n+1,len(s.verts)):
    x,y,z=s.verts[i]; s.verts[i]=(x,.026+z,y)
register(s,'StartMedallion')

manifest={'version':1,'blender':bpy.app.version_string,'units':'meters',
    'axisConvention':'Blender (x,z,y) stores semantic Unity (x,y,z); FBX -Z forward/Y up; Unity bakeAxisConversion=true',
    'assets':[]}
for index,obj in enumerate(assets):
    if any(p.area<1e-10 for p in obj.data.polygons):
        raise RuntimeError(obj.name+': degenerate geometry')
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    obj.location=(0,0,0); obj.rotation_euler=(0,0,0); obj.scale=(1,1,1)
    target=EXPORT/(obj.name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        use_space_transform=True,bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=True,
        add_leaf_bones=False,bake_anim=False,colors_type='LINEAR')
    coords=[(v.co.x,v.co.z,v.co.y) for v in obj.data.vertices]
    low=[min(v[k] for v in coords) for k in range(3)]; high=[max(v[k] for v in coords) for k in range(3)]
    obj.data.calc_loop_triangles()
    manifest['assets'].append({'name':obj.name,'file':str(target.relative_to(ROOT)).replace('\\','/'),
        'min':low,'max':high,'triangles':len(obj.data.loop_triangles),'vertices':len(obj.data.vertices)})
    # Display layout only, applied after FBX export. Each exported root remains identity.
    span=max(high[k]-low[k] for k in (0,2)); scale=min(1,2.8/max(.01,span))
    obj.scale=(scale,)*3
    obj.location=((index%5)*3.8,(index//5)*5.0,0)

(SOURCE/'asset-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
world=scene.world or bpy.data.worlds.new('Canyon Studio World'); scene.world=world
world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.64,.74,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
light_data=bpy.data.lights.new('Studio Sun','SUN'); light_data.energy=2.2; light_data.angle=.12
light=bpy.data.objects.new('Studio Sun',light_data); collection.objects.link(light); light.rotation_euler=(.4,-.5,-.4)
cam_data=bpy.data.cameras.new('Kit Overview'); cam=bpy.data.objects.new('Kit Overview',cam_data); collection.objects.link(cam)
cam.location=(16,-19,23); target=Vector((7.5,5.5,0)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam_data.type='ORTHO'; cam_data.ortho_scale=23; scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.render.resolution_x=1400; scene.render.resolution_y=1050; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_distance=24
            area.spaces.active.region_3d.view_location=(7.5,5.5,0)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'UC_Canyon_Kit.blend'))
result={'blend':str(SOURCE/'UC_Canyon_Kit.blend'),'count':len(assets),
        'triangles':sum(a['triangles'] for a in manifest['assets']),'assets':[a['name'] for a in manifest['assets']]}
