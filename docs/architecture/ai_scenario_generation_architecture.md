# AI scenario generation and personalisation architecture

How ProcSense will create fresh, coherent scenarios with real AI, not random recombination of case fragments, and how it will learn from each trainee to shape future practice. Planning only (12 October 2026, second pass); nothing here is built. Stage details: `live_generation_spec.md`. Per-feature plans: `feature_plans/A1.md`, `A2.md`, `A66.md`, `B14.md`, `B18.md`, `D67.md`, `D68.md`. Canonical graph: `procsense_full_pipeline.json`.

## The idea in one paragraph

An offline step turns our cases, the walkthrough, the verified tables and the datasets into a **scenario knowledge base** (relations between conditions, findings, tests, decisions and consequences, each with a source and a confidence) and into **learned statistical models** of how results vary and move together. At session time, the trainee's **learning profile** sets the objectives; an **AI composer** (a language model with retrieval over the knowledge base and example cases, forced to answer in a schema of approved ids) proposes a whole scenario plan with a reason for each choice; a **deterministic solver and validators** complete it, enforce every constraint and reject or repair contradictions; **learned generators and tagged libraries** produce the numbers, waveforms, images and reports; a **novelty check** compares its structure with what the trainee has already seen; and after the session the **profile is updated** from recorded evidence. Every AI part has a measured baseline it must beat and a fallback that keeps the session safe.

## A. Learning from existing evidence (offline)

| Source | What is learned | Method | Limits recorded |
|---|---|---|---|
| Verified tables (bands, critical results, findings, decision rules, what-ifs, NEWS2, sepsis tiers) | Typed relations with SR ids | Deterministic import (A66.c1) | Only what the tables state |
| 53 case files and the walkthrough text | Condition-finding, decision-consequence and interaction relations | LLM extraction offline, every relation backed by a verbatim quote, reviewed before release (A66.c2) | 53 cases are not a population: co-occurrence is not treated as causation; relations carry a type and confidence |
| MIMIC-IV demo labs, MIMIC-IV-ED triage | Per-analyte distributions by flag, linked-analyte correlations, joint triage vitals | Profiles, bootstrap intervals, conditional copulas or Bayesian networks with KB structure (B14.c2, p1-live-scenario-generation-s14) | About 100 patients: n under 50 is marked weak; population percentiles are not per-condition truth |
| VitalDB | Noise, autocorrelation, artefact rates, deterioration slopes | Signal statistics | Intra-operative population |
| PTB-XL | Rhythm and interval distributions, morphology templates | Profiles; parametric ECGSYN variants (B15.c1); learned generator only if needed (B15.c2) | Hyperkalaemia examples sparse (OQ-05) |
| Image datasets and the imaging library | Finding tags, quality grades, gaps | Tagging; validated transforms (B16.c2); offline generative research (E64) | Small library; NIH labels noisy (checked by eye) |

Outliers are never recorded as clinical abnormalities on statistics alone: an abnormality needs a band, a critical limit or a sourced relation.

## B. AI scenario composition (LG-04)

| Choice evaluated | Verdict | Why |
|---|---|---|
| LLM alone writes the whole case | Rejected as the decider | Cannot guarantee constraints, values or licences; hallucination risk |
| Random recombination of blocks | Baseline only | Valid but incoherent or formulaic; no understanding of relations |
| Constraint solver alone | Baseline and fallback | Valid, explainable, but cannot judge teaching value or coherence |
| Learned representation (for example, embedding cases and sampling near them) | Considered; deferred | 53 cases are too few to learn a reliable generative space; revisit with more cases |
| **LLM with retrieval + structured generation + solver** | **Chosen** | Uses the knowledge base and exemplars to propose coherent plans with reasons; the solver and validators enforce correctness |

**Inputs:** Objectives v1 (from the learner profile), difficulty vector, recent fingerprints to avoid, retrieved knowledge-base subgraph, three exemplar cases.
**Output:** ScenarioPlan v1, covering:
- the family;
- the primary and secondary problems, with how they interact;
- comorbidities, medicines and allergies;
- the investigations, with expected findings and band targets;
- decision points, with correct and trap options;
- events, with triggers;
- branches and outcomes;
- teaching points and a rationale for each choice.

Every id is an enum of approved blocks and knowledge-base concepts, and every relation the plan relies on must exist in the knowledge base.

**Loop:** propose → the solver completes and checks the plan → skeleton → generate → validate. Any defect goes back to the composer as an avoid-hint, for at most 3 rounds. After that the case comes from the pool or the library.

**Proof it is AI that helps:** a blind comparison on 40 cases against the solver-only baseline, scored on coherence, objective coverage and variety, at an equal Stage B pass rate (`p1-live-scenario-generation-s13`).

## C. How each output is produced

| Output | Produced by | Kind |
|---|---|---|
| Scenario plan (problems, investigations, decisions, events, branches, outcomes) | LLM composer + solver | **Generated (AI)**, constrained |
| Numerical results, vitals, trends, repeats | Learned conditional models from packs | **Statistically generated** |
| Patient history structure (age, medicines, allergies) | Sampler with compatibility rules | **Statistically generated** |
| Patient story, social context, persona wording | LLM | **Generated (AI)**, grounded |
| ECG and monitor waveforms | Real PTB-XL strips; ECGSYN or template variants; later a learned generator for missing classes | **Retrieved** / **model-generated (parametric)** / **generated (trained model, offline, labelled)** |
| Ultrasound, X-ray, CT | Tag retrieval; bounded transformations that keep the finding; later offline generative synthesis (E64) | **Retrieved** / **validated transformation** / **generated offline, labelled synthetic** |
| Written reports | Approved templates with numbers locked to the generated values; optional LLM paraphrase that must keep every number | **Template + validated paraphrase** |
| Patient and staff dialogue | LLM within released facts and approved intents | **Generated (AI)** live, validated |
| Event sequences and branching outcomes | Composer proposes from approved events and what-ifs; EventDirector and the what-if library execute | **AI-selected, deterministically executed** |

