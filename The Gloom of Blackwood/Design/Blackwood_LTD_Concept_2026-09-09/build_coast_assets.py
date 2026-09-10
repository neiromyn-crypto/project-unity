"""Headless Blender source for original Blackwood additions. Existing user models are untouched."""
import bpy, math, os
from mathutils import Vector

ROOT = r'D:\project unity\The Gloom of Blackwood'
OUT = os.path.join(ROOT, 'Assets', 'DawnGuard', 'BlackwoodLTD', 'Art', 'Models')
SRC = os.path.join(ROOT, 'Design', 'Blackwood_LTD_Concept_2026-09-09', 'Blender')
os.makedirs(OUT, exist_ok=True)
os.makedirs(SRC, exist_ok=True)

def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    global mats
    mats = {}
    for name, rgb, metal in [('Ivory',(.76,.79,.72),.3),('Orange',(.95,.31,.055),.45),('Dark',(.045,.085,.105),.65),('Steel',(.23,.31,.32),.7),('Cyan',(.06,.83,.88),.3),('Wood',(.27,.16,.07),0),('Stone',(.31,.37,.40),.1),('Leaf',(.16,.29,.12),0)]:
        m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=.45
        mats[name]=m

def empty(name,loc=(0,0,0),parent=None):
    obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);obj.location=loc;obj.parent=parent;return obj

def box(name,loc,scale,mat='Dark',bevel=.035,parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc);o=bpy.context.object;o.name=name+'_'+mat;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Manufactured edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    o.data.materials.append(mats[mat]);o.parent=parent;return o

def cylinder(name,loc,radius,depth,mat='Steel',vertices=12,parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    o=bpy.context.object;o.name=name+'_'+mat;o.data.materials.append(mats[mat]);o.parent=parent;return o

def export(name):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC,name+'.blend'))
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,name+'.fbx'),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)

reset()
root=empty('WorkerRoot')
box('Body',(0,0,.78),(.53,.37,.43),'Ivory',.055,root)
box('Cap',(0,0,1.015),(.57,.41,.10),'Orange',.03,root)
box('Visor',(0,-.195,.81),(.32,.035,.16),'Dark',.015,root)
box('EyeLeft',(-.09,-.219,.82),(.065,.025,.055),'Cyan',.008,root)
box('EyeRight',(.09,-.219,.82),(.065,.025,.055),'Cyan',.008,root)
box('Belt',(0,0,.56),(.47,.36,.12),'Dark',.015,root)
box('Backpack',(0,.25,.75),(.43,.23,.36),'Orange',.025,root)
for x,side in [(-.17,'Left'),(.17,'Right')]:
    p=empty('Leg'+side,(x,0,.50),root)
    box('Hip',(0,0,-.09),(.14,.17,.22),'Steel',.022,p)
    box('Shin',(0,-.015,-.26),(.17,.18,.20),'Orange',.025,p)
    box('Boot',(0,-.06,-.41),(.22,.33,.12),'Dark',.02,p)
for x,side in [(-.34,'Left'),(.34,'Right')]:
    p=empty('Arm'+side,(x,0,.89),root)
    cylinder('Shoulder',(0,0,0),.115,.16,'Orange',12,p)
    box('Arm',(0,0,-.19),(.12,.15,.29),'Steel',.02,p)
    box('Hand',(0,-.055,-.35),(.16,.19,.14),'Dark',.018,p)
cargo=empty('Cargo',(0,-.40,.44),root)
box('CarryBox',(0,0,0),(.50,.38,.29),'Dark',.02,cargo)
for x in [-.2,.2]:box('CrateEdge',(x,0,.02),(.045,.40,.33),'Orange',.008,cargo)
for x,y,z in [(-.13,-.03,.17),(.12,.04,.20),(0,-.08,.23)]:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.16,location=(x,y,z));o=bpy.context.object;o.name='CargoStone_Stone';o.data.materials.append(mats['Stone']);o.parent=cargo
export('CoastWorker')

reset();root=empty('GuardianRoot')
cylinder('Foundation',(0,0,.10),.64,.2,'Dark',8,root)
cylinder('CopperRing',(0,0,.23),.57,.09,'Orange',12,root)
cylinder('Plinth',(0,0,.37),.43,.2,'Steel',8,root)
verts=[]
for z,r in [(.47,.43),(1.08,.28),(1.39,.34)]:
    for i in range(8):a=i*math.pi/4;verts.append((math.cos(a)*r,math.sin(a)*r,z))
