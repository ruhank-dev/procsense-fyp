# What the relation-support metric measures (audit, 19 October 2026)

Synthetic, AI-judged, not independent. Data: the 86 relations the 4B extracted (`artifacts/ai-bench/kb-queue-4b.json`, git-ignored; summary in `reports/ai/kb-extract-4b-vs-9b.json`, which is historical since the 9B was retired, ADR-0019).

**Question.** The 4B's relations were "supported by both judging configurations" in only 30 percent of cases. Is that the extractor, the judge, a missing knowledge-base link, or the proxy?

**Findings (deterministic, on the 86 relations).**
* Every relation already passes the structural check (the quote is word for word in the source chunk, both ends appear in the quote): 86 of 86. So the low score is not made-up quotes.
* The sources are mostly terse case-data fragments, not prose: the median quote is 8 words and 48 of 86 quotes have 8 words or fewer ("Airway at risk: vomiting", "Hypovolaemia: tachycardia, low BP and slow capillary refill"). A judge that asks "does the quote state the claim" rejects a fragment that lists two things next to each other when the predicate (for example `is_a_sign_of`, `leads_to`) asserts a direction or a cause.
* In 18 of 86 relations the object appears before the subject in the quote for a directional predicate (reversed direction: "Acute kidney injury ... with dehydration" extracted as dehydration-caused-by AKI). Those are real extraction errors, about a fifth.
* The 4B judged by the 4B (30.2 percent both, 52.3 percent either) and judged by the 9B (30.2 and 52.3 percent) agree; with the 9B extracting and judging itself the figures were 24.1 and 52.9 percent. The judge was therefore not the main reason for the low figure.

**Conclusion.** The metric measures whether a short source fragment licenses a specific predicate and direction. It is a harsh, useful proxy for extraction quality on this source, not a measure of clinical correctness, and both models score about the same on it. The gate that matters (EX-10, at least 0.8 on 100 relations labelled by people) is unchanged and unmet. The queue stays a queue for a person to approve; nothing was added to the knowledge base from it. Missing knowledge-base links for the interaction proposals (P3-01) are a content-authoring problem, not an extraction problem: the extractor cannot supply clinically approved pairs from fragments like these.
