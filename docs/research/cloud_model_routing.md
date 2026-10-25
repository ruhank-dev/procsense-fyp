# Cloud model routing benchmark (25 October 2026, ADR-0026)

Every figure is from `reports/ai/cloud-routing-benchmark.json` (built by `tools/ai/cloud_routing_report.py` from the runs of `tools/ai/eval_*.py`). Models run through a signed-in local Ollama on the Free plan. **Data: synthetic only** (authored cases; AI-written test sets that are not human-labelled). The checks for leaks and wording are lexical; the replies are written out for a person to read (DT-190, DT-203, DT-250). No real trainee data was sent.

## What the account can use (Free plan)
Reachable: `gemma4:31b`, `gpt-oss:20b`, `gpt-oss:120b`, `nemotron-3-nano:30b`. HTTP 402 (paid plan): `deepseek-v4.1-flash:1017`, `deepseek-v4-pro:0813`, `mistral-large-3:675b`. Not found for the account: `deepseek-v4.1-flash`, `glm-5.2`, `glm-5.3`, `glm-5.3-flash`, `kimi-k2.6`, `kimi-k2.7-code`, `kimi-k3`, `minimax-m2.7`, `minimax-m3`, `minimax-m3.1-flash`, `mistral-large-4`, `nemotron-3-super`, `nemotron-3-ultra`, `muse-spark-open`, `beam`. The pricing page: Free has 1 concurrent request and a starter amount of usage credits that reset monthly (the amount is not stated).

## Results (local = `qwen3.5:4b-q4_K_M`)
| Task | Measure | Local | gemma4:31b | gpt-oss:120b | Others |
|---|---|---|---|---|---|
| Patient dialogue (82 + 82 questions) | fact used when earned | 74/82 | **82/82** | 82/82 | nemotron-nano 81/82, gpt-oss:20b 81/82 |
| | "cannot recall" when withheld | 76/82 | **81/82** | 80/82 | nemotron-nano 34/82, gpt-oss:20b 72/82 |
| | lexical leaks | 1 | **0** | 0 | nemotron-nano 7 |
| | median / p95 | 1,882 / 2,289 ms | **684 / 1,716** | 979 / 1,595 | gpt-oss:20b 2,034 / 3,151 |
| Staff intent (16 sentences x 3) | accepted | 70.8% | **93.8%** | 89.6% | |
| | p95 | 1,517 ms | 717 ms | 3,616 ms | |
| Three voices, one turn (12 turns) | p95 | 3.55 s | **2.09 s** | 3.72 s | budget 4 s |
| Scenario narrative (106 lines) | accepted / fallback to authored | 71.7% / 30 | **92.5% / 8** | 88.7% / 12 | |
| Text critic (214 seeded defects) | recall / false alarms | 68.8% / 3.1% | **89.3% / 0%** | 85.6% / 3.1% | |
| Scenario judge (30 seeded, 15 clean) | flagged / clean flagged | 36.7% / 0% | **90.0% / 0%** | 86.7% / 0% | |
| Trap writer (62 cases) | accepted | 23.5% | **71.2%** | not run | |
| Composition planner (10 requests) | gate pass / coverage / distinct block sets / p50 | 10/10, 1.0, **10**, 14.9 s | 10/10, 1.0, 4, 3.5 s | 10/10, 1.0, 9, 8.3 s | |
| Plan events and interactions (65 cases) | cases with events / refused | 62 / 0 | 61 / 0 | 62 / 0 | equal |
| Debrief rewrite (16 sessions) | accepted / p50 | **16/16** / 6,027 ms | 15/16 / 1,820 ms | 15/16 / 2,290 ms | all 59-63 mutations rejected |
| Performance interpreter (20 learners) | confirmed | 85.4% | 85.5% | 85.0% | |

