using System.Text;

namespace Cortex.API.Services;

public interface ITicketTriagePromptBuilder
{
    string BuildSystemPrompt(TicketTriageVocabularySnapshot vocabulary);
}

public sealed class TicketTriagePromptBuilder : ITicketTriagePromptBuilder
{
    public string BuildSystemPrompt(TicketTriageVocabularySnapshot vocabulary)
    {
        var sb = new StringBuilder(2048);

        sb.AppendLine("You are a decisive intake assistant for CORTEX ticket reviewers.");
        sb.AppendLine("Return one JSON object only. No markdown. No prose outside JSON. No extra keys.");
        sb.AppendLine();
        sb.AppendLine("Decision rules:");
        sb.AppendLine("- Be reviewer-first, confident, concise, and operationally direct.");
        sb.AppendLine("- Do not hedge. Do not use words like appears, seems, likely, may, might, probably, or approximately.");
        sb.AppendLine("- Use only facts and reasonable inferences from the ticket text.");
        sb.AppendLine("- If the ticket is thin or ambiguous, still choose the best valid configured values and use missingDetails to name what must be clarified.");
        sb.AppendLine();
        sb.AppendLine("Controlled vocabulary rules:");
        sb.AppendLine("- Priority and status values come from live Cortex configuration and are the only allowed vocabulary.");
        sb.AppendLine("- Output configured values exactly as listed below.");
        sb.AppendLine("- Do not invent, normalize, approximate, translate, abbreviate, or paraphrase configured values.");
        sb.AppendLine("- A value that is not listed below is invalid.");
        sb.AppendLine();
        sb.AppendLine("Valid priorities:");

        foreach (var priority in vocabulary.Priorities)
        {
            sb.AppendLine($"- {priority.Name} (target: {priority.TargetHours}h, warning: {priority.WarningHours}h)");
        }

        sb.AppendLine();

        if (vocabulary.Statuses.Count > 0)
        {
            sb.AppendLine("Valid statuses:");

            foreach (var status in vocabulary.Statuses.OrderBy(x => x.SortKey))
            {
                if (string.IsNullOrWhiteSpace(status.Description))
                {
                    sb.AppendLine($"- {status.Name}");
                }
                else
                {
                    sb.AppendLine($"- {status.Name}: {status.Description}");
                }
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("No valid statuses are configured in this environment.");
            sb.AppendLine();
        }

        sb.AppendLine("Strict JSON contract:");
        sb.AppendLine("{");
        sb.AppendLine("  \"summary\": string,");
        sb.AppendLine("  \"priority\": string,");
        sb.AppendLine("  \"priorityReason\": string,");

        if (vocabulary.Statuses.Count > 0)
        {
            sb.AppendLine("  \"status\": string,");
        }

        sb.AppendLine("  \"missingDetails\": [string, string, ...],");
        sb.AppendLine("  \"potentialSlaRisk\": \"Low\" | \"Medium\" | \"High\",");
        sb.AppendLine("  \"slaRiskReason\": string");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Field rules:");
        sb.AppendLine("- summary: Required. Exactly one sentence. State the ask directly with no filler.");
        sb.AppendLine("- priority: Required. Must be exactly one configured priority value.");
        sb.AppendLine("- priorityReason: Required. One concise sentence explaining why that configured priority fits.");

        if (vocabulary.Statuses.Count > 0)
        {
            sb.AppendLine("- status: Required. Must be exactly one configured status value.");
        }
        else
        {
            sb.AppendLine("- status: Do not include this property because no statuses are configured.");
        }

        sb.AppendLine("- missingDetails: Required. JSON array containing 2 to 4 short, actionable strings.");
        sb.AppendLine("- potentialSlaRisk: Required. Must be exactly Low, Medium, or High.");
        sb.AppendLine("- slaRiskReason: Required. One concise sentence explaining the risk tier.");

        return sb.ToString();
    }
}
