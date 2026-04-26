using Cortex.API.Services;

namespace Cortex.API.Tests;

public class TicketQualityClassifierTests
{
    // --- Ugly ---

    [Theory]
    [InlineData("SAP broken", "not working cant post fix asap")]
    [InlineData("", "help")]
    [InlineData("issue", "broken")]
    [InlineData("fix this", "asap")]
    public void Classify_UglyInput_ReturnsUgly(string title, string description)
    {
        Assert.Equal(TicketQuality.Ugly, TicketQualityClassifier.Classify(title, description));
    }

    // --- Good ---

    [Theory]
    [InlineData(
        "Vendor upload validation fails due to blank vendor IDs",
        "Uploading vendor data via template fails validation due to blank vendor ID fields. Error occurs during batch upload in finance workflow.")]
    [InlineData(
        "SAP batch job failure during nightly process",
        "The nightly batch job fails during the upload process. Error message is not captured. The job runs in the production environment and affects the finance workflow.")]
    public void Classify_GoodInput_ReturnsGood(string title, string description)
    {
        Assert.Equal(TicketQuality.Good, TicketQualityClassifier.Classify(title, description));
    }

    // --- Bad ---

    [Theory]
    [InlineData("Vendor upload issue", "Some vendor IDs are failing when uploading file")]
    [InlineData("SAP not working", "The SAP system cannot complete the posting operation")]
    [InlineData("Upload fails", "File upload is failing for some records in the vendor module")]
    public void Classify_BadInput_ReturnsBad(string title, string description)
    {
        Assert.Equal(TicketQuality.Bad, TicketQualityClassifier.Classify(title, description));
    }

    [Fact]
    public void Classify_NullInputs_ReturnsUgly()
    {
        Assert.Equal(TicketQuality.Ugly, TicketQualityClassifier.Classify(null, null));
    }

    [Fact]
    public void Classify_EmptyInputs_ReturnsUgly()
    {
        Assert.Equal(TicketQuality.Ugly, TicketQualityClassifier.Classify("", ""));
    }

    [Fact]
    public void Classify_GoodDescriptionAloneWithShortTitle_ReturnsGood()
    {
        const string title = "Vendor upload issue";
        const string description =
            "Upload fails when the file contains blank vendor ID fields. " +
            "Validation error is thrown during the batch process. " +
            "The template uses the standard finance module. " +
            "Approximately 150 records failed on the last run.";

        Assert.Equal(TicketQuality.Good, TicketQualityClassifier.Classify(title, description));
    }
}
