using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Tests;

public class CatalogSearchRankingTests
{
    private static SynitiKnowledgeCatalogEntryDto Syniti(
        string term,
        string? relatedTerms = null,
        string? shortDefinition = "Definition",
        string? aliases = null,
        string? examplePhrases = null,
        string? businessMeaning = null) =>
        new(
            Term: term,
            Category: "Platform",
            Aliases: aliases,
            ExamplePhrases: examplePhrases,
            ShortDefinition: shortDefinition ?? "Definition",
            BusinessMeaning: businessMeaning,
            TechnicalMeaning: null,
            SuggestedReviewerChecks: [],
            MissingContextQuestions: [],
            RelatedTerms: relatedTerms,
            SourceIsEnabled: true,
            SourceName: "Test",
            SourceType: "Manual",
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

    private static SapReferenceCatalogEntryDto SapField(
        string table,
        string field,
        string? tableDesc = null,
        string? fieldDesc = null,
        bool likelyCustom = false) =>
        new(
            RowKind: "Field",
            TableName: table,
            FieldName: field,
            TableDescription: tableDesc,
            FieldDescription: fieldDesc,
            BusinessObject: null,
            Module: null,
            Domain: null,
            IsKey: false,
            IsRequired: false,
            IsCustomField: false,
            LikelyCustomSapField: likelyCustom,
            SourceName: "Src",
            SourceType: "Manual entry",
            SourceIsEnabled: true,
            FieldCount: 0,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

    private static SapReferenceCatalogEntryDto SapTable(
        string table,
        string? tableDesc = null,
        int fieldCount = 0) =>
        new(
            RowKind: "Table",
            TableName: table,
            FieldName: null,
            TableDescription: tableDesc,
            FieldDescription: null,
            BusinessObject: null,
            Module: null,
            Domain: null,
            IsKey: null,
            IsRequired: null,
            IsCustomField: false,
            LikelyCustomSapField: false,
            SourceName: "Src",
            SourceType: "Manual entry",
            SourceIsEnabled: true,
            FieldCount: fieldCount,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

    [Fact]
    public void Syniti_Reconciliation_ranks_above_Load_error_when_related_mentions_Reconciliation()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("Reconciliation");
        var recon = Syniti("Reconciliation", relatedTerms: "Something else");
        var load = Syniti("Load error", shortDefinition: "Ops", relatedTerms: "Reconciliation");
        Assert.True(CatalogSearchRanking.GetSynitiSortKey(recon, q) < CatalogSearchRanking.GetSynitiSortKey(load, q));
    }

    [Fact]
    public void Syniti_Cutover_ranks_above_Business_validation_when_only_related_matches()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("cutover");
        var cut = Syniti("Cutover", shortDefinition: "Go-live slice");
        var biz = Syniti("Business validation", shortDefinition: "Discuss cutover readiness");
        Assert.True(CatalogSearchRanking.GetSynitiSortKey(cut, q) < CatalogSearchRanking.GetSynitiSortKey(biz, q));
    }

    [Fact]
    public void Syniti_Field_ownership_ranks_above_related_concept()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("field ownership");
        var fo = Syniti("Field ownership", shortDefinition: "Who owns the field");
        var ds = Syniti(
            "Data steward review",
            shortDefinition: "Review ownership changes",
            businessMeaning: "May relate to field ownership decisions");
        Assert.True(CatalogSearchRanking.GetSynitiSortKey(fo, q) < CatalogSearchRanking.GetSynitiSortKey(ds, q));
    }

    [Fact]
    public void Syniti_Mock_load_ranks_above_Load_error_on_shared_substring()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("mock load");
        var mock = Syniti("Mock load", shortDefinition: "Practice load");
        var load = Syniti("Load error", shortDefinition: "Failure related to mock load runs");
        Assert.True(CatalogSearchRanking.GetSynitiSortKey(mock, q) < CatalogSearchRanking.GetSynitiSortKey(load, q));
    }

    [Fact]
    public void Sap_Marc_table_before_field_rows_and_weak_description_match()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("MARC");
        var table = SapTable("MARC", "Plant data", fieldCount: 2);
        var matnr = SapField("MARC", "MATNR", "Plant data", "Material");
        var weak = SapTable("ZTEMP", "Copy of MARC-like staging", fieldCount: 0);
        Assert.True(CatalogSearchRanking.GetSapSortKey(table, q) < CatalogSearchRanking.GetSapSortKey(matnr, q));
        Assert.True(CatalogSearchRanking.GetSapSortKey(matnr, q) < CatalogSearchRanking.GetSapSortKey(weak, q));
    }

    [Fact]
    public void Sap_Matnr_field_ranks_before_description_only()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("MATNR");
        var marcMatnr = SapField("MARC", "MATNR", "Plant data", "Material number");
        var descOnly = SapField("T001", "KUNNR", "Header", "See MATNR reference doc");
        Assert.True(CatalogSearchRanking.GetSapSortKey(marcMatnr, q) < CatalogSearchRanking.GetSapSortKey(descOnly, q));
    }

    [Fact]
    public void Sap_Yyngm_active_field_strong_match_and_custom_flag_unchanged()
    {
        var q = CatalogSearchRanking.NormalizeSearchText("YYNGM_ACTIVE");
        var row = SapField("MARC", "YYNGM_ACTIVE", "Plant", "Flag", likelyCustom: true);
        Assert.True(CatalogSearchRanking.GetSapSortKey(row, q) <= 120);
        Assert.True(row.LikelyCustomSapField);
    }
}