## Selected (rule: `cloud_routing_report.py`)
`gemma4:31b-cloud` for patient dialogue, staff intent, narrative, critic, judge and trap writer; local for the planner, plan extras, debrief and interpreter (no gain by the rule's margin); local for analytics explanation and the spoken-order fallback (no benchmark). Real-time routes carry trainee speech and stay local until DT-248.

## What this does not show
Human quality ratings (blind, 50 needed), real audio, real trainee text, a Quest, the monthly allowance, behaviour under a longer outage, other days' latency. Free-plan latency varies by day: the debrief retry maximum was 17 s on both cloud models.

## Second part: the prompts of ADR-0026, the three voices and the whole path (25 October 2026, synthetic data)
Prompts were rewritten to state the role, the situation, what may and may not be said, the persona, uncertainty, out-of-scope remarks and the output (no extra models, no keyword gates). `reports/ai/cloud-routing-benchmark.json` holds the figures of both prompt generations. They are development figures: the prompts were adjusted on these same sets.

| Task | Measure | Local, prompt it keeps | gemma4:31b-cloud, first prompt | gemma4:31b-cloud, shipped prompt |
|---|---|---|---|---|
| Patient dialogue (82 + 82) | fact used / "cannot recall" when withheld / leaks | 74 / 76 / 1 (v1) | 82 / 81 / 0 | 80 / 80 / 0; median 661 ms, p95 3,337 ms |
| Staff intent (16 x 3) | accepted | 70.8% (v1) | 93.8% | 93.8% to 100% over two runs |
| Staff silent when not addressed (8 x 3) | correct | not offered (58% when offered) | not offered | 100% |
| Three voices, 12 turns | p95 | 3.55 s | 2.09 s | 1.67 s |
| Narrative (106 lines) | accepted / fallback to authored | 88.7% / 12 (v2 helps local too; v1 71.7% / 30) | 92.5% / 8 | 97.2% / 3 |
| Critic (215 seeded) | recall / false alarms | 68.8% / 3.1% (v1) | 89.3% / 0% | 87.9% / 0% |
| Judge (30 seeded) | flagged among the missed / clean flagged | 63% / 0% (v2; v1 37%) | 92.6% / 0% | 100% / 0% |
| Trap writer (62 cases) | accepted | 45.5% (v2; v1 23.5%) | 71.2% | 73.5% |

A single combined call (patient and staff together) was built and measured: withheld "cannot recall" 75 of 82 against 80, staff 93.8% against 100%, 0.6 s faster; removed. The longer prompts cost the 4B local model 1.3 s a turn and lose accuracy, so dialogue, staff and critic keep the first prompts locally (registry scope `cloud`).

### The whole path, synthetic (`reports/ai/e2e-synthetic.json`, 8 questions, recogniser on the processor, nothing else heavy running)
A synthetic voice speaks an invented trainee question; the recogniser hears it (8 of 8 heard); the patient and staff answer through the production routes; the local voice speaks the replies. Cloud routing off: dialogue median 2.9 s (p95 3.5 s), the patient alone, on the local model. Routing on with the privacy gate closed (a real deployment): the same, 0 cloud calls (one local answer took 6.5 s). Routing on and the deployment declared synthetic: dialogue median 1.9 s, 6 of 8 answered by the cloud model, 2 fell back to the local model inside the same request (a reply the grounding check refused, and a call slower than the 4 s live timeout), which cost 7.5 s and 4.3 s; the nurse spoke only for the pulse question, the anaesthetist only for the call for help, and the other staff stayed silent. From the end of the question to the first voice ready: median 3.5 s (cloud) against 4.4 s (local). Recognition took about 8 s on the processor; on the card it is 0.6 s (`reports/ai/asr-wer.json`), which was not re-run because the card libraries are not installed. A first run with Docker Desktop running on the same machine made the local model 1.4 s slower and made three teleport tests of the Unity PlayMode suite fail by timing; with it stopped both were normal, so latency and PlayMode figures here are from a machine with nothing else heavy running.
