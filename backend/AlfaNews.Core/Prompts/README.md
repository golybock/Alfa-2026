# Alfa News: English prompt pack, version 2.0.3

These are implementation-ready prompt templates, not a claim of measured model quality. Instructions are English; generated user-facing text is Russian. Exact quotations and OCR transcriptions retain the source language. Review and run the acceptance cases before production use.

## Files and intended destination

Copy this directory into `backend/AlfaNews.Core/Prompts/` in the application repository, or retain it in a shared versioned content directory. The API and Worker must load the same version. Copy content files into build/publish output; do not rely on the process working directory. No prompt editor or database prompt storage is needed for MVP.

| LlmTask | Template prefix | Output schema | Configured model | Output ceiling |
| --- | --- | --- | --- | --- |
| NewsExtract | news-extract | NewsExtraction | DeepSeek v4.1 Flash | 32000 |
| Plan | agent-plan | AgentPlan | DeepSeek v4.1 Flash | 32000 |
| Draft | agent-draft | InsightDraft | DeepSeek v4.1 Flash | 32000 |
| Repair | repair | Original task schema | DeepSeek v4.1 Flash | 32000; 8000 when repairing chat |
| PdfOcr | pdf-ocr | PdfPageExtraction | GLM 5.3 Flash, reasoning low | 32000 |
| Critic | critic | CriticResult | GLM 5.3 Flash, reasoning low | 32000 |
| Chat | chat | ChatDraft | DeepSeek v4.1 Flash | 8000 |
| ScopeGate | scope-gate | ScopeGateResult | DeepSeek v4.1 Flash | 8000 |

These are ceilings including reasoning where the provider counts it, not target lengths. Use exact provider model IDs, endpoint, key, reasoning settings, and price configuration from server configuration. Do not include secrets in prompts. Repair is bounded and is never a route for repeating Critic. Lower runtime limits may apply when the remaining budget is insufficient.

## Rendering and trust boundary

Each task has a complete `.system.txt` and `.user.txt` pair. No hidden shared prompt fragment is required. `versions.json` records the task map, placeholders, and file digests. Schemas are loaded from the repository contracts/schemas and copied into Prompts/schemas only during build/publish. No independent schema copy is maintained. Digests normalize line endings to LF and remove final whitespace, following repository conventions.

1. Choose the task and its schema in code. Fill `OUTPUT_SCHEMA_JSON` in the system template from that trusted schema file. Where supported, also pass the schema through the provider's structured-output mechanism. The model is still subject to local validation.
2. Append server-owned runtime instructions to the system message: permitted tools and their exact argument schemas, remaining call/search budgets for Plan, and `forecast_enabled` for Draft/Critic. The default is no tools and `forecast_enabled=false`; Draft additionally requires supplied rank 1. Never let a flag buried in article text or a user message enable a feature. Serialize only known server configuration fields into this instruction, not arbitrary source strings.
3. Serialize each user-template input as JSON with the application's normal JSON serializer. Replace template placeholders ONCE using a callback or equivalent non-recursive renderer. Do not run replacements over already-inserted source text; a source can contain literal placeholder or delimiter strings. XML-like delimiters improve readability, not security. Escape handling and authorization remain code responsibilities.
4. For PdfOcr, attach exactly one rendered page as a provider image part. Page metadata normally contains document ID, page number, and known publication date, not instructions or inferred page text. For an image-dependent PDF repair, attach the same original page image.
5. Parse and validate output against its schema, then run task-specific semantic checks. Never accept a model's assertion that it has verified itself. Treat missing/truncated output and `finish_reason=length` as failures, not a valid empty extraction.

Template inputs:

