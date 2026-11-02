# ChatGPT research: benches_people

Question: Recommend exact free CC0/CC-BY 3D models (with URLs and licences, say plainly if unsure) usable in a public repo for: (1) a perforated stainless-steel connected 3-seat waiting bench like in airports and hospitals, (2) realistic rigged seated/standing civilian characters including an elderly man, a woman, a child, a parent, in casual clothes, (3) a patient stretcher/gurney, (4) hospital medical transport trolley. Also: best practice for sitting poses and idle animation loops in Unity URP for a Quest 2 crowd (Animator vs baked poses vs GPU instancing), and for baked directional sunlight through large windows in URP including window-frame shadows.

**I can recommend a reliable CC0 character-generation route, but I cannot confidently name exact CC0/CC-BY downloads for the three hospital-furniture items without checking their individual asset pages.** I don’t have live web verification here, so I won’t invent model names, authors, or Sketchfab links.

That distinction matters for a public repository: **“free download” does not necessarily permit redistribution of the model’s source files.** Unity Asset Store assets, for example, generally are not suitable for committing as openly downloadable source assets.

## 1. Asset recommendations and verification status

| Requirement | Exact asset/source, author and URL | Licence and suitability |
|---|---|---|
| **Perforated stainless-steel connected three-seat waiting bench** | **No exact model I can confidently identify and license-check.** | I cannot responsibly recommend a particular download as CC0/CC-BY. For this fairly specific object, modelling an original version is the most dependable public-repository option. |
| **Realistic civilian characters: elderly man, woman, child, parent** | **MakeHuman**, by the **MakeHuman Community** — https://www.makehumancommunity.org/ | MakeHuman’s standard exported character assets are provided under **CC0**; the application itself uses a different software licence. This is a **character-generation workflow, not a downloadable pack containing those four finished people**. Check the licences of any separately downloaded clothes, hair or other community additions. |
| **Patient stretcher/gurney** | **No exact model I can confidently identify and license-check.** | Do not assume that a medical model described as “free” allows source redistribution. |
| **Hospital medical transport trolley** | **No exact model I can confidently identify and license-check.** | Also clarify the type: an instrument/dressing trolley, enclosed supply trolley and patient-transfer trolley are substantially different assets. |

### Characters: the practical CC0 route

With **MakeHuman**, generate your own named project assets—for example, `ElderlyMan`, `AdultWoman`, `Child` and `Parent`—rather than searching for four unrelated models with mismatched proportions and rigs.

It provides age and body-shape controls and rigged export options. However:

- **Seated poses and animation clips are additional work**, not something I am claiming comes with those four characters.
- “Parent” is a role: use an adult character with a compatible child, appropriate placement and interaction.
- Start with the **bundled standard assets**. Third-party MakeHuman community clothing can have different licences; verify each item.
- Test the chosen skeleton with Unity’s Humanoid importer. A rigged export does not automatically guarantee a clean Humanoid mapping.
- Treat MakeHuman as a starting point for realism. Hair, clothing, skin materials and expression work strongly affect the final result.

Official CC0 deed: https://creativecommons.org/publicdomain/zero/1.0/

**Limit of my confidence:** I am confident in the MakeHuman website and its standard-export CC0 policy, but I cannot inspect the current download or individual clothing pages here. Retain the licence information distributed with the version and assets you actually use.

### Furniture: what I would do for this project

For the waiting bench, an original mesh is likely less work than finding a legally reusable exact match:

- Build three repeated seat/back shells on one beam with shared legs.
- Use a normal/roughness treatment for the perforated appearance at crowd distances.
- Use actual holes only where silhouette or close inspection requires them.
- Avoid modelling every tiny hole across the entire bench; alpha clipping is not automatically cheaper either, especially on Quest.
- Use one shared material and atlas where practical.

For a gurney or trolley used in hands-on training, validate the geometry against your actual equipment. Brake controls, rails, mattress height and handle placement can matter more than visual detail.

If the project owns the resulting original models and all their inputs, it can publish them under CC0.

## 2. Public-repository licence checklist

For each imported model, commit:

```text
Assets/ThirdParty/AssetName/
    model...
    textures...
    LICENSE.txt
    SOURCE.md
```

Record the **exact title, author, source URL, licence/version and modifications**. Check textures separately: a permissive mesh licence does not necessarily cover every included texture.

For **CC BY 4.0**, preserve attribution, link the licence and indicate changes:

https://creativecommons.org/licenses/by/4.0/

Avoid **NC**, **ND**, “editorial use only,” and custom licences that prohibit redistribution. Also avoid assuming that an author’s other CC0 assets establish the licence of a particular download.

## 3. Quest 2 crowds: Animator vs baked poses vs GPU instancing

**Use a hybrid system.** Do not give every background person a full Animator just because the source character has a rig.

