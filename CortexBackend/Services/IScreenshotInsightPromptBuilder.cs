namespace Cortex.API.Services;

public interface IScreenshotInsightPromptBuilder
{
    string BuildSystemPrompt();
    string BuildUserIntro(string ticketTitle, IReadOnlyList<string> imageFileNames);
}
