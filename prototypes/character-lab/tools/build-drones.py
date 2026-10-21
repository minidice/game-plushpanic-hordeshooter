"""Blender toy drones; same belly socket and independently replaceable payloads."""
import sys
import math
import importlib.util
from pathlib import Path
import bpy

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('pet_builder',HERE/'build-pets.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
b.ASSETS=b.ROOT/'game/assets/drones';b.OUT=b.ROOT/'prototypes/character-lab/output/drones'
b.ASSETS.mkdir(parents=True,exist_ok=True);b.OUT.mkdir(parents=True,exist_ok=True)

def sphere(p,n,at,scale,m,segments=32,rings=20):
    return b.ball(p,n,at,scale,m,segments=segments,rings=rings)

def ring(p,n,at,radius,thickness,m,axis='Y'):
    bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=10,major_radius=radius,minor_radius=thickness)
    return b.finish(bpy.context.object,n,p,at,(1,1,1),m,(math.pi/2,0,0) if axis=='Y' else (0,0,0))

def palette(color):
    return {k:b.material(k,c,metal=.25 if k in ['metal','dark'] else .05,rough=.25 if k=='glass' else .5)
            for k,c in dict(main=color,cream='eee6d1',dark='253545',metal='738b9c',glass='5ac6d7',light='b8f4ec',accent='efb650').items()}

def lens(p,at,m,r=.11):
    x,y,z=at
    b.tube(p,'LensRim',at,r,.06,m['dark'])
    sphere(p,'Lens',(x,y,z+.035),(r*.79,r*.79,.035),m['glass'],24,12)
    sphere(p,'LensGlint',(x-r*.24,y+r*.27,z+.063),(.018,.025,.01),m['light'],12,8)

def rotor(p,index,at,m,r=.29):
    ring(p,'SafetyRing'+str(index),at,r+.04,.035,m['cream'])
    b.tube(p,'Motor'+str(index),at,.075,.14,m['dark'],'Y')
    pivot=b.empty('Rotor'+str(index),p,at)
    for angle in [0,math.pi/2]:
        blade=sphere(pivot,'RotorBlade',(0,.025,0),(r,.025,.052),m['metal'],24,10)
        blade.rotation_euler.y=angle
    sphere(pivot,'RotorHub',(0,.045,0),(.085,.04,.085),m['accent'],20,10)

def body(kind):
    m=palette({'quadcopter':'6199b5','ladybug':'d95749','bee':'efbb4f','ufo':'9bb6be'}[kind])
    root=b.empty(kind);body=b.empty('Body',root)
    root['mount_contract']='drone_belly_v1'
    # Every chassis has the same flush, downward-facing attachment disc.
    b.tube(body,'SocketHousing',(0,-.165,0),.20,.11,m['dark'],'Y')
    b.empty('EquipmentSocket',body,(0,-.22,0))['mount_contract']='drone_belly_v1'
    if kind=='quadcopter':
        sphere(body,'MainHull',(0,.06,0),(.40,.25,.49),m['main'])
        b.box(body,'Battery',(0,.23,-.08),(.38,.14,.43),m['cream'],.06)
        for i,(x,z) in enumerate([(-.65,-.52),(.65,-.52),(-.65,.52),(.65,.52)]):
            b.line(body,'Arm'+str(i),[(x*.35,.03,z*.35),(x*.7,.02,z*.7),(x,.04,z)],m['main'],.065)
            rotor(body,i,(x,.08,z),m)
        lens(body,(0,.02,.48),m,.15)
        for x in [-.27,.27]:
            b.line(body,'LandingSkid',[(x,-.08,-.27),(x,-.27,-.32),(x,-.29,.29),(x,-.18,.34)],m['dark'],.025)
        for z in [-.16,-.04,.08]:b.box(body,'BatteryVent',(0,.31,z),(.20,.012,.022),m['dark'],.004)
    elif kind=='ladybug':
        sphere(body,'UnderShell',(0,.04,-.05),(.57,.20,.61),m['dark'])
        for side in [-1,1]:
            shell=sphere(body,'WingCover',(side*.24,.18,-.10),(.33,.31,.59),m['main'])
            for z in [-.39,-.08,.23]:
                x=side*(.31 if z<.2 else .25)
                y=.18+.31*math.sqrt(max(0,1-((x-side*.24)/.33)**2-((z+.10)/.59)**2))
                sphere(body,'ShellSpot',(x,y-.008,z),(.105,.018,.11),m['dark'],20,10)
            rotor(body,0 if side<0 else 1,(side*.78,.06,-.04),m,.20)
        b.line(body,'ShellSeam',[(0,.22,-.68),(0,.47,-.3),(0,.49,0),(0,.38,.43)],m['dark'],.012)
        sphere(body,'Face',(0,.10,.53),(.32,.23,.23),m['dark'])
        for x in [-.145,.145]:
            lens(body,(x,.13,.72),m,.08)
            b.line(body,'Antenna',[(x,.24,.53),(x*1.4,.44,.65),(x*1.5,.47,.70)],m['metal'],.018)
            sphere(body,'AntennaTip',(x*1.5,.47,.70),(.045,.045,.045),m['accent'],16,10)
    elif kind=='bee':
        sphere(body,'Abdomen',(0,.06,-.22),(.34,.29,.57),m['main'])
        for z,r in [(-.50,.24),(-.28,.325),(-.04,.32)]:ring(body,'Stripe',(0,.06,z),r,.035,m['dark'],'Z')
        sphere(body,'Face',(0,.11,.39),(.33,.30,.30),m['main'])
        for x in [-.14,.14]:
            lens(body,(x,.15,.65),m,.08)
            b.line(body,'Antenna',[(x,.34,.39),(x*1.4,.51,.41),(x*1.6,.54,.50)],m['dark'],.018)
            sphere(body,'AntennaTip',(x*1.6,.54,.50),(.045,.045,.045),m['accent'],16,10)
        for side in [-1,1]:
            pivot=b.empty('WingLeft' if side<0 else 'WingRight',body,(side*.25,.24,-.10))
            wing=sphere(pivot,'Wing',(side*.38,.02,-.12),(.45,.032,.24),m['cream'])
            wing.rotation_euler.y=side*.3
            b.line(pivot,'WingVein',[(0,.045,0),(side*.32,.055,-.08),(side*.69,.05,-.23)],m['metal'],.012)
        b.tube(body,'TailJet',(0,.06,-.79),.10,.14,m['dark'])
        b.tube(body,'JetCore',(0,.06,-.865),.065,.01,m['glass'])
    else:
        sphere(body,'Saucer',(0,.015,0),(.86,.17,.86),m['main'],48,24)
        ring(body,'Rim',(0,.02,0),.85,.045,m['dark'])
        sphere(body,'GlassDome',(0,.17,0),(.43,.31,.43),m['glass'],40,24)
        ring(body,'DomeSeal',(0,.18,0),.41,.035,m['cream'])
        carousel=b.empty('RimSpin',body)
        for i in range(8):
            a=i*math.tau/8
            sphere(carousel,'RimLamp',(math.cos(a)*.76,.085,math.sin(a)*.76),(.065,.045,.065),m['light'],16,10)
        for x in [-.11,.11]:lens(body,(x,.20,.41),m,.065)
        b.tube(body,'TopCap',(0,.47,0),.065,.06,m['accent'],'Y')
    return root