| Character use | Recommended approach |
|---|---|
| Close, interactive patient/NPC | Skinned mesh with Animator; limited animation layers and bones |
| Nearby waiting-room occupant | Simple seated/standing Animator with a small idle set |
| Mid-distance background occupant | Static baked pose, or a very lightweight animated version where movement matters |
| Distant occupant | Static posed mesh; simplified geometry/materials |
| Large, visibly animated crowd | Consider GPU animation only after profiling proves ordinary skinning/animation is the bottleneck |

### Sitting poses

Author the pose against a **known seat height and depth**:

1. Place a seat anchor on the bench.
2. Align the character’s hips/root to that anchor.
3. Check pelvis contact, feet on the floor and hands against thighs or armrests.
4. Create several pose variants to avoid identical silhouettes.
5. Keep background seated idles **in place**, normally without root motion.

Use separate variants for adults and children. A child’s feet may hang above the floor; forcing adult foot placement can produce implausible poses.

For a close NPC, IK can resolve small differences in chair or floor position. For background crowds, **author and bake that correction rather than running IK continuously**.

### Idle loops

Use subtle breathing, small head turns and occasional posture adjustments. Offset playback phases so the room does not move in synchrony.

For static background figures:

- Pose the character in your DCC application or Unity.
- Bake the skinned mesh once.
- Disable/remove the Animator and SkinnedMeshRenderer.
- Render the result as a normal mesh.

**Do not call `BakeMesh` every frame** as a supposed crowd optimisation.

### Important instancing distinction

**Conventional mesh GPU instancing does not automatically batch independently animated SkinnedMeshRenderers.**

Static posed characters can benefit from instancing when they share the same mesh and compatible material state. A crowd using a different baked mesh for every individual loses much of that benefit. Reuse a limited library of pose meshes.

Animated GPU crowds require a separate solution—such as bone-texture animation or vertex animation—with appropriate URP shaders, bounds handling and shadow support. That adds engineering and memory costs; it is not a free checkbox.

For Quest 2, first reduce:

- Animator count and complexity.
- Bone counts and skin influences.
- Material slots and transparent hair.
- Real-time shadow casters.
- Overdraw and visible geometry.

Profile on the headset at the intended refresh rate. Keep desktop and Quest crowd-quality settings separate.

## 4. Baked sunlight through large windows in Unity 6 URP

**For fixed-time hospital interiors, baked sunlight is a strong baseline.** Window-frame shadows should come from actual frame geometry, not from the glass material.

### Reliable setup

1. Use a **Directional Light** for the sun.
2. Mark walls, floors, ceilings and window frames to **Contribute GI**.
3. Give receiving meshes suitable, non-overlapping lightmap UVs.
4. Ensure window-frame geometry casts shadows.
5. Start with a **Baked** sun for a fully static lighting solution.
6. Bake with the Progressive Lightmapper.
7. Add light probes for moving characters, plus reflection probes where useful.

### Glass: the common failure point

**Transparent-looking glass is not automatically transparent to the lightmapper.**

For ordinary clear windows, the robust starting point is:

- Disable shadow casting on the glass.
- Exclude the glass from contributing GI if it otherwise blocks the bake.
- Keep the window frame and mullions contributing GI and casting shadows.
- Re-bake and inspect the result.

Use a temporary bake with the glass disabled to diagnose blocked sunlight. If you need tinted transmission, validate a deliberate lightmapping-material setup; do not assume the visual URP glass shader provides physically correct transmission during baking.

### Keeping the frame shadows readable

Large window shadows need sufficient texel density on the **receiving floor and walls**, not just on the frame.

- Inspect lightmap texel density around the sunlight patch.
- Increase receiver resolution selectively.
- Check UV padding and avoid tiny charts.
- Adjust bake filtering/denoising if it erases thin mullion shadows.
- Keep the sun’s baked angular size reasonably small if you need crisp shadows.

Do not solve one blurry window shadow by raising the entire hospital’s lightmap resolution.

### Moving characters and baked sunlight

A baked sun will not provide moving characters with correct real-time cast shadows. Probes can approximate their lighting, but **ordinary light probes do not reproduce sharp window-frame shadow bands moving across a character**.

Choose consciously:

- **Quest baseline:** baked environment, probe-lit characters, and inexpensive contact/blob shadows if needed.
- **Higher quality:** a Mixed sun with supported shadowmask settings and tightly budgeted real-time shadows. Validate the exact Unity 6 URP renderer, shader and target-device configuration.
- **Desktop:** longer shadow distance and more dynamic shadow casters, if profiling permits.

Directional lightmaps improve directional lighting information on static surfaces, especially with normal maps, but cost additional memory and sampling. Compare them with non-directional lightmaps on Quest rather than enabling them by default.

**My recommended baseline:** original CC0 project furniture, MakeHuman-derived civilians with carefully tracked clothing licences, mostly static posed background occupants, a few animated foreground NPCs, and fully baked window sunlight on Quest. Add dynamic crowd and shadow features only where they improve the training task.
