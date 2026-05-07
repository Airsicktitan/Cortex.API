using Cortex.API.Models;

namespace Cortex.API.Infrastructure;

/// <summary>
/// Safe, generic Syniti / data-governance concepts for reviewer context only.
/// No customer content — curated phrasing suitable for demos and internal use.
/// </summary>
public static class SynitiKnowledgeCuratedCatalog
{
    public sealed record Definition(
        string Term,
        SynitiKnowledgeCategory Category,
        string ShortDefinition,
        string? ReviewerGuidance,
        string? RelatedTerms,
        string? ExamplePhrases,
        string? Aliases,
        string? SuggestedReviewerChecks,
        string? MissingContextQuestions);

    /// <summary>Pipe (|) separates checklist items for storage and APIs.</summary>
    public static IReadOnlyList<Definition> CuratedEntries { get; } =
    [
        new(
            Term: "Syniti",
            Category: SynitiKnowledgeCategory.Platform,
            ShortDefinition:
            "Syniti provides data migration, governance, and quality tooling used to design, validate, and execute controlled moves of data between systems.",
            ReviewerGuidance:
            "Clarify whether the request is product/platform usage, configuration, or operational support. Confirm environment, scope, and who owns business sign-off.",
            RelatedTerms: "DSP; ADM; data governance",
            ExamplePhrases: "syniti platform; syniti dsp; migrate program",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm whether the work spans design, build, testing, or production cutover.|Identify the owning team for configuration versus business rules.|Confirm any migration wave or cutover timing assumptions.",
            MissingContextQuestions:
            "Which program or workstream does this apply to?|Is this about tooling access, configuration change, or defect triage?"),

        new(
            Term: "DSP",
            Category: SynitiKnowledgeCategory.Platform,
            ShortDefinition:
            "Syniti DSP is a platform for orchestrating migration activities such as mapping, enrichment, validation, and load cycles with auditable controls.",
            ReviewerGuidance:
            "Treat as platform-level work: confirm scope (which objects and environments), validation expectations, and whether DSP rules or jobs are in scope.",
            RelatedTerms: "ADM; validation; mapping",
            ExamplePhrases: "dsp rule; dsp validation; syniti dsp",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm which DSP objects, jobs, or rules are referenced.|Confirm non-production versus production impact.|Confirm tester or business reviewer involvement.",
            MissingContextQuestions:
            "Which DSP area or job type is involved?|Is this a new build issue or a production operations issue?"),

        new(
            Term: "ADM",
            Category: SynitiKnowledgeCategory.Platform,
            ShortDefinition:
            "Advanced Data Migration (ADM) is a structured approach to planning and executing data migration with explicit scope, waves, and governance checkpoints.",
            ReviewerGuidance:
            "Confirm migration wave, object scope, and readiness criteria. Separate technical execution issues from business sign-off gaps.",
            RelatedTerms: "ADMM; migration waves; cutover",
            ExamplePhrases: "advanced data migration; adm scenario",
            Aliases: "ADMM",
            SuggestedReviewerChecks:
            "Confirm wave or scenario scope.|Confirm whether the issue is data, tooling configuration, or process ownership.|Confirm reconciliation or mock-load evidence if claimed.",
            MissingContextQuestions:
            "Which wave or milestone is affected?|What readiness checkpoint is blocked?"),

        new(
            Term: "Source-to-target mapping",
            Category: SynitiKnowledgeCategory.Mapping,
            ShortDefinition:
            "Defines how a source field or value contributes to a target field, including transformations, defaults, or lookups when direct copy does not apply.",
            ReviewerGuidance:
            "Confirm source field, target field, transformation or defaulting logic, and who validated business correctness before approval.",
            RelatedTerms: "Value mapping; transform logic; rules",
            ExamplePhrases: "source-to-target; source to target mapping; s2t mapping",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm source and target fields are identified.|Confirm transformation or defaulting logic is documented.|Confirm business owner has reviewed the mapping.|Confirm downstream load or reconciliation impact is understood.",
            MissingContextQuestions:
            "What is the authoritative source field and the target field?|Who owns business validation for this mapping?"),

        new(
            Term: "Value Mapping",
            Category: SynitiKnowledgeCategory.Mapping,
            ShortDefinition:
            "Maps source values to allowed target values, often via lookup tables, rules, or controlled translations.",
            ReviewerGuidance:
            "Validate lookup coverage, exception handling, and stewardship for value lists that change over time.",
            RelatedTerms: "Lookup; source-to-target mapping",
            ExamplePhrases: "value mappings; translate source values to target",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm which code values are in scope.|Confirm lookup maintenance ownership.|Confirm how unmapped values are handled.",
            MissingContextQuestions:
            "Are new codes introduced or only corrections to existing mappings?"),

        new(
            Term: "Transform logic",
            Category: SynitiKnowledgeCategory.Mapping,
            ShortDefinition:
            "Rules or procedures that derive a target value from one or more source fields, including concatenation, formatting, or conditional logic.",
            ReviewerGuidance:
            "Confirm the rule is documented, testable, and owned; flag risky implicit defaults.",
            RelatedTerms: "Validation rule; enrichment",
            ExamplePhrases: "transform rule; transformation rule; transform logic",
            Aliases: null,
            SuggestedReviewerChecks:
            "Capture the explicit rule or formula intent.|Confirm sample before/after examples.|Confirm owner for changes to the rule.",
            MissingContextQuestions:
            "Is this net-new logic or a correction to an existing rule?"),

        new(
            Term: "Validation rule",
            Category: SynitiKnowledgeCategory.Validation,
            ShortDefinition:
            "A check that rejects or flags records when data does not meet stated business constraints before load or handoff.",
            ReviewerGuidance:
            "Separate rule design errors from data quality problems; confirm exception handling and who approves overrides.",
            RelatedTerms: "Business validation; data quality rule",
            ExamplePhrases: "validation rules failing; rule failed validation",
            Aliases: null,
            SuggestedReviewerChecks:
            "Identify failing constraint and example records.|Confirm whether failure is expected cleansing work or a rule defect.|Confirm override process if applicable.",
            MissingContextQuestions:
            "Hard stop or warning?|Which object and field triggered the failure?"),

        new(
            Term: "Business validation",
            Category: SynitiKnowledgeCategory.Validation,
            ShortDefinition:
            "Human or procedural confirmation that data meaning and use in the target process are acceptable, beyond automated technical checks.",
            ReviewerGuidance:
            "Confirm the right business owner, evidence pack, and whether validation is blocking cutover or reporting.",
            RelatedTerms: "Data steward review; governance approval",
            ExamplePhrases: "business sign-off; business validation needed",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm named business approver.|Confirm what evidence is required (samples, metrics, screenshots).|Confirm timeline versus cutover readiness.",
            MissingContextQuestions:
            "What decision is awaiting approval?|What artifact proves validation completed?"),

        new(
            Term: "Load error",
            Category: SynitiKnowledgeCategory.LoadProcessing,
            ShortDefinition:
            "A failure when applying staged or transformed data to a target, often surfaced as a rejected batch, rejected record, or technical error message.",
            ReviewerGuidance:
            "Capture object, record keys, error category, and whether retry or fix-forward applies; separate environmental issues from mapping defects.",
            RelatedTerms: "Mock load; reconciliation",
            ExamplePhrases: "load failure; load rejected; failed load",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm target object and approximate volume impacted.|Confirm whether the failure is reproducible.|Confirm owner for remediation.",
            MissingContextQuestions:
            "Production or non-production load?|Batch id or correlation available?"),

        new(
            Term: "Data Quality Rule",
            Category: SynitiKnowledgeCategory.DataQuality,
            ShortDefinition:
            "A rule that measures or enforces expected data conditions, often used ahead of loads or during steady-state monitoring.",
            ReviewerGuidance:
            "Tie failures to stewardship and remediation paths; avoid treating every warning as a defect.",
            RelatedTerms: "Validation rule; defects",
            ExamplePhrases: "dq rules; data quality rules; dq rule",
            Aliases: null,
            SuggestedReviewerChecks:
            "State threshold or scope of the DQ check.|Confirm data owner for remediation.|Confirm whether issue blocks migration milestones.",
            MissingContextQuestions:
            "Is this a new DQ gate or a regression on an existing check?"),

        new(
            Term: "Reconciliation",
            Category: SynitiKnowledgeCategory.Reconciliation,
            ShortDefinition:
            "Comparing source and target balances, counts, or key populations to prove completeness and correctness for a scope.",
            ReviewerGuidance:
            "Confirm reconciliation scope, tolerances, and whether differences are explanatory work items or blocking issues.",
            RelatedTerms: "Mock load; cutover readiness",
            ExamplePhrases: "reconciliation mismatch; recon variance; recon differences",
            Aliases: null,
            SuggestedReviewerChecks:
            "Define population and keys used.|Document explained vs unexplained variance.|Confirm who signs off on exceptions.",
            MissingContextQuestions:
            "Financial reconciliation or master-data reconciliation?|Which period or wave does this cover?"),

        new(
            Term: "Mock load",
            Category: SynitiKnowledgeCategory.Readiness,
            ShortDefinition:
            "A controlled rehearsal load into a non-production target to validate mappings, performance, and operational readiness.",
            ReviewerGuidance:
            "Confirm objectives, success criteria, and whether issues require mapping fixes versus data fixes.",
            RelatedTerms: "Cutover readiness; load error",
            ExamplePhrases: "mock load cycle; rehearsal load; trial load",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm mock environment and cutover relevance.|List top failures and owners.|Confirm retest plan.",
            MissingContextQuestions:
            "Was this an end-to-end mock or a partial object scope?"),

        new(
            Term: "Cutover readiness",
            Category: SynitiKnowledgeCategory.Readiness,
            ShortDefinition:
            "The agreed set of technical and business checkpoints that must be satisfied before switching to the new system or freezing legacy entry.",
            ReviewerGuidance:
            "Identify blocking vs non-blocking items; confirm rollback and communication expectations.",
            RelatedTerms: "Mock load; wave; cutover",
            ExamplePhrases: "go-live readiness; cutover checklist",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm which readiness gate is blocked.|Confirm owners and dates.|Confirm dependencies on migrations, reporting, or integrations.",
            MissingContextQuestions:
            "Hard cutover date or window?|Which checkpoint failed?"),

        new(
            Term: "Field ownership",
            Category: SynitiKnowledgeCategory.FieldOwnership,
            ShortDefinition:
            "Clarity about which role or team is authoritative for a field’s meaning, allowed values, and approval of changes.",
            ReviewerGuidance:
            "Confirm accountable owner versus contributors; aligns governance approvals to the right forum.",
            RelatedTerms: "Data steward review; business validation",
            ExamplePhrases: "field-level ownership; owning team for field",
            Aliases: null,
            SuggestedReviewerChecks:
            "Name the data steward or business owner for the field.|Confirm escalation when owners disagree.|Confirm impact on downstream consumers.",
            MissingContextQuestions:
            "Is ownership disputed or only undocumented?"),

        new(
            Term: "Data steward review",
            Category: SynitiKnowledgeCategory.Governance,
            ShortDefinition:
            "A structured review by data stewardship to confirm meaning, policy compliance, and fit-for-use for governed attributes.",
            ReviewerGuidance:
            "Ensure the right steward forum, evidence, and decision outcome are recorded.",
            RelatedTerms: "Governance approval; field ownership",
            ExamplePhrases: "steward approval; stewardship review",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm steward group or RACI.|Confirm what changed versus prior baseline.|Record decision and exceptions.",
            MissingContextQuestions:
            "Which data domain does this attribute belong to?"),

        new(
            Term: "Governance approval",
            Category: SynitiKnowledgeCategory.Governance,
            ShortDefinition:
            "Recorded consent that a change meets policy controls, often required for master data affecting finance, compliance, or reporting.",
            ReviewerGuidance:
            "Separate local process approval from enterprise policy gates when both apply.",
            RelatedTerms: "Business validation; data steward review",
            ExamplePhrases: "governance sign-off; policy approval",
            Aliases: null,
            SuggestedReviewerChecks:
            "Identify policy or control cited.|Confirm approver role versus delegate.|Confirm audit trail expectation.",
            MissingContextQuestions:
            "Which policy or standard applies?|Is this emergency or standard path?"),

        new(
            Term: "Data enrichment",
            Category: SynitiKnowledgeCategory.Mapping,
            ShortDefinition:
            "Deriving or supplementing values from additional references, hierarchies, or external sources to produce a complete target representation.",
            ReviewerGuidance:
            "Confirm enrichment sources, refresh cadence, and failure behavior when lookups miss.",
            RelatedTerms: "Transform logic; value mapping",
            ExamplePhrases: "enrichment step; enrich records",
            Aliases: null,
            SuggestedReviewerChecks:
            "List enrichment sources and keys.|Confirm handling when enrichments are missing.|Confirm owners for source maintenance.",
            MissingContextQuestions:
            "Net-new enrichment or change to existing enrichment?"),

        new(
            Term: "Defect",
            Category: SynitiKnowledgeCategory.DataQuality,
            ShortDefinition:
            "An agreed issue where behavior deviates from expected design, requiring remediation and retest.",
            ReviewerGuidance:
            "Capture reproducible steps, severity, and whether workaround exists; distinguish defects from data cleanup work.",
            RelatedTerms: "Load error; validation rule",
            ExamplePhrases: "defect triage; opened defect",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm expected versus actual.|Confirm environment and build.|Link to retest evidence when closed.",
            MissingContextQuestions:
            "Blocking migration path or cosmetic?|Regression or new behavior?"),

        new(
            Term: "Wave",
            Category: SynitiKnowledgeCategory.Migration,
            ShortDefinition:
            "A scoped migration slice grouping objects, sites, or timelines so execution and validation can be managed incrementally.",
            ReviewerGuidance:
            "Confirm wave membership, dependencies, and whether issues are local to the wave or systemic.",
            RelatedTerms: "Cutover; mock load",
            ExamplePhrases: "migration wave; rollout wave; wave plan",
            Aliases: null,
            SuggestedReviewerChecks:
            "Identify wave id or scope criteria.|Confirm predecessors completed.|Confirm business readiness for this slice.",
            MissingContextQuestions:
            "Which geography or business unit is in this wave?"),

        new(
            Term: "Cutover",
            Category: SynitiKnowledgeCategory.Cutover,
            ShortDefinition:
            "The controlled transition point when processing or authority shifts to the new target for a defined scope.",
            ReviewerGuidance:
            "Clarify freeze windows, rollback triggers, and communication plans; avoid mixing readiness discussion with unrelated defects.",
            RelatedTerms: "Cutover readiness; mock load",
            ExamplePhrases: "cutover weekend; cut-over window",
            Aliases: null,
            SuggestedReviewerChecks:
            "Confirm scope of cutover versus hypercare.|Confirm decision makers on go/no-go.|Confirm parallel run expectations if any.",
            MissingContextQuestions:
            "Hard cutover or phased?|Which regions or plants are included?"),

        new(
            Term: "Data governance",
            Category: SynitiKnowledgeCategory.Governance,
            ShortDefinition:
            "Policies, stewardship, and controls that guide how data is defined, accessed, changed, and quality-assured across the organization.",
            ReviewerGuidance:
            "Ground requests in policy reference, stewardship model, and measurable outcomes—not generic 'fix data' language.",
            RelatedTerms: "Governance approval; data steward review",
            ExamplePhrases: "governance forum; governance policy",
            Aliases: null,
            SuggestedReviewerChecks:
            "Name relevant policy or standard.|Confirm steward or forum outcome required.|Confirm documentation expectations.",
            MissingContextQuestions:
            "Is this an enterprise policy question or project-local workaround?"),
    ];
}
