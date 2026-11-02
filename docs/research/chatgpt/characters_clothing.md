# ChatGPT research: characters_clothing

Question: List the best FREE, redistributable (CC0 or CC-BY) sources for realistic rigged human characters (male/female doctors, nurses, patients, receptionist) and medical clothing (lab coats, scrubs, hospital gown, surgical gown, cap, mask, gloves, shoes, badge) usable in Unity URP. Consider MakeHuman/MPFB2, Sketchfab CC0, Quaternius, Poly Haven, Mixamo, KayKit, Blender Studio, OpenGameArt, CGTrader free. For each: realism, rig, licence, repo-redistribution OK?, and what you would pick. Also give your recommended approach for a hospital gown that drapes naturally on a lying patient (baked pose-specific mesh vs cloth sim) and best realistic skin/hair shading in URP.

## Bottom line

For **realistic hospital staff and patients in a public Unity repository**, I would use:

1. **MakeHuman or MPFB2 for the human bodies and starting rigs.**
2. **A small, consistent wardrobe made in Blender**, using only individually verified CC0/CC-BY clothing as starting material.
3. **Unity URP-compatible materials rebuilt in Unity**, rather than expecting downloaded Blender materials to transfer correctly.
4. **Pose-specific, baked hospital gowns for lying patients**, with optional small secondary-motion effects—not full garment simulation on Quest 2.

**I do not know a complete, verified CC0/CC-BY collection that provides realistic, rigged male/female doctors, nurses, patients and receptionists plus every garment you list.** The reliable solution is a character pipeline, not a single download.

Here, “repository redistribution” means committing the **actual models, textures and animations**, not merely distributing a compiled application.

---

## Source comparison

The named assets below are concrete starting points. Where I list a marketplace or library instead, that is **a discovery source, not an endorsement of every asset hosted there**.

