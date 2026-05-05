using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Tests;

public class SapTicketReferenceDetectorTests
{
    private static readonly SapTicketCatalogTable DemoTable = new(
        Id: 1,
        SourceId: 10,
        SourceName: "Demo",
        TableName: "MARC",
        Description: "Plant Data for Material",
        Module: "MM",
        BusinessObject: "Material Master",
        DataDomain: null,
        IsCustom: false);

    private static readonly SapTicketCatalogField YyngmField = new(
        Id: 100,
        TableMetadataId: 1,
        SourceId: 10,
        SourceName: "Demo",
        TableName: "MARC",
        TableDescription: "Plant Data for Material",
        Module: "MM",
        BusinessObject: "Material Master",
        DataDomain: null,
        TableIsCustom: false,
        FieldName: "YYNGM_ACTIVE",
        FieldDescription: "Custom active flag (example)",
        DomainName: null,
        FieldIsCustom: true);

    [Fact]
    public void TicketMentioningMarc_ReturnsTableMatch()
    {
        var text = "Please check MARC for plant 1000.";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [YyngmField]);
        Assert.Contains(matches, m =>
            m.MatchType == SapTicketReferenceMatchType.Table &&
            m.TableName == "MARC" &&
            m.Confidence == SapTicketReferenceMatchConfidence.High);
    }

    [Fact]
    public void TicketMentioningYyngm_ReturnsFieldMatch_Custom()
    {
        var text = "YYNGM_ACTIVE is missing on my material.";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [YyngmField]);
        var field = Assert.Single(matches.Where(m => m.MatchType == SapTicketReferenceMatchType.Field));
        Assert.Equal("YYNGM_ACTIVE", field.FieldName);
        Assert.True(field.IsCustom);
        Assert.True(field.LikelyCustomerExtensionField);
        Assert.Equal("MARC", field.TableName);
    }

    [Fact]
    public void TicketMentioningMarcYyngmExpression_MatchesTableAndField()
    {
        var text = "YYNGM_ACTIVE is missing for MARC on plant 1000.";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [YyngmField]);
        Assert.Contains(matches, m => m.MatchType == SapTicketReferenceMatchType.Table && m.TableName == "MARC");
        Assert.Contains(matches, m => m.MatchType == SapTicketReferenceMatchType.Field && m.FieldName == "YYNGM_ACTIVE");
    }

    [Fact]
    public void HyphenExpression_MarcDashYyngm_ReturnsFieldHigh()
    {
        var text = "Update MARC-YYNGM_ACTIVE flag.";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [YyngmField]);
        Assert.Contains(
            matches,
            m => m is
            {
                MatchType: SapTicketReferenceMatchType.Field,
                FieldName: "YYNGM_ACTIVE",
                TableName: "MARC",
            });
    }

    [Fact]
    public void GenericShortField_Status_DoesNotMatchWithoutTableContext()
    {
        var werks = new SapTicketCatalogField(
            Id: 2,
            TableMetadataId: 1,
            SourceId: 10,
            SourceName: "Demo",
            TableName: "MARC",
            TableDescription: null,
            Module: null,
            BusinessObject: null,
            DataDomain: null,
            TableIsCustom: false,
            FieldName: "STATUS",
            FieldDescription: "Status",
            DomainName: null,
            FieldIsCustom: false);
        var text = "Something about STATUS in the description.";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [werks]);
        Assert.DoesNotContain(matches, m => m.FieldName == "STATUS");
    }

    [Fact]
    public void ShortField_Werk_RequiresTableContext()
    {
        var werks = new SapTicketCatalogField(
            Id: 3,
            TableMetadataId: 1,
            SourceId: 10,
            SourceName: "Demo",
            TableName: "MARC",
            TableDescription: null,
            Module: null,
            BusinessObject: null,
            DataDomain: null,
            TableIsCustom: false,
            FieldName: "WER",
            FieldDescription: "Too short sample",
            DomainName: null,
            FieldIsCustom: false);
        var text = "Field WER needs review without table name.";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [werks]);
        Assert.DoesNotContain(matches, m => m.FieldName == "WER");
        var withTable = SapTicketReferenceDetector.DetectMatches(
            "WER on MARC",
            [DemoTable],
            [werks]);
        Assert.Contains(withTable, m => m.FieldName == "WER");
    }

    [Fact]
    public void DedupesSameTableFieldCombination()
    {
        var text = "MARC YYNGM_ACTIVE MARC-YYNGM_ACTIVE";
        var matches = SapTicketReferenceDetector.DetectMatches(
            text,
            [DemoTable],
            [YyngmField]);
        var fields = matches.Where(m => m is { MatchType: SapTicketReferenceMatchType.Field, FieldName: "YYNGM_ACTIVE" }).ToList();
        Assert.True(fields.Count <= 1);
    }

    [Fact]
    public void NoSapTerms_ReturnsEmpty()
    {
        var matches = SapTicketReferenceDetector.DetectMatches(
            "The printer is jammed again.",
            [DemoTable],
            [YyngmField]);
        Assert.Empty(matches);
    }

    [Fact]
    public void RespectsMaxMatchCap()
    {
        var tables = Enumerable.Range(0, 15).Select(i => new SapTicketCatalogTable(
                i + 1,
                10,
                "Demo",
                $"T{i}XX",
                null,
                null,
                null,
                null,
                false))
            .ToList();
        var text = string.Join(" ", tables.Select(t => t.TableName));
        var matches = SapTicketReferenceDetector.DetectMatches(text, tables, []);
        Assert.True(matches.Count <= SapTicketReferenceDetector.MaxMatches);
    }

    [Fact]
    public void EmptyCatalog_VagueSap_SapIntentOnly_NoMatches()
    {
        var text = "Need SAP field fixed\nThere is a problem with a field in SAP.";
        var dto = SapTicketReferenceDetector.DetectForTicket("T1", text, [], []);
        Assert.Empty(dto.Matches);
        Assert.True(dto.SapIntentOnly);
    }

    [Fact]
    public void EmptyCatalog_NonSap_NoIntent()
    {
        var dto = SapTicketReferenceDetector.DetectForTicket(
            "T1",
            "Fix field on intake form",
            [],
            []);
        Assert.Empty(dto.Matches);
        Assert.False(dto.SapIntentOnly);
    }

    [Fact]
    public void CatalogMatch_DisablesSapIntentOnlyFlag()
    {
        var dto = SapTicketReferenceDetector.DetectForTicket(
            "T1",
            "Please check MARC for plant 1000.",
            [DemoTable],
            [YyngmField]);
        Assert.Contains(dto.Matches, m => m.TableName == "MARC");
        Assert.False(dto.SapIntentOnly);
    }

    [Fact]
    public void Dto_HasNoSecretLikeFields()
    {
        var dto = SapTicketReferenceDetector.DetectForTicket(
            "T1",
            "MARC",
            [DemoTable],
            [YyngmField]);
        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tableId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fieldId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceId", json, StringComparison.OrdinalIgnoreCase);
    }
}
