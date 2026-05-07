using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Tests;

public class SynitiKnowledgeDetectorTests
{
    private static SynitiKnowledgeCatalogRow R(
        int id,
        string term,
        SynitiKnowledgeCategory cat,
        string? examples = null,
        string? aliases = null) =>
        new(id, "Seed", term, cat, "Def", null, null, null, examples, aliases, null, null);

    [Fact]
    public void FindMatches_DspToken_MatchesDsp()
    {
        var hay = "please review our dsp setup for cutover";
        var catalog = new[] { R(1, "DSP", SynitiKnowledgeCategory.Platform) };
        var hits = SynitiKnowledgeDetector.FindMatches(hay, catalog);
        Assert.Single(hits);
        Assert.Equal("DSP", hits[0].Row.Term);
        Assert.Equal(SynitiKnowledgeMatchStrength.Strong, hits[0].Strength);
    }

    [Fact]
    public void FindMatches_ValueMappingPhrase_Matches()
    {
        var hay = "we need help with value mapping for the plant codes";
        var catalog = new[]
        {
            R(1, "Value Mapping", SynitiKnowledgeCategory.Mapping),
        };
        var hits = SynitiKnowledgeDetector.FindMatches(hay, catalog);
        Assert.Single(hits);
        Assert.Equal("Value Mapping", hits[0].Row.Term);
    }

    [Fact]
    public void FindMatches_ExamplePhrase_MatchesModerateStrength()
    {
        var hay = "failing dq rules reported by the team";
        var catalog = new[]
        {
            R(1, "Data Quality Rule", SynitiKnowledgeCategory.DataQuality, examples: "dq rules"),
        };
        var hits = SynitiKnowledgeDetector.FindMatches(hay, catalog);
        Assert.Single(hits);
        Assert.True(hits[0].MatchedViaExamplePhrase);
        Assert.Equal(SynitiKnowledgeMatchStrength.Moderate, hits[0].Strength);
    }

    [Fact]
    public void FindMatches_AliasMatchesAdmmToAdm()
    {
        var hay = "issue in ADMM for vendor master";
        var catalog = new[]
        {
            R(1, "ADM", SynitiKnowledgeCategory.Platform, aliases: "ADMM"),
        };
        var hits = SynitiKnowledgeDetector.FindMatches(hay, catalog);
        Assert.Single(hits);
        Assert.Equal("ADM", hits[0].Row.Term);
        Assert.Equal(SynitiKnowledgeMatchStrength.Strong, hits[0].Strength);
    }

    [Fact]
    public void FindMatches_RespectsMaxMatches()
    {
        var hay =
            "dsp adm value mapping data quality rule wave and syniti glossary overflow";
        var catalog = new[]
        {
            R(1, "DSP", SynitiKnowledgeCategory.Platform),
            R(2, "ADM", SynitiKnowledgeCategory.Platform),
            R(3, "Value Mapping", SynitiKnowledgeCategory.Mapping),
            R(4, "Data Quality Rule", SynitiKnowledgeCategory.DataQuality, "quality rule"),
            R(5, "Wave", SynitiKnowledgeCategory.Migration),
            R(6, "Syniti", SynitiKnowledgeCategory.Platform),
        };
        var hits = SynitiKnowledgeDetector.FindMatches(hay, catalog);
        Assert.Equal(3, hits.Count);
    }
}
