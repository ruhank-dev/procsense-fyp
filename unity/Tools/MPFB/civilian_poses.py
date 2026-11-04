"""Poses for the civilian waiting-room and corridor people (patients, visitors, a parent and child). Registered into staff_pose.POSES; authored with the same world-space joint helpers
(no mocap, no real person). Blender world: the character faces -Y, its right side is -X, its left +X. Sitting targets a bench seat 0.465 m high with the back of the seat at y = +0.235 (unity/Tools/Environment/build_waiting_furniture.py).
"""
from mathutils import Matrix, Vector


def register(sp, POSES):
    rot, two_bone, bone, upd, X, Y, Z, grip = sp.rot, sp.two_bone, sp.bone, sp.upd, sp.X, sp.Y, sp.Z, sp.grip

    def sit(rig, seat=.465, back=.05, child=False):
        """Seated against the backrest: pelvis on the seat, thighs forward and level, shins vertical, feet flat, torso upright with a slight recline."""
        pel = bone(rig, "pelvis"); hip = .12 * (pel.head.z / .95)
        pel.matrix = Matrix.Translation((0, back, -(pel.head.z - (seat + hip)))) @ pel.matrix; upd()
        for s in ("l", "r"):
            rot(rig, f"thigh_{s}", X, 84 if child else 88); rot(rig, f"calf_{s}", X, -62 if child else -92); rot(rig, f"foot_{s}", X, 24 if child else 4)
        rot(rig, "spine_01", X, -4); rot(rig, "spine_03", X, 2); rot(rig, "neck_01", X, 3)                       # sitting tall against the back, head level

    def hands_on_thighs(rig, up=.075, inset=.035):
        """Palms resting on the thighs about halfway to the knees, elbows slightly out and back."""
        for s, sx in (("r", -1), ("l", 1)):
            hip = bone(rig, f"thigh_{s}").head; knee = bone(rig, f"calf_{s}").head
            mid = (hip + knee) * .5 + Vector((-sx * inset, 0, up))
            two_bone(rig, f"upperarm_{s}", f"lowerarm_{s}", mid, Vector((sx * 1.0, .5, -.3)))

    def pose_patient_m_old(rig):
        """Elderly man waiting: sitting upright against the backrest, hands resting on his thighs, looking ahead."""
        sit(rig); rot(rig, "spine_02", X, 2); rot(rig, "head", X, 3); hands_on_thighs(rig); return {}

    def pose_patient_f_old(rig):
        """Elderly woman waiting, sitting upright, hands loosely clasped in her lap."""
        sit(rig); rot(rig, "head", X, 4)
        lap = (bone(rig, "thigh_l").head + bone(rig, "thigh_r").head) / 2 + Vector((0, -.16, .08))
        two_bone(rig, "upperarm_r", "lowerarm_r", lap + Vector((-.035, 0, 0)), Vector((-1, .4, -.3)))
        two_bone(rig, "upperarm_l", "lowerarm_l", lap + Vector((.035, 0, 0)), Vector((1, .4, -.3))); return {}

    def pose_adult_f_phone(rig):
        """Young woman checking her phone with both hands: sitting, elbows in near the body, forearms forward, phone at chest height tilted to her eyes, head lowered."""
        sit(rig); z = bone(rig, "spine_03").head.z
        rot(rig, "spine_03", X, 5); rot(rig, "neck_01", X, 12); rot(rig, "head", X, 12)
        ph = Vector((0, -.255, z - .07))
        two_bone(rig, "upperarm_r", "lowerarm_r", ph + Vector((-.045, .02, -.05)), Vector((-.5, .6, -1)))
        two_bone(rig, "upperarm_l", "lowerarm_l", ph + Vector((.045, .02, -.05)), Vector((.5, .6, -1)))
        grip(rig, "r", 30, 16); grip(rig, "l", 30, 16)
        return {"phone_pos": ph, "phone_tilt": 38, "phone_hand": "r"}

    def pose_child(rig):
        """Child sitting on the bench, legs not reaching the floor, hands resting on the thighs, looking toward the parent (to the child's left, +X)."""
        sit(rig, child=True, back=.03); hands_on_thighs(rig, up=.05, inset=.03)
        rot(rig, "neck_01", Z, 12); rot(rig, "head", Z, 18); return {}

    def pose_parent_f(rig):
        """Parent talking to the child beside her (to her right, -X): sitting upright, shoulders and head turned toward the child, right hand lifted in a small explaining gesture, left hand resting on her thigh."""
        sit(rig); rot(rig, "spine_02", Z, -6); rot(rig, "neck_01", Z, -14); rot(rig, "head", Z, -16)
        hip = bone(rig, "thigh_l").head; knee = bone(rig, "calf_l").head
        two_bone(rig, "upperarm_l", "lowerarm_l", (hip + knee) * .5 + Vector((-.035, 0, .075)), Vector((1, .5, -.3)))
        knee_r = bone(rig, "calf_r").head
        two_bone(rig, "upperarm_r", "lowerarm_r", knee_r + Vector((.03, -.07, .22)), Vector((-1, .3, -.3))); return {}

    def pose_visitor_m_paper(rig):
        """Man standing, reading a sheet of paperwork held in both hands at the lower side edges, elbows bent about 90 degrees, sheet tilted to his eyes, head lowered."""
        z = bone(rig, "spine_03").head.z; c = Vector((0, -.31, z - .06))
        rot(rig, "thigh_r", X, -3); rot(rig, "calf_r", X, 5); rot(rig, "spine_03", X, 4); rot(rig, "neck_01", X, 8); rot(rig, "head", X, 12)
        two_bone(rig, "upperarm_r", "lowerarm_r", c + Vector((-.095, .01, -.115)), Vector((-.5, .6, -1)))
        two_bone(rig, "upperarm_l", "lowerarm_l", c + Vector((.095, .01, -.115)), Vector((.5, .6, -1)))
        grip(rig, "r", 34, 20); grip(rig, "l", 34, 20)
        return {"sheet_pos": c, "sheet_tilt": 20, "sheet_hand": "r"}

    def pose_visitor_f_talk(rig):
        """Woman standing in conversation: weight on one leg, right hand gesturing at waist height, left hand relaxed, head turned toward her companion (to her right, -X)."""
        z = bone(rig, "spine_03").head.z; pz = bone(rig, "pelvis").head.z
        rot(rig, "pelvis", Y, 3.5); rot(rig, "spine_02", Y, -2); rot(rig, "thigh_l", X, -4); rot(rig, "calf_l", X, 7)
        rot(rig, "neck_01", Z, -10); rot(rig, "head", Z, -14); rot(rig, "head", Y, 3)
        two_bone(rig, "upperarm_r", "lowerarm_r", Vector((-.25, -.27, z - .13)), Vector((-1, .6, -.3)))
        two_bone(rig, "upperarm_l", "lowerarm_l", Vector((.22, -.06, pz - .05)), Vector((.8, .4, -.5))); return {}

    def pose_visitor_m_cough(rig):
        """Man standing, coughing into the crook of his raised right arm in front of his mouth, head turned slightly away, left arm relaxed."""
        hz = bone(rig, "head").head.z; pz = bone(rig, "pelvis").head.z
        rot(rig, "spine_02", X, 4); rot(rig, "spine_03", X, 4); rot(rig, "neck_01", Z, 10); rot(rig, "head", Z, 14); rot(rig, "head", X, 6)
        two_bone(rig, "upperarm_r", "lowerarm_r", Vector((-.03, -.12, hz + .01)), Vector((-.3, -.7, -.8)))
        two_bone(rig, "upperarm_l", "lowerarm_l", Vector((.21, -.05, pz - .06)), Vector((.9, .3, -.4))); return {}

    POSES.update({"patient_m_old": pose_patient_m_old, "patient_f_old": pose_patient_f_old, "adult_f_phone": pose_adult_f_phone, "child": pose_child,
                  "parent_f": pose_parent_f, "visitor_m_paper": pose_visitor_m_paper, "visitor_f_talk": pose_visitor_f_talk, "visitor_m_cough": pose_visitor_m_cough})
