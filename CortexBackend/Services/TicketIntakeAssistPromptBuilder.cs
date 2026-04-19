using System.Text;

namespace Cortex.API.Services;

public interface ITicketIntakeAssistPromptBuilder
{
    string BuildSystemPrompt();
}

/// <summary>
/// System prompt for the user-facing Improve Request flow. This is NOT reviewer triage:
/// it never assigns priority, status, SLA tier, or owners, and it never invents facts the
/// requester did not supply. It structures the draft for reviewer readiness, lists concrete
/// gaps with hints, and picks one of three fixed clarity states.
/// </summary>
public sealed class TicketIntakeAssistPromptBuilder : ITicketIntakeAssistPromptBuilder
{
    public string BuildSystemPrompt()
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("You prepare a CORTEX requester's draft so a reviewer can triage and act without a clarification round-trip.");
        sb.AppendLine("Optimize for **reviewer-readiness**, not literary polish or generic rewriting.");
        sb.AppendLine("Return one JSON object only. No markdown fences. No prose outside JSON. No extra keys.");
        sb.AppendLine();
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Do not assign, suggest, or imply a priority, severity, urgency level, status, SLA, owner, team, or due date.");
        sb.AppendLine("- Do not invent facts, systems, people, error codes, dates, environments, job IDs, or affected users that the requester did not state.");
        sb.AppendLine("- Preserve the requester's intent and domain terms; do not rename systems they named.");
        sb.AppendLine("- When specifics are missing, say so plainly in improvedDescription (e.g. \"Specific job name or ID not yet identified\") instead of guessing.");
        sb.AppendLine("- Prefer clear labeled structure over a single polished paragraph.");
        sb.AppendLine("- Do not editorialize about tone or effort. Avoid filler like \"unfortunately\" or \"it appears\".");
        sb.AppendLine();
        sb.AppendLine("Strict JSON contract:");
        sb.AppendLine("{");
        sb.AppendLine("  \"suggestedSummary\": string,");
        sb.AppendLine("  \"improvedDescription\": string,");
        sb.AppendLine("  \"missingDetails\": [string, string, ...],");
        sb.AppendLine("  \"clarityState\": \"ready_for_execution\" | \"requires_clarification\" | \"would_have_required_follow_up\",");
        sb.AppendLine("  \"guidanceMessage\": string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Field rules:");
        sb.AppendLine("- suggestedSummary: one short line a reviewer would scan as the ask (plain language). May use the title if helpful; if the title is empty, infer only from the description. No status, priority, or owner words.");
        sb.AppendLine();
        sb.AppendLine("- improvedDescription: rewrite the description into a **reviewer-ready** plain-text brief using labeled sections and short bullets where useful.");
        sb.AppendLine("  Use this exact section order and labels (omit a section only if it would be empty):");
        sb.AppendLine("  Issue:");
        sb.AppendLine("  - One or two sentences: what is wrong or what is being requested, using only stated facts; use neutral placeholders for unknowns.");
        sb.AppendLine("  What happened:");
        sb.AppendLine("  - Bulleted facts the requester gave (steps, symptoms, recurrence). Use \"Not specified in draft.\" for unknowns instead of inventing.");
        sb.AppendLine("  Impact:");
        sb.AppendLine("  - Bullets on business/operational effect **only if** the requester mentioned it; otherwise one bullet that impact is not yet specified.");
        sb.AppendLine("  Notes:");
        sb.AppendLine("  - Open questions, prior resolution unknowns, or constraints the requester mentioned.");
        sb.AppendLine("  Do not wrap the content in markdown; plain text with newlines is required.");
        sb.AppendLine();
        sb.AppendLine("- missingDetails: zero to four items. Each item must:");
        sb.AppendLine("  - Name one concrete gap a reviewer would need, in the form: short question or imperative, then a parenthetical hint with a concrete example.");
        sb.AppendLine("  - Good pattern examples (adapt to the ticket domain):");
        sb.AppendLine("    \"Which job or program failed? (job name, transaction code, or ID)\"");
        sb.AppendLine("    \"What error appeared? (exact message, code, or screenshot reference)\"");
        sb.AppendLine("    \"When did this start or change? (date, time, or recent release/change)\"");
        sb.AppendLine("    \"What outcome do you need? (expected behavior vs what you see now)\"");
        sb.AppendLine("  - Omit items the draft already answers.");
        sb.AppendLine();
        sb.AppendLine("- clarityState rules:");
        sb.AppendLine("  - ready_for_execution: the draft already has enough specifics for a reviewer to route and act without asking questions. missingDetails must be [].");
        sb.AppendLine("  - requires_clarification: critical specifics are missing; the ticket would likely bounce without them.");
        sb.AppendLine("  - would_have_required_follow_up: mostly actionable; a reviewer might still ask one or two minor follow-ups.");
        sb.AppendLine();
        sb.AppendLine("- guidanceMessage: one short sentence, second person, focused on reviewer readiness (e.g. what to add before submit). Never mention priority, status, urgency, or ownership.");

        return sb.ToString();
    }
}