A language model never "draws" an image or a waveform from a description. Images and waveforms come from data, from signal models, or from a dedicated generative model that is trained and evaluated offline.

## D. Consistency and validation

1. **Structured fields:** Stage B (11 checks, 12 contradiction types) and the cross-modal rules VR-CM. Examples:
   - ECG rhythm agrees with the pulse.
   - Potassium agrees with the ECG.
   - Ultrasound agrees with the inflammatory markers.
   - Free air on the film agrees with peritonism.
   - The sepsis tier agrees with vitals and lactate.
   - A haemolysed result has its repeat.
   - The pregnancy test fits sex and age.
   - The timeline order is consistent.
2. **Free text:** the grounding validator checks every narrative, report and dialogue line. An LLM consistency critic then extracts claims from all the text and compares them with the structured case (`p1-live-scenario-generation-s15`).
3. **Behaviour:** a headless correct-path run must reach the declared end state.
4. **Deliberate discrepancies:** a teaching trap or anomaly is a labelled block with a discoverable cue. It is never an accident.
5. **Outcome:** an invalid scenario is repaired at the cheapest level, regenerated, or rejected. Unity only loads packages with a valid hash and status. Nothing contradictory or broken is passed silently.

## E. Meaningful novelty

A fingerprint at three levels:
- **Structural (weight 0.5):** problems, block ids per slot, decision points, events and branches.
- **Clinical (0.35):** abnormal analyte-band pairs, finding tags, rhythm, sepsis tier and traps.
- **Surface (0.15):** persona, narrative template and wording embedding.

The rules:
- A plan must be at distance 0.35 or more from each of the trainee's last 10 sessions.
- Its structural level must differ from the last 3.
- Deliberate practice may repeat the structure only if the clinical level differs.

Names, dialogue or a few changed numbers cannot make a case "novel", because they carry only 15 percent of the weight. Reproducibility comes from:
- versioned inputs (packs, knowledge base, prompts, model digests);
- named seed streams;
- AI output stored inside the package.

## F. Personalisation from recorded performance

| Element | Plan |
|---|---|
| Evidence stored per session (SessionEvidence v1) | Completed and missed steps with lateness, errors and severity, the decision sequence, communication acts (with source and confidence), investigation interpretation actions, responses to events (with latency), hints used and confidence reports. Pseudonymous; no free text from the trainee is kept. |
| Learner model | A Beta-binomial mastery estimate per competency, with evidence weights and a forgetting factor (optionally Bayesian knowledge tracing). AI-sourced evidence is weighted by its confidence and capped. Each estimate carries an interval. |
| Single mistake versus demonstrated weakness | A weakness counts as "demonstrated" only with support from at least 3 sessions, a posterior mean under 0.5 and an interval narrower than 0.3. Until then it is a hypothesis. |
| LLM performance interpreter | Reads a structured summary and proposes hypotheses (linked weaknesses, likely misconceptions) with cited evidence ids. Hypotheses never change mastery directly, and are checked against later evidence. |
| Targeting policy | Rules first (demonstrated weaknesses, with spacing and novelty). A contextual bandit (Thompson sampling over scenario features) replaces them only after off-policy evaluation shows it beats the rules. |
| Effect on content | The objectives drive the composer: which problems, traps, investigations, events and anomalies the next case contains (A2, A7, B20). This changes what is practised, not only the label. |
| Explanation | "Why this case": grounded text listing the profile facts behind the choice. |
| Evaluation | Simulated learners with known mastery, compared against the Elo baseline on next-session prediction and learning gain. Then a pilot comparing personalised generated practice with Elo selection (`p1-personalisation-s05`, `p1-evaluation-s03`). |

## G. Offline preparation versus live execution

| Part | When | What may change | Never |
|---|---|---|---|
| Dataset analysis, knowledge base, learned value models, ECG and image preparation, prompt and schema registry, model benchmark | Offline, reviewed release | Packs, knowledge base, prompts, thresholds, adapters (if E65 adopted) | Inside a session |
| Learner-profile retrieval | Session start (LP-01) | Nothing (read only) | — |
| Targeting and AI composition | Session start, or batch for the pool (LP-03, LG-02 to LG-06) | Only the new package | Model weights |
| Numerical and multimodal generation | Live from packs, libraries and parametric models; generative models only offline | Only the new package | Training |
| Validation, repair, fallback | Live, bounded (3 rounds, time budget) | Package or fallback choice | Silent pass of a defect |
| Runtime interaction and state | Live in the Core | State by rules only | AI setting state |
| Post-session analysis | After the session (LP-02, cheap posterior update); interpreter and bandit refit offline | Learner profile | Model retraining from scratch per session |

## Hardware

Both GPUs are part of the plan. The desktop GTX 1660 SUPER has 6 GB, no tensor cores, 16 GB RAM and a Ryzen 5 2600 (all measured). The laptop RTX 4050 has 6 GB, and its RAM and CPU are still to be recorded. The benchmark (`p1-ai-infrastructure-s02`) measures:
- every model's memory use, speed and output validity on each GPU;
- Unity's frame time while the model runs.

The expected topology is inference on the desktop and Unity on the laptop. A single-machine mode with small models and the pre-generated pool is the fallback. Profiles and alternatives are in `model_technology_register.md`.
