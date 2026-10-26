"""Clean tiger form, built through the common-monster Blender authoring API."""
import math

import bpy
from mathutils import Vector


def _section(profiles, z):
    for low, high in zip(profiles, profiles[1:]):
        if low[0] <= z <= high[0]:
            t = (z-low[0]) / (high[0]-low[0])
            return tuple(a+(b-a)*t for a,b in zip(low[1:], high[1:]))
    raise ValueError(f'stripe outside silhouette: {z}')


def _stripe(api, name, profiles, points, width, material, parent):
    """A featherweight surface ribbon, sampled on the loft instead of a raised tube."""
    vertices=[];faces=[];across=7
    for j,(z,angle) in enumerate(points):
        rx,ry,cy=_section(profiles,z)
        span=width[j] if isinstance(width,(tuple,list)) else width
        for i in range(across):
            a=angle+span*(2*i/(across-1)-1)
            vertices.append(((rx+.012)*math.sin(a),cy-(ry+.012)*math.cos(a),z))
    for j in range(len(points)-1):
        for i in range(across-1):
            k=across*j+i;faces.append((k,k+1,k+across+1,k+across))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    api.parent_world(obj,parent)
    return obj


def _ear(api, side, head, outer, inner):
    """Round ear with a shallow recessed center and narrow colored rim."""
    cx=side*.327;cy=-.036;cz=1.808
    verts=[];faces=[];sides=16
    for scale,y in ((.87,cy+.012),(.88,cy-.055),(.65,cy-.090),(.22,cy-.067)):
        for i in range(sides):
            a=math.tau*i/sides
            verts.append((cx+.119*scale*math.cos(a),y,cz+.126*scale*math.sin(a)))
    for ring in range(3):
        for i in range(sides):
            a=ring*sides+i;b=ring*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(range(3*sides,4*sides)))
    mesh=bpy.data.meshes.new('cupped_tiger_ear');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('cupped_tiger_ear',mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(outer);obj.data.materials.append(inner)
    for face in mesh.polygons:
        face.material_index=1 if face.index>=sides else 0
        face.use_smooth=True
    api.parent_world(obj,head)


def build(api):
    c=api.colors('tiger');root,deform,head,left,right,arms=api.frame('tiger')
    for key,roughness in [('primary',.76),('secondary',.78),('cream',.79),
                          ('eye',.64),('ink',.72),('vest',.9)]:
        c[key].node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=roughness
    body=[(.24,.11,.115,.015),(.34,.225,.205,.01),(.48,.325,.275,0),
          (.65,.385,.312,0),(.82,.385,.315,0),(.99,.335,.273,.01),
          (1.12,.275,.23,.02),(1.25,.19,.17,.025),(1.37,.07,.08,.025)]
    api.loft('continuous_tiger_body',body,c['primary'],deform,32)
    face=[(1.08,.145,.15,.018),(1.18,.235,.22,-.01),(1.31,.334,.281,-.035),
          (1.45,.382,.309,-.04),(1.59,.374,.302,-.027),
          (1.73,.292,.239,0),(1.83,.162,.145,.025),(1.87,.025,.03,.03)]
    api.loft('continuous_tiger_face',face,c['primary'],head,36)
    # Skin pattern rides the continuous torso; secondary goes on the tail tip.
    for side in (-1,1):
        for j in range(4):
            z=.49+j*.125
            _stripe(api,'tiger_flank_stripe',body,[(z-.075,side*1.77),
                (z-.025,side*1.43),(z+.035,side*1.10),(z+.090,side*1.15)],
                .065,c['ink'],deform)
        for j in range(2):
            z=.66+j*.19
            _stripe(api,'tiger_rear_stripe',body,[(z-.09,side*2.76),
                (z-.025,side*2.53),(z+.06,side*2.40)],.052,c['ink'],deform)
        _ear(api,side,head,c['primary'],c['mane'])
        api.oval('small_almond_eye',(side*.198,-.315,1.554),(.035,.016,.041),
                 c['eye'],head,16,8,rot=(0,0,side*.30))
        for j in range(2):
            z=1.32+j*.145
            _stripe(api,'tiger_cheek_stripe',face,[(z-.049,side*1.40),
                (z,side*1.15),(z+.052,side*.96)],.058,c['ink'],head)
            _stripe(api,'tiger_back_head_stripe',face,[(z-.045,side*2.72),
                (z,side*2.51),(z+.052,side*2.32)],.058,c['ink'],head)
        _stripe(api,'tiger_crown_stripe',face,[(1.69,side*.35),
            (1.76,side*.43),(1.815,side*.53)],.054,c['ink'],head)
    _stripe(api,'tiger_center_stripe',face,[(1.68,0),(1.76,.025),(1.82,.015)],
            .055,c['ink'],head)
    # Joined, broad muzzle with a short matte nose; all facial marks remain shallow.
    api.loft('joined_tiger_muzzle',[(1.213,.045,.025,-.299),
        (1.235,.135,.06,-.326),(1.29,.217,.083,-.343),
        (1.35,.230,.091,-.342),(1.418,.177,.065,-.330),
        (1.439,.065,.025,-.312)],c['cream'],head,32)
    api.feather('short_tiger_nose',[(0,-.383,1.43),(0,-.433,1.405),
        (0,-.439,1.377)],[.055,.063,.014],[.019,.022,.006],c['ink'],head,12)
    api.tube('tiger_philtrum',[(0,-.43,1.378),(0,-.428,1.331)],
             [.004,.004],c['ink'],head,6)
    api.tube('neutral_tiger_mouth',[(-.052,-.418,1.318),(0,-.427,1.325),
        (.052,-.418,1.318)],[.004]*3,c['ink'],head,6)
    for sign,foot,arm in zip((-1,1),(left,right),arms):
        leg=api.loft('tapered_tiger_leg',[(.077,.064,.076,-.005),
            (.14,.068,.08,.002),(.22,.084,.094,.01),(.32,.119,.124,.016),
            (.41,.13,.135,.018),(.475,.105,.105,.018)],c['primary'],foot,16)
        for v in leg.data.vertices:v.co.x+=sign*.205
        sole=api.loft('flat_tiger_foot',[(0,.105,.149,-.075),
            (.02,.122,.174,-.075),(.06,.126,.177,-.075),
            (.106,.112,.158,-.054),(.149,.081,.105,-.015),
            (.171,.056,.074,0)],c['primary'],foot,20)
        for v in sole.data.vertices:
            v.co.x+=sign*.22
            v.co.y=max(v.co.y,-.235)
        for polygon in sole.data.polygons:
            if all(abs(sole.data.vertices[i].co.z)<1e-6 for i in polygon.vertices):
                polygon.use_smooth=False
        forearm=api.loft('tiger_forearm',[(.77,.052,.052,-.024),
            (.83,.071,.065,-.019),(.91,.092,.078,-.008),
            (.985,.068,.06,0),(.996,.024,.025,0)],c['primary'],arm,16)
        for v in forearm.data.vertices:
            v.co.x+=sign*(.43-.04*max(0,min(1,(v.co.z-.79)/.20)))
        forearm.name=arm.name+'_mesh'
        side='left' if sign<0 else 'right'
        shoulder=api.empty(f'common_tiger_{side}Shoulder',arm)
        shoulder.location=(0,0,0)
        paw=api.empty(f'common_tiger_{side}Paw');api.parent_world(paw,arm)
        wrist=api.empty(f'common_tiger_{side}Wrist',paw)
        wrist.location=(sign*.43,-.024,.79)
        grip=api.empty(f'common_tiger_{side}Grip',paw)
        grip.location=(sign*.43,-.067,.68)
        hand=api.loft('compact_tiger_paw',[(.606,.023,.026,-.04),
            (.635,.055,.045,-.05),(.674,.074,.055,-.058),
            (.718,.076,.051,-.05),(.766,.061,.045,-.033),
            (.813,.05,.047,-.024)],c['primary'],paw,16)
        for v in hand.data.vertices:v.co.x+=sign*.43
        hand.name=paw.name+'_mesh'
        api.oval('tucked_tiger_thumb',(sign*.372,-.087,.702),
                 (.033,.037,.04),c['primary'],paw,12,8)
    tail=api.empty('common_tiger_tail',deform,(0,.24,.60))
    api.tube('curved_tiger_tail',[(0,.24,.60),(0,.32,.545),
        (.03,.41,.52),(.10,.48,.545),(.16,.52,.60),(.20,.52,.675),
        (.23,.49,.745)],[.052,.05,.046,.042,.037,.032,.027],c['primary'],tail,12)
    api.oval('tiger_tail_tip',(.23,.49,.752),(.039,.041,.073),c['secondary'],tail,12,8)
    vest=api.empty('Gear_Vest',deform)
    api.loft('fitted_tiger_vest',[(z,rx+.025,ry+.027,cy) for z,rx,ry,cy in body[2:7]],
             c['vest'],vest,32)
    for sign in (-1,1):
        api.parrot_box('tiger_pouch',(sign*.16,-.341,.72),
                       (.216,.104,.248),c['vest'],vest,sign*.28)
        api.parrot_box('tiger_pouch_lid',(sign*.16,-.391,.816),
                       (.219,.032,.059),c['vest'],vest,sign*.28,.008)
        api.tube('tiger_vest_shoulder',[(sign*.21,-.21,1.0),
            (sign*.265,-.15,1.075),(sign*.30,0,1.10),
            (sign*.26,.16,1.07),(sign*.21,.21,1.0)],[.027]*5,c['vest'],vest,6)
    api.parrot_box('tiger_vest_closure',(0,-.352,.75),
                   (.022,.018,.38),c['ink'],vest,bevel=.005)
    glasses=api.empty('Gear_Glasses',head)
    for sign in (-1,1):
        api.oval('sunglass_lens',(sign*.20,-.374,1.554),
                 (.105,.02,.10),c['ink'],glasses,12,8)
    api.tube('nose_bridge',[(-.10,-.374,1.554),(.10,-.374,1.554)],
             [.013,.013],c['ink'],glasses)
    return root
