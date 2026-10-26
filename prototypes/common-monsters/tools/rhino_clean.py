"""Clean, heavy rhinoceros silhouette using the common rig and semantic palette."""
import math
import bpy
import bmesh


def build(api):
    c=api.colors('rhino')
    root,deform,head,left,right,arms=api.frame('rhino')
    for key,rough in [('primary',.77),('secondary',.80),('eye',.67),('horn',.74),('ink',.76)]:
        c[key].node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=rough
    body=[(.25,.16,.15,.025),(.33,.29,.25,.025),(.46,.375,.31,.02),
          (.61,.42,.34,.018),(.79,.43,.35,.025),(.96,.405,.325,.035),
          (1.10,.34,.27,.045),(1.22,.27,.23,.045),(1.34,.21,.18,.035)]
    api.loft('continuous_rhino_body',body,c['primary'],deform,32)
    api.loft('broad_rhino_head',[(1.10,.20,.19,-.015),(1.18,.31,.265,-.035),
        (1.30,.405,.325,-.045),(1.44,.435,.35,-.035),(1.58,.425,.32,-.005),
        (1.72,.36,.27,.015),(1.82,.275,.205,.03),(1.88,.15,.12,.035),
        (1.90,.035,.035,.035)],c['primary'],head,32)
    # Broad planes of the joined muzzle rather than a ball attached below the eyes.
    api.loft('low_rhino_muzzle',[(1.13,.12,.055,-.29),(1.17,.255,.115,-.315),
        (1.23,.33,.16,-.33),(1.32,.345,.18,-.345),(1.41,.30,.155,-.35),
        (1.48,.19,.10,-.32),(1.51,.07,.04,-.29)],c['primary'],head,32)
    for sign in [-1,1]:
        api.oval('small_rhino_eye',(sign*.285,-.268,1.585),(.033,.019,.036),c['eye'],head,16,9)
        api.oval('shallow_nostril',(sign*.21,-.505,1.34),(.025,.007,.014),c['ink'],head,12,7)
        # A restrained brow ridge remains part of the animal's face, not eyewear.
        api.feather('low_brow_ridge',[(sign*.24,-.27,1.635),(sign*.29,-.254,1.644),
             (sign*.335,-.225,1.638)],[.008,.022,.004],[.008,.015,.004],c['primary'],head,8)
        ear=api.loft('leaf_rhino_ear',[(1.69,.025,.022,.055),(1.75,.065,.041,.055),
             (1.83,.087,.043,.052),(1.91,.061,.033,.045),(1.965,.008,.008,.04)],c['primary'],head,16)
        for v in ear.data.vertices:v.co.x+=sign*(.34+.13*(v.co.z-1.69)/.275)
        inset=api.loft('ear_inner_plane',[(1.77,.025,.008,.014),(1.82,.052,.009,.007),
             (1.88,.040,.008,.010),(1.924,.004,.004,.015)],c['secondary'],head,14)
        for v in inset.data.vertices:v.co.x+=sign*(.34+.13*(v.co.z-1.69)/.275)
    api.tube('large_rhino_horn',[(0,-.475,1.44),(0,-.49,1.53),(0,-.47,1.64),
        (0,-.43,1.77),(0,-.39,1.84)],[.102,.085,.057,.025,.002],c['horn'],head,16)
    api.tube('small_rhino_horn',[(0,-.288,1.68),(0,-.275,1.76),(0,-.255,1.81)],
        [.056,.032,.002],c['horn'],head,12)
    api.tube('quiet_mouth',[(-.17,-.45,1.205),(0,-.474,1.19),(.17,-.45,1.205)],
        [.0035,.004,.0035],c['ink'],head,8)

    for sign,foot,arm in zip([-1,1],[left,right],arms):
        side='left' if sign<0 else 'right'
        leg=api.loft('stocky_tapered_ankle',[(.08,.082,.083,-.008),(.17,.084,.087,.002),
            (.26,.10,.102,.012),(.36,.137,.13,.018),(.47,.125,.12,.02)],c['primary'],foot,18)
        for v in leg.data.vertices:v.co.x+=sign*.22
        sole=api.loft('flat_three_toed_foot',[(0,.105,.147,-.063),(.022,.13,.174,-.063),
            (.065,.135,.18,-.06),(.11,.12,.155,-.045),(.155,.084,.093,-.005)],c['primary'],foot,24)
        for v in sole.data.vertices:
            v.co.x+=sign*.22
            v.co.y=max(v.co.y,-.222)
        for poly in sole.data.polygons:
            if all(abs(sole.data.vertices[i].co.z)<1e-6 for i in poly.vertices):poly.use_smooth=False
        for dx in [-.073,0,.073]:
            api.parrot_box('small_toenail',(sign*.22+dx,-.217,.041),(.054,.021,.039),c['horn'],foot,bevel=.006)
        forearm=api.loft('rhino_forearm',[(.775,.065,.059,-.028),(.84,.082,.074,-.023),
            (.92,.105,.092,-.011),(.99,.085,.078,0),(1.05,.04,.04,0)],c['primary'],arm,18)
        for v in forearm.data.vertices:v.co.x+=sign*(.445-.055*max(0,min(1,(v.co.z-.80)/.25)))
        forearm.name=arm.name+'_mesh'
        # Original shoulder pivot remains at .99; explicit anatomical top avoids a stretched cap.
        shoulder=api.empty(f'common_rhino_{side}Shoulder')
        shoulder.location=(sign*.39,0,1.05);api.parent_world(shoulder,arm)
        paw_node=api.empty(f'common_rhino_{side}Paw');api.parent_world(paw_node,arm)
        api.empty(f'common_rhino_{side}Wrist',paw_node,(sign*.445,-.028,.795))
        api.empty(f'common_rhino_{side}Grip',paw_node,(sign*.435,-.115,.685))
        paw=api.loft('tapered_rhino_palm',[(.61,.034,.033,-.055),(.64,.072,.053,-.065),
            (.69,.086,.061,-.068),(.735,.089,.057,-.053),(.78,.072,.051,-.032),
            (.815,.06,.054,-.028)],c['primary'],paw_node,20)
        for v in paw.data.vertices:
            v.co.x+=sign*.445
            # Broad, shallow dorsal plane rather than a spherical mitten.
            v.co.y=min(v.co.y,0)
        paw.name=paw_node.name+'_mesh'
    tail=api.empty('common_rhino_tail',deform)
    api.tube('short_rhino_tail',[(0,.29,.66),(.025,.40,.57),(.045,.44,.49)],
        [.037,.032,.018],c['primary'],tail,12)
    api.feather('small_tail_tip',[(.045,.44,.51),(.048,.45,.46),(.05,.45,.43)],
        [.026,.033,.006],[.02,.026,.004],c['secondary'],tail,10)
    vest=api.empty('Gear_Vest',deform)
    api.loft('fitted_rhino_vest',[(z,rx+.026,ry+.029,cy) for z,rx,ry,cy in body[2:7]],c['vest'],vest,32)
    for sign in [-1,1]:
        api.parrot_box('rhino_rectangular_pouch',(sign*.18,-.371,.76),(.235,.105,.255),c['vest'],vest,sign*.22)
        api.parrot_box('pouch_lid',(sign*.18,-.423,.855),(.24,.027,.065),c['vest'],vest,sign*.22,.008)
        api.parrot_box('pouch_fastener',(sign*.18,-.44,.80),(.037,.013,.044),c['ink'],vest,sign*.22,.004)
        api.tube('vest_shoulder',[(sign*.26,-.22,1.10),(sign*.29,.015,1.20),(sign*.26,.25,1.10)],
            [.032]*3,c['vest'],vest,10)
    api.parrot_box('vest_closure',(0,-.365,.80),(.023,.022,.42),c['ink'],vest,bevel=.005)
    # Keep the legacy optional module ID for loaders, never display eyewear in the roster.
    glasses=api.empty('Gear_Glasses',head)
    for sign in [-1,1]:api.oval('legacy_lens',(sign*.285,-.28,1.585),(.07,.009,.065),c['ink'],glasses,12,7)
    # Recalculate closed surface winding once; exported materials and smooth normals remain.
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
    return root
