"""Builds the civilian waiting-room and corridor people with MPFB exactly like the staff (GameEngine rig, CC0 system assets), saves build/mpfb/output/staff/<key>.blend ready for staff_pose.py --bake.

  BLENDER_USER_RESOURCES=<abs>/build/mpfb/blender-user blender -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/build_civilians.py -- [key ...]
All people are fictional generic MPFB presets, nobody real is depicted. Clothes are the MakeHuman casual suits (CC0), skins from the MakeHuman skin library (CC0).
"""
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_staff as bs  # noqa: E402
from staff_outfit import clear_of, outfit, tint_texture  # noqa: E402
SUIT_TINT = {"patient_f_old": (.70, .85, 1.0), "parent_f": (1.0, .86, .62), "visitor_f_talk": (.78, .78, .80)}   # the one striped blouse and skirt, recoloured so the three women do not match


def person(name, gender, age, height, weight, muscle, race, skin, brows, lashes, hair, hair_rgb, clothes):
    return dict(name=name, gender=gender, age=age, height=height, weight=weight, muscle=muscle, cupsize=.45, race=race, skin=skin, eyebrows=brows, eyelashes=lashes, hair=hair, hair_rgb=hair_rgb, clothes=clothes)


CAUC, AFR, ASI = {"asian": .1, "caucasian": .8, "african": .1}, {"asian": .1, "caucasian": .1, "african": .8}, {"asian": .8, "caucasian": .1, "african": .1}
CIV = {
    "patient_m_old": person("PatientMaleOld", 1.0, .92, .5, .55, .3, CAUC, "old_caucasian_male/old_caucasian_male.mhmat", "eyebrow001/eyebrow001.mhclo", "eyelashes01/eyelashes01.mhclo", "short01/short01.mhclo", (.70, .70, .70), ["male_casualsuit01/male_casualsuit01.mhclo", "shoes01/shoes01.mhclo"]),
    "patient_f_old": person("PatientFemaleOld", 0.0, .94, .45, .5, .25, CAUC, "old_caucasian_female/old_caucasian_female.mhmat", "eyebrow004/eyebrow004.mhclo", "eyelashes02/eyelashes02.mhclo", "bob01/bob01.mhclo", (.78, .78, .78), ["male_casualsuit04/male_casualsuit04.mhclo", "shoes04/shoes04.mhclo"]),
    "adult_f_phone": person("AdultFemalePhone", 0.0, .36, .5, .45, .35, AFR, "young_african_female/young_african_female.mhmat", "eyebrow001/eyebrow001.mhclo", "eyelashes01/eyelashes01.mhclo", "long01/long01.mhclo", (.05, .035, .03), ["male_casualsuit02/male_casualsuit02.mhclo", "shoes05/shoes05.mhclo"]),
    "child": person("ChildBoy", 1.0, .16, .5, .45, .3, CAUC, "young_caucasian_male2/young_caucasian_male2.mhmat", "eyebrow004/eyebrow004.mhclo", "eyelashes01/eyelashes01.mhclo", "short02/short02.mhclo", (.22, .12, .06), ["male_casualsuit05/male_casualsuit05.mhclo", "shoes04/shoes04.mhclo"]),
    "parent_f": person("ParentFemale", 0.0, .45, .5, .5, .35, CAUC, "young_caucasian_female2/young_caucasian_female2.mhmat", "eyebrow005/eyebrow005.mhclo", "eyelashes02/eyelashes02.mhclo", "ponytail01/ponytail01.mhclo", (.20, .10, .05), ["female_casualsuit01/female_casualsuit01.mhclo", "shoes01/shoes01.mhclo"]),
    "visitor_m_paper": person("VisitorMalePaper", 1.0, .58, .52, .5, .4, ASI, "middleage_asian_male/middleage_asian_male.mhmat", "eyebrow004/eyebrow004.mhclo", "eyelashes01/eyelashes01.mhclo", "short03/short03.mhclo", (.03, .025, .02), ["male_casualsuit03/male_casualsuit03.mhclo", "shoes05/shoes05.mhclo"]),
    "visitor_f_talk": person("VisitorFemaleTalk", 0.0, .6, .5, .5, .35, AFR, "middleage_african_female/middleage_african_female.mhmat", "eyebrow001/eyebrow001.mhclo", "eyelashes01/eyelashes01.mhclo", "bob02/bob02.mhclo", (.04, .03, .025), ["female_elegantsuit01/female_elegantsuit01.mhclo", "shoes01/shoes01.mhclo"]),
    "visitor_m_cough": person("VisitorMaleCough", 1.0, .40, .55, .5, .45, AFR, "young_african_male/young_african_male.mhmat", "eyebrow004/eyebrow004.mhclo", "eyelashes01/eyelashes01.mhclo", "short04/short04.mhclo", (.025, .02, .018), ["male_casualsuit06/male_casualsuit06.mhclo", "shoes05/shoes05.mhclo"]),
}
bs.PEOPLE.update(CIV)


def main():
    keys = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(CIV)
    for key in keys:
        rig, human = bs.build(key)
        outfit(bs.PEOPLE[key], rig, human)                                       # hair and brow colour (no scrubs/coat: the else branch only clears any elegant suit)
        for o in rig.children:                                                  # casual suit: keep it clear of the skin, drop the (inactive) shape keys first like the staff garments
            if o.type == "MESH" and ("casualsuit" in o.name or "sportsuit" in o.name or "elegantsuit" in o.name):
                if o.data.shape_keys:
                    o.shape_key_clear()
                clear_of(o, [human], .009)
                if "elegantsuit" in o.name and key in SUIT_TINT:
                    tint_texture(o, SUIT_TINT[key])
        bpy.ops.wm.save_as_mainfile(filepath=str(bs.OUT / f"{key}.blend"))
        print("CIVILIAN_BUILT", key, len(rig.children))


main()
