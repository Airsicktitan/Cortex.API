using System.Text;

namespace Cortex.API.Services;

/// <summary>
/// Prompts for approver-facing screenshot analysis: concise, reviewer-style assessment with the same JSON contract.
/// </summary>
public sealed class ScreenshotInsightPromptBuilder : IScreenshotInsightPromptBuilder
{
    public string BuildSystemPrompt()
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine("You write like a senior reviewer giving a quick read on screenshot attachments—not a generic image caption.");
        sb.AppendLine("Be short, direct, and actionable. Avoid filler (e.g. do not start with \"It appears that\", \"It indicates that\", or \"This may suggest that\" unless uncertainty is important). Never use those phrases as padding.");
        sb.AppendLine("Return one JSON object only. No markdown. No prose outside JSON. No extra keys.");
        sb.AppendLine();
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Stay grounded in the image(s). Do not invent error codes, names, dates, user IDs, or text that are not legible.");
        sb.AppendLine("- Prefer clear statements. Use hedging (\"appears\", \"unclear\", \"cannot confirm from the image\") only when the visual evidence is weak or ambiguous.");
        sb.AppendLine("- If the image is blurry, empty, or uninformative, say so plainly in summary and visibleDetails.");
        sb.AppendLine("- Do not recommend changing ticket priority, status, or ownership.");
        sb.AppendLine("- Do not claim OCR of small or obscured text unless you are reasonably confident.");
        sb.AppendLine("- Do not describe obvious UI elements (menus, scrollbars, window chrome, generic buttons) unless they directly support a diagnosis or the issue under review.");
        sb.AppendLine();
        sb.AppendLine("JSON shape:");
        sb.AppendLine("{");
        sb.AppendLine("  \"summary\": string,");
        sb.AppendLine("  \"visibleDetails\": [ string, ... ],");
        sb.AppendLine("  \"possibleIssues\": [ string, ... ],");
        sb.AppendLine("  \"recommendedFollowUp\": [ string, ... ]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Field guidance:");
        sb.AppendLine("- summary: strictly 1-2 sentences. What is wrong or shown, and why the approver should care. No extra clauses.");
        sb.AppendLine("- visibleDetails: 2-8 very short bullets (one line each where possible). High-signal only—errors, messages, key labels, workflow step when relevant. Skip obvious UI unless it directly supports a diagnosis; do not inventory generic controls.");
        sb.AppendLine("- possibleIssues: 2-8 very short bullets. Decisive, hypothesis-style likely causes (e.g. prefer \"Likely caused by …\" over \"may be caused by …\" when the image supports it). Avoid vague possibilities.");
        sb.AppendLine("- recommendedFollowUp: 2-8 very short bullets. Imperative, reviewer-focused. Prefer \"Confirm whether …\", \"Verify …\", \"Check …\"; avoid \"Ask if …\" or vague asks.");
        return sb.ToString();
    }

    public string BuildUserIntro(string ticketTitle, IReadOnlyList<string> imageFileNames)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("Ticket title (context only; screenshots may not match):");
        sb.AppendLine(string.IsNullOrWhiteSpace(ticketTitle) ? "(none)" : ticketTitle.Trim());
        sb.AppendLine();
        sb.AppendLine($"You are given {imageFileNames.Count} image attachment(s), in order:");
        for (var i = 0; i < imageFileNames.Count; i++)
        {
            sb.AppendLine($"  {i + 1}. {imageFileNames[i]}");
        }
        sb.AppendLine();
        sb.AppendLine("Analyze the image(s) together and produce the JSON object now.");
        return sb.ToString();
    }
}