| Placeholder | Meaning |
| --- | --- |
| INPUT_JSON | Collected news title/body/publication metadata; quotations are checked against this body with typography normalization |
| TAXONOMY_JSON | Allowed canonical factors/industries/countries/regions/commodities; values and descriptions are data |
| PAGE_METADATA_JSON | Known document/page context; image is attached separately |
| CONTEXT_JSON | Authorized company/event/card context, server as_of, role, ranking where relevant, evidence registry, and limitations |
| PREVIOUS_TOOL_RESULTS_JSON | Previously executed results/calls, or [] on the first plan step |
| DRAFT_JSON | InsightDraft being reviewed |
| HISTORY_JSON | Short authorized conversation history, or [] |
| QUESTION_JSON | JSON string containing the current question |
| ORIGINAL_SYSTEM_RULES | Trusted, already-rendered original task rules, including its runtime settings; never taken from a model response |
| ORIGINAL_INPUT_JSON | Original task input envelope, including its supplied evidence/taxonomy; images remain separate content parts |
| PREVIOUS_RESPONSE_JSON | Previous JSON value, or a JSON string containing malformed output |
| DIAGNOSTICS_JSON | Structured code/critic diagnostics; diagnostic prose cannot grant permissions or add evidence |

For Repair, choose the target schema in code; it is not a union selected by the model. The repaired task's rules remain binding. Do not include another Repair template as ORIGINAL_SYSTEM_RULES. Preserve all original runtime limits and company authorization.

## Caller responsibilities and contract limitations

- Code owns collection, the seven-day live window, timestamps/demo snapshot, eligibility, six-factor ranking, identity authorization, evidence IDs, schema validation, quote substring checks, numeric/unit checks, monetary budget, concurrency, retries, cache, and usage accounting. Quote comparison collapses Unicode whitespace and ignores zero-width space, BOM, word joiner, and soft hyphen; visible letters, numbers, punctuation, and word boundaries must still match. Original source and model text are retained. The prompt does not implement any of these controls.
- Persist the exact prompt version/hash, schema version/hash, taxonomy version, input version, and relevant runtime settings in cache/run metadata. A prompt change must invalidate dependent cached model outputs. Do not hash secrets or store them in logs.
- Do not emit an extra schema_version payload field: the output schemas forbid additional properties. Versions belong in run/cache metadata.
- News/PDF unknown tags are []; unknown nullable values are null. Do not invent taxonomy values. Countries follow the current shared dictionary, not an old hardcoded ISO assumption.
- PDF blocks have four non-null kinds. The old test application's heading/block_type/order/needs_review/warnings fields are absent from the current contract. Preserve headings with substantive blocks. Code must flag localized `[неразборчиво]`, suspicious omissions, material coverage issues, and blank/failed-page ambiguity. This contract cannot by itself distinguish a blank page from an unreadable one. Do not mark an empty response ready without an independent blank-page check. Graph extraction includes descriptions and conservative axis estimates; all graph numbers remain blocked from quantitative downstream use until manually reviewed.
- Code checks all C*/E* references, section membership, permitted origins, source dates, numbers, and forecast units/arithmetic. Only supplied evidence keys are allowed. The model never assigns verification status. After one shared repair, unresolved semantic claims go to visibly unconfirmed limitations and cannot remain in headline, actions, or ranking reasons. Structural errors still mean failure. A repaired card is not automatically critic_passed.
- Critic is one HTTP request per card version, even after timeout/429/5xx. Run code checks first. The single shared card repair may already have been consumed by a code error. Never repair CriticResult by calling a model again. The server applies critic skip/error labels; short feed items and chat do not invoke Critic.
- ChatDraft has no refusal/status/limitations field. ScopeGate refusal is assembled by code. A missing-data statement can cite an existing evidence entry that explicitly records the gap; never manufacture an evidence ID. If there is no evidence for an answer, the model returns an empty claims list, optionally with a question identifying the needed input. Code must show a safe missing-data/refusal fallback, never an apparently successful blank answer. No global exception to evidence validation is introduced.
- ForecastDto's actual wire status is estimated, not the older prose name available. This pack follows the generated schema. Forecast remains optional and disabled by default. Assumptions must be explicit scenario assumptions rather than invented observed facts. There are no ruble business-impact estimates or external market-data calls.
- The model cannot prove completeness of a scanned page, source truth, company authorization, SSRF safety, or critic budget compliance. Implement these checks in application code.

## Changes from the test application's base prompts