faces=[]
for j in range(2):
    for i in range(8):faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
faces += [tuple(range(7,-1,-1)),tuple(range(16,24))]
mesh=bpy.data.meshes.new('CoatMesh');mesh.from_pydata(verts,[],faces);mesh.materials.append(mats['Dark']);o=bpy.data.objects.new('Robe_Dark',mesh);bpy.context.collection.objects.link(o);o.parent=root
box('ChestPlate',(0,-.25,1.17),(.33,.12,.31),'Ivory',.04,root)
box('Core',(0,-.325,1.18),(.11,.045,.17),'Cyan',.012,root)
box('Hood',(0,0,1.60),(.48,.40,.48),'Ivory',.09,root)
box('Face',(0,-.218,1.59),(.32,.04,.25),'Dark',.03,root)
box('Eyes',(0,-.244,1.61),(.24,.025,.035),'Cyan',.007,root)
for x in [-.40,.40]:
    box('Shoulder',(x,0,1.35),(.25,.34,.24),'Orange',.04,root)
    arm=box('Forearm',(x,-.20,1.12),(.17,.21,.41),'Ivory',.03,root);arm.rotation_euler[0]=-.5
    box('Hand',(x,-.34,1.01),(.18,.17,.14),'Steel',.025,root)
orb=empty('Focus',(0,-.42,1.05),root)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.21);o=bpy.context.object;o.name='Crystal_Cyan';o.parent=orb;o.location=(0,0,0);o.scale=(.7,.7,1.4);o.data.materials.append(mats['Cyan'])
export('CoastGuardian')

reset();root=empty('DockRoot')
for i in range(9):box('Plank',(0,i*.28, .15),(1.55,.255,.11),'Wood',.015,root)
for x in [-.66,.66]:
    box('Rail',(x,1.1,.02),(.12,2.6,.18),'Dark',.008,root)
    for y in [0,1.12,2.24]:cylinder('Post',(x,y,-.09),.09,.94,'Wood',8,root)
export('CoastDock')

reset();root=empty('LanternRoot')
cylinder('Foot',(0,0,.08),.23,.16,'Dark',8,root)
cylinder('Pole',(0,0,.74),.055,1.35,'Steel',8,root)
box('Housing',(0,0,1.42),(.25,.25,.31),'Orange',.025,root)
box('Glass',(0,-.135,1.42),(.15,.035,.20),'Cyan',.008,root)
cylinder('Lid',(0,0,1.60),.20,.07,'Dark',8,root)
export('CoastLantern')
reset();root=empty('BasaltRoot')
for i,(x,y,z,sx,sy,sz) in enumerate([(-.25,0,.35,.55,.7,.85),(.26,.13,.24,.5,.6,.61),(0,-.31,.15,.6,.4,.37)]):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(x,y,z));o=bpy.context.object;o.name='Basalt_Stone';o.scale=(sx,sy,sz);o.rotation_euler=(.12*i,.16*i,.73*i);o.data.materials.append(mats['Stone']);o.parent=root
export('CoastBasalt')
reset();root=empty('OreRoot')
for i in range(5):
    a=i*2.4;bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.5,location=(math.sin(a)*.43,math.cos(a)*.32,.25));o=bpy.context.object;o.name='OreBase_Stone';o.scale=(1,.8,.8);o.data.materials.append(mats['Stone']);o.parent=root
for i in range(4):
    a=i*2.4;bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.22,location=(math.sin(a)*.42,math.cos(a)*.26,.57));o=bpy.context.object;o.name='OreVein_Cyan';o.scale=(.55,.6,1.6);o.data.materials.append(mats['Cyan']);o.parent=root
export('CoastOre')
reset();root=empty('FernRoot')
for i in range(7):
    a=i*2.4;r=.22+i*.013;mesh=bpy.data.meshes.new('Frond');mesh.from_pydata([(0,0,0),(math.cos(a+.3)*r,math.sin(a+.3)*r,.20),(math.cos(a)*r*1.7,math.sin(a)*r*1.7,.16),(math.cos(a-.3)*r,math.sin(a-.3)*r,.2),(math.cos(a)*r*.6,math.sin(a)*r*.6,.38)],[],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)]);mesh.materials.append(mats['Leaf']);o=bpy.data.objects.new('Fern_Leaf',mesh);bpy.context.collection.objects.link(o);o.parent=root
export('CoastFern')
print('BLACKWOOD_COAST_ASSETS_OK', OUT)