| Source / exact starting point | Realism and rig | Licence and public-repository redistribution | My recommendation |
|---|---|---|---|
| **MakeHuman**, by the **MakeHuman Community** — [makehumancommunity.org](https://www.makehumancommunity.org/) | Realistic human proportions; configurable age, body shape and sex characteristics. Exportable skeletons. Appearance still needs good textures, hair and clothing. | Software is **AGPL**; ordinary exported characters made from the core assets are covered by MakeHuman’s **CC0 export exception**. **Yes for qualifying character exports**, but verify separately downloaded community assets. | **Best starting point for this project.** Preserve the export/licence documentation alongside your exported assets. |
| **MPFB / MPFB2**, by the **MakeHuman Community** — [GitHub: makehumancommunity/mpfb2](https://github.com/makehumancommunity/mpfb2), [documentation](https://static.makehumancommunity.org/mpfb.html) | Blender-native MakeHuman workflow, suitable for generating and editing realistic bodies and rigs. | Add-on code is **GPL-licensed**; that does not automatically make generated meshes GPL. MakeHuman core asset/output permissions and each installed asset pack’s licence must be considered separately. **Yes for verified CC0 output/assets.** | **My preferred Blender-centred workflow.** Do not assume every optional clothing or hair download is CC0. |
| **Sketchfab downloadable CC0 / CC-BY models**, by individual uploaders — [Sketchfab](https://sketchfab.com/) | Potentially realistic; many scans are static, in awkward topology, or unsuitable for animation. Rig quality varies enormously. | **Yes only when the particular model explicitly has CC0 or CC-BY.** CC-BY requires attribution and identification of modifications. Other Sketchfab licences are not equivalent. | Useful for finding individual garments or reference meshes. **I am not naming a specific medical character because I cannot confidently verify one’s current download and licence.** |
| **Universal Base Characters**, by **Quaternius** — [asset page](https://quaternius.com/packs/universalbasecharacters.html) | Rigged base characters; deliberately simplified/stylized rather than realistic medical-training humans. | Advertised as **CC0**; **yes**, including source assets. Check the licence included with your downloaded version. | Good for prototypes and mobile performance tests. Not my final-art choice for a realistic hospital. |
| **Poly Haven asset library**, by **Poly Haven and credited contributors** — [polyhaven.com](https://polyhaven.com/), [licence](https://polyhaven.com/license) | Excellent realistic environment textures and lighting references. **Not a dependable source of rigged humans or a medical wardrobe.** | Library assets are **CC0**; **yes**. | Use for supporting materials and environment lighting—not as your character solution. |
| **Mixamo characters and animations**, by **Adobe** — [mixamo.com](https://www.mixamo.com/) | Useful auto-rigging and humanoid animation; character realism varies. | **Not CC0 or CC-BY.** Adobe’s normal permissions allow incorporation into projects, but not unrestricted distribution of the raw character/animation files as an asset library. **Do not commit downloaded Mixamo assets to a public asset repository.** | Exclude from the committed asset pipeline under your requirement. A private prototype is a different use case. |
| **KayKit: Adventurers Character Pack**, by **Kay Lousberg** — [asset page](https://kaylousberg.itch.io/kaykit-adventurers) | Rigged, stylized low-poly fantasy characters—not realistic hospital staff. | **CC0** for the pack; **yes**. Check that you are downloading this pack, not assuming identical terms across every product. | Excellent permissive stylized assets, but **wrong subject and realism level** here. |
| **Rain character**, by **Blender Studio** — [Rain v2](https://studio.blender.org/characters/rain/v2/) | High-quality **stylized**, rigged Blender character. The Blender control rig is not a drop-in Unity rig. | **CC-BY 4.0**; **yes**, with attribution and modification notices. Preserve the supplied credits. | Useful rig/art reference, not my hospital production base. Requires game-oriented export, material conversion and optimization. |
| **OpenGameArt**, by individual contributors — [opengameart.org](https://opengameart.org/) | Mixed quality and style; realistic rigged humans and medical clothing are not its main strength. | Asset-specific. **CC0 and CC-BY entries can qualify**; GPL, CC-BY-SA and other entries do not meet your stated CC0/CC-BY filter. | Secondary discovery source only. **No specific medical set recommended without inspecting its files and licence.** |
| **CGTrader Free**, by individual sellers — [free models](https://www.cgtrader.com/free-3d-models) | Can contain realistic humans and medical garments, but topology, rig and provenance vary. | “Free” usually describes price, **not open redistribution**. Standard royalty-free terms generally do not permit sharing raw source assets publicly. | **Exclude unless the particular asset is explicitly CC0/CC-BY and that permission is clear.** I cannot recommend a verified medical set here. |

**Important:** CC-BY does not mean “credit the website.” Credit the actual creator and asset, link the licence, and state changes. Also inspect bundled textures: a permissively licensed mesh does not automatically clear third-party textures, logos or reference imagery.

---

## What I would actually use for your hospital

### Characters

Build a small family of MakeHuman/MPFB-derived characters sharing:

- A consistent export skeleton and scale.
- A limited number of body shapes.
- Shared skin and clothing material conventions.
- A reusable set of standing, seated and lying poses.

Use **Unity Humanoid** for standard staff locomotion where the exported rig maps cleanly. For a patient with a specialized bed pose, a **Generic rig** can be simpler if humanoid retargeting provides no benefit.

Do not create roles solely through different bodies: doctors, nurses and receptionists can share base characters and differ through clothing, identification and animation.

### Wardrobe

I would make or commission the following small set in Blender and release your own work under CC0 or CC-BY:

| Item | Practical construction |
|---|---|
| **Scrub top and trousers** | Fitted, skinned garment meshes with baked folds. |
| **Lab coat** | Skinned coat; add helper bones for the lower panels if movement needs them. |
| **Hospital gown** | Separate standing/seated and lying variants where necessary. |
| **Surgical gown** | Skinned garment with modeled overlap, cuffs and restrained baked folds. |
| **Cap** | Close-fitting mesh; hide underlying hair where appropriate. |
| **Mask** | Separate fitted mesh, with fit variants for different faces. |
| **Gloves** | Hand-derived meshes or a suitable hand material replacement; avoid unnecessary overlapping hand geometry. |
| **Shoes** | Separate skinned meshes, shared across compatible characters. |
| **Badge** | Simple mesh and original/generated identity graphics; avoid third-party logos and real patient information. |

**I cannot confidently name a verified, openly redistributable free asset for every item in that list.** It is better to make a coherent small wardrobe than assemble an apparently free kit with uncertain licences.

Remove or hide body surfaces under opaque clothing where practical. This reduces clipping and wasted rendering, especially on Quest.

---

## Hospital gown on a lying patient: bake the drape

**My recommendation is offline cloth simulation in Blender, followed by a baked, pose-specific garment mesh.**

### Recommended workflow

1. Pose the patient on the actual mattress and pillow geometry.
2. Fit a gown with believable ease and sufficient fabric around the hips and knees.
3. Simulate its settling in Blender, using body and bed collision.
4. Apply the settled result.
5. Clean up penetrations and simplify/retopologize where needed.
6. Bake fine folds into normal maps.
7. Skin the resulting gown for small movements, or create a few corrective shape keys.

A supine gown should respond to **the mattress as well as the body**. Merely skinning a standing gown to a lying skeleton often produces stretched fabric rather than convincing drape.

### Choose the technique by interaction

| Situation | Preferred solution |
|---|---|
| Patient mostly still, breathing | Baked drape plus subtle skeletal or shape-key breathing. |
| A few predefined poses | Separate pose-specific gowns, or carefully authored corrective shapes. |
| Scripted repositioning | Authored deformation, or a validated baked animation/cache where its runtime cost is acceptable. |
| Trainee actively pulls fabric aside | Dedicated interaction design; small controlled garment sections or constrained simulation. |
| Unrestricted fabric manipulation | A much larger simulation task—not a free consequence of adding Unity Cloth. |

Unity Cloth can help with selected secondary motion, but **it is not a robust, general-purpose solution for a gown trapped between a moving patient and a mattress**. Collision setup, self-collision, stability and Quest CPU cost all become concerns.

---

## Realistic skin and hair in Unity 6 URP

### Skin

URP Lit is a useful baseline, but **it does not provide HDRP’s built-in skin subsurface-scattering workflow**.

**Desktop:**

- Use well-authored skin albedo, normal and smoothness maps.
- Add restrained skin-specific lighting through a custom Shader Graph/HLSL shader if needed: approximate subsurface response, thickness-dependent transmission and subtle detail normals.
- Treat screen-space SSS as an optional desktop feature with explicit XR testing—not something guaranteed by choosing URP.
- Keep colour variation subtle and ensure the albedo is not carrying baked directional lighting.

**Quest 2:**

- Prefer a simple forward-rendered shader: URP Lit or a lean custom skin shader.
- Use baked texture detail and a modest subsurface approximation rather than full-screen scattering.
- Spend the budget on face topology, good normals, texture quality at the actual viewing distance, and stable lighting.

**Lighting quality usually improves perceived skin more than adding a complicated scattering effect to poor textures.**

### Hair

For this project, use **hair cards or carefully modeled hair masses**, not cinematic strand rendering.

- Desktop: a tested custom anisotropic hair shader can improve highlights.
- Quest: prioritize a tight silhouette, low card overlap and minimal overdraw.
- Prefer opaque hair masses and/or alpha-clipped cards over many transparent layers.
- Alpha-to-coverage can improve clipped edges with MSAA, **if your shader and rendering configuration support it**.
- Test both eyes on-device: sorting artifacts, shimmering and temporal effects can be much more objectionable in VR.

For staff wearing caps, make a cap-specific hair variant instead of rendering a full hairstyle underneath.

### Final pick

**MPFB/MakeHuman + your own small CC0 wardrobe + baked lying-gown variants + restrained URP skin/hair shaders** is the strongest combination of realism, repository safety and Quest feasibility.

Keep an asset manifest containing **asset name, creator, original URL, downloaded version/date, licence file, attribution and modifications**. That is what makes the public repository auditable—not just a folder named `FreeAssets`.