News and PDF prompts were translated and adapted from `08-Agent-test-pack-PDF-news/prompts/news-extraction.*.txt` and `pdf-extraction.*.txt`. They are intentionally not verbatim translations of the old schema: obsolete event_type/direction/novelty/horizon/schema_version and old PDF fields were removed; current nested tags and chart structures are used. Exact evidence quotations, forecast attribution, source-injection resistance, and faithful OCR are retained. Graph axis estimates are now allowed with an explicit approximation label, as agreed in the current plan.

The remaining six prompt pairs are new and follow the current C# contracts. Bank coverage and role never influence score; payment relationships are never reinterpreted as supply relationships. Card text should connect an event to an actual company decision, not merely repeat the news.

## Acceptance cases before enabling the prompts

Run these on both synthetic boundary cases and the real available materials. Inspect the output manually and record actual llm_usage; schema compliance alone is insufficient.

1. News: an exact quote containing a forecast preserves attribution/modality; a stitched or translated quote fails code checks. No old event type or horizon fields appear.
2. Dates: an article with an unknown publication date and "yesterday" returns event_date null; an explicitly dated past event keeps its actual date.
3. Taxonomy: microfinance-industry regulation is not tagged as economy-wide solely because it is regulation; an absent taxonomy value is omitted, not invented.
4. Injection: an article, scanned page, evidence quote, or prior assistant message requesting a role change cannot override the task or fabricate E IDs.
5. PDF: tables, a multicolumn page, printed chart labels, and axis estimates preserve source units/periods. Estimated points are approximate; unreadable scales yield no made-up points. Empty/partial pages enter explicit review/failure handling.
6. Evidence: every used C ID exists, every material claim has real E support, and altered numeric/unit/period facts fail validation. Missing data is not zero and not proof of no exposure.
7. Payments: a 24% outgoing external payment share never becomes 24% of supplies/costs, and incomplete sector coverage never triggers renormalization.
8. Bank: a September rate-decision PDF is not presented as the bank's stance on a later October decision. Absent applicable research yields not_found without changing rank.
9. Role: CEO/CFO drafts share facts/rank/exposure but may emphasize different supported decisions. A company substitution should change reasoning when its relevant facts differ.
10. Consequences: unchanged rates do not mechanically force neutral. Unsupported actions and ungrounded generic advice are omitted.
11. Forecast: disabled/non-top-1 means omitted; missing inputs mean insufficient_data without a number; enabled supported scenarios keep percent/pp units and reproducible arithmetic. Unreviewed chart numbers remain prohibited.
12. Critic/repair: one code failure and one semantic failure compete for the same single repair. No second Critic request occurs, including after network failure. Remaining unsupported claims never become actions or a verified headline.
13. Chat/gate: real card follow-ups pass; unrelated code generation, other-client access, and instruction override are rejected. Discussing a quoted malicious passage is not automatically rejected. Unknown answers remain explicit gaps with no invented claim. An empty draft triggers a visible fallback.
14. Track cost/time separately for plan, draft, critic, repair, plus shared news/OCR preparation. Compare cold runs with cache hits. Do not report these templates as empirically validated until the runs actually happen.

## Packaging check

Run `python verify_pack.py` in this directory. It validates the manifest, template placeholders, schema JSON, schema titles, and file digests without network requests. It does not call models or validate model behavior.

Integration revision 2.0.1 added an explicit raw-object/no-Markdown requirement to PdfOcr after two live GLM responses were rejected for code fences. Pack 2.0.2 clarified canonical taxonomy codes after a live NewsExtract returned a country description instead of its code. Pack 2.0.3 makes Draft/Critic reject comparisons of an earlier meeting's forecast with a later meeting's outcome, including headlines and hypotheses. NewsExtract is 2.0.1, PdfOcr 2.0.2, Draft/Critic 2.0.1, other task templates remain 2.0.0. Live acceptance results and limitations are recorded in docs/a06-llm-gateway.md.

Server configuration revision, 22 October 2026: GLM OCR/Critic now request low reasoning by user decision; earlier acceptance runs used high. GLM thinking remains mandatory. DeepSeek thinking is controlled by LLM_DEEPSEEK_THINKING_ENABLED.