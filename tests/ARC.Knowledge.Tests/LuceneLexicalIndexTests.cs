using ARC.Knowledge.Lexical;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.Tests;

public sealed class LuceneLexicalIndexTests
{
    [Fact]
    public async Task Search_finds_exact_policy_words()
    {
        var dir = Path.Combine(Path.GetTempPath(), "arc-lucene-tests", Guid.NewGuid().ToString("N"));
        using var index = new LuceneLexicalIndex(dir);
        await index.RebuildAsync(
            [
                new IndexedDocument(
                    "odos-policy",
                    "ODOS Demand Notice Policy",
                    "Section 138 NI Act notice must follow the bounced cheque window.",
                    "ACTIVE",
                    "policy",
                    "2026.03.1",
                    "west",
                    "blob://odos",
                    null),
                new IndexedDocument(
                    "credit-memo",
                    "Unapplied credit process",
                    "Reconcile unapplied credits before issuing a demand notice.",
                    "ACTIVE",
                    "sop",
                    "2026.03.1",
                    "west",
                    "blob://credit",
                    null)
            ],
            CancellationToken.None);

        var hits = await index.SearchAsync("Section 138 NI Act", topK: 5, region: null, documentCategory: null, requiredVersion: null, CancellationToken.None);

        Assert.NotEmpty(hits);
        Assert.Equal("odos-policy", hits[0].Reference.DocumentId);
        Assert.Equal("lucene-lexical", hits[0].Reference.SourceSystem);
    }

    [Fact]
    public async Task Search_respects_document_category_filter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "arc-lucene-tests", Guid.NewGuid().ToString("N"));
        using var index = new LuceneLexicalIndex(dir);
        await index.RebuildAsync(
            [
                new IndexedDocument("a", "Notice", "demand notice wording", "ACTIVE", "policy", "1", null, null, null),
                new IndexedDocument("b", "Notice", "demand notice wording", "ACTIVE", "sop", "1", null, null, null)
            ],
            CancellationToken.None);

        var hits = await index.SearchAsync("demand notice", 5, null, "sop", null, CancellationToken.None);

        Assert.Single(hits);
        Assert.Equal("b", hits[0].Reference.DocumentId);
    }
}