def equipment(kind):
    m=palette({'light':'e4c568','laser':'66c5c2','blaster':'859aaf','support':'93c6a8'}[kind])
    root=b.empty(kind);root['mount_contract']='drone_belly_v1'
    b.tube(root,'MountDisc',(0,-.025,0),.18,.05,m['dark'],'Y')
    yaw=b.empty('Yaw',root,(0,-.065,0))
    b.box(yaw,'Hanger',(0,-.05,0),(.25,.12,.17),m['metal'],.025)
    pitch=b.empty('Pitch',yaw,(0,-.16,0))
    if kind=='light':
        b.box(pitch,'LampCase',(0,0,.07),(.35,.26,.30),m['main'],.06)
        b.tube(pitch,'Reflector',(0,0,.24),.13,.07,m['cream'])
        b.tube(pitch,'Lens',(0,0,.28),.105,.012,m['light'])
    elif kind=='laser':
        b.box(pitch,'EmitterCase',(0,0,.03),(.25,.20,.38),m['main'])
        for z in [-.10,-.03,.04]:b.box(pitch,'CoolingFin',(0,.12,z),(.28,.025,.025),m['dark'],.006)
        b.tube(pitch,'Emitter',(0,0,.29),.085,.18,m['dark'])
        b.tube(pitch,'Crystal',(0,0,.385),.055,.02,m['glass'])
    elif kind=='blaster':
        b.box(pitch,'Receiver',(0,0,0),(.32,.22,.25),m['main'])
        for x in [-.095,.095]:
            b.tube(pitch,'Barrel',(x,0,.26),.045,.31,m['metal'])
            b.tube(pitch,'MuzzleRing',(x,0,.42),.06,.07,m['dark'])
        b.box(pitch,'Magazine',(0,-.14,-.04),(.23,.12,.16),m['accent'],.025)
    else:
        b.box(pitch,'SupportPod',(0,0,.04),(.36,.26,.33),m['main'],.055)
        b.box(pitch,'Indicator',(0,0,.212),(.21,.14,.012),m['dark'],.02)
        b.box(pitch,'PlusVertical',(0,0,.225),(.035,.10,.012),m['light'],.005)
        b.box(pitch,'PlusHorizontal',(0,0,.225),(.105,.035,.012),m['light'],.005)
        for x in [-.21,.21]:b.tube(pitch,'Canister',(x,0,.01),.065,.24,m['cream'],'Y')
    b.empty('Muzzle',pitch,(0,0,.47))
    return root

def main():
    bodies=['quadcopter','ladybug','bee','ufo'];modules=['light','laser','blaster','support']
    requested=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else bodies+modules
    for name in requested:
        if name not in bodies+modules:raise ValueError(name)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        b.export(body(name) if name in bodies else equipment(name),name)
        if name in bodies:
            for obj in bpy.context.scene.objects:
                if obj.type!='MESH':continue
                bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
                mod=obj.modifiers.new('Low quality','DECIMATE');mod.ratio=.30
                bpy.ops.object.modifier_apply(modifier=mod.name)
            bpy.ops.object.select_all(action='SELECT')
            bpy.ops.export_scene.gltf(filepath=str(b.ASSETS/f'{name}_low.glb'),export_format='GLB',use_selection=True,export_yup=True,export_extras=True,export_animations=False)
            bpy.ops.wm.save_as_mainfile(filepath=str(b.OUT/f'{name}_low.blend'))

if __name__=='__main__':main()
