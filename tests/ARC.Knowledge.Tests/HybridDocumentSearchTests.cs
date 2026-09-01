using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Fusion;
using ARC.Knowledge.Lexical;
using ARC.Knowledge.Provenance;
using ARC.Knowledge.Retrieval;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.Tests;

public sealed class HybridDocumentSearchTests
{
    [Fact]
    public async Task Hybrid_rrf_keeps_lexical_name_hit()
    {
        var dense = new StubDense(
        [
            S("dense-1", "Retrieval overview", "Dense passage retrieval dual encoder", 0.9, "cosmos-dense"),
            S("dense-2", "ODOS process", "Monthly demand notice cycle", 0.8, "cosmos-dense")
        ]);
        var lexical = new StubLexical(
        [
            S("biblio", "Lewis RAG", "Thomas Wolf, Lysandre Debut, Hugging Face transformers", 9, "lucene-lexical"),
            S("other", "Ops", "unrelated operational resilience text", 1, "lucene-lexical")
        ]);

        var search = new HybridDocumentSearch(
            dense,
            lexical,
            new ReciprocalRankFusion(60),
            Options.Create(new ArcKnowledgeOptions { RetrievalTopK = 3, FusionCandidates = 3 }),
            NullLogger<HybridDocumentSearch>.Instance);

        var result = await search.SearchAsync(
            "What is a contribution of Thomas Wolf?",
            embedding: null,
            region: null,
            documentCategory: null,
            requiredVersion: null,
            topK: 3,
            CancellationToken.None);

        Assert.Equal("biblio", result[0].Reference.DocumentId);
        Assert.Contains(result, s => s.Reference.DocumentId == "dense-1");
    }

    [Fact]
    public void Distinctive_terms_keep_names()
    {
        var terms = QueryTermCoverage.DistinctiveQueryTerms("What is Thomas Wolf?");
        Assert.Contains("thomas", terms);
        Assert.Contains("wolf", terms);
        Assert.DoesNotContain("what", terms);
    }

    private static EvidenceSource S(string id, string title, string snippet, double score, string system) =>
        new(
            new SourceReference(id, null, null, "v1", null, system, DateTimeOffset.UtcNow),
            title,
            snippet,
            score,
            "ACTIVE",
            null);

    private sealed class StubDense(IReadOnlyList<EvidenceSource> hits) : IDenseSearch
    {
        public Task<IReadOnlyList<EvidenceSource>> SearchAsync(
            string text, float[]? embedding, string? region, string? documentCategory, string? requiredVersion, int topK, CancellationToken cancellationToken)
            => Task.FromResult(hits);
    }

    private sealed class StubLexical(IReadOnlyList<EvidenceSource> hits) : ILexicalSearch
    {
        public Task<IReadOnlyList<EvidenceSource>> SearchAsync(
            string text, string? region, string? documentCategory, string? requiredVersion, int topK, CancellationToken cancellationToken)
            => Task.FromResult(hits);
    }
}
