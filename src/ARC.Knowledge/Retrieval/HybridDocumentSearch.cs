using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Fusion;
using ARC.Knowledge.Lexical;
using ARC.Knowledge.Provenance;
using ARC.Knowledge.Vector;

namespace ARC.Knowledge.Retrieval;

/// <summary>
/// Athena-style hybrid search: Cosmos dense + Lucene BM25, fused with RRF.
/// Implements <see cref="IVectorSearch"/> so graph retrieval and tools stay unchanged.
/// </summary>
public sealed class HybridDocumentSearch : IVectorSearch
{
    private readonly IDenseSearch _dense;
    private readonly ILexicalSearch _lexical;
    private readonly IRankFusion _fusion;
    private readonly ArcKnowledgeOptions _options;
    private readonly ILogger<HybridDocumentSearch> _logger;

    public HybridDocumentSearch(
        IDenseSearch dense,
        ILexicalSearch lexical,
        IRankFusion fusion,
        IOptions<ArcKnowledgeOptions> options,
        ILogger<HybridDocumentSearch> logger)
    {
        _dense = dense;
        _lexical = lexical;
        _fusion = fusion;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EvidenceSource>> SearchAsync(
        string text,
        float[]? embedding,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken)
    {
        var denseK = Math.Max(topK, _options.DenseCandidates);
        var lexicalK = Math.Max(topK, _options.LexicalCandidates);

        var denseTask = _dense.SearchAsync(
            text, embedding, region, documentCategory, requiredVersion, denseK, cancellationToken);
        var lexicalTask = _lexical.SearchAsync(
            text, region, documentCategory, requiredVersion, lexicalK, cancellationToken);

        await Task.WhenAll(denseTask, lexicalTask);
        var dense = await denseTask;
        var lexical = await lexicalTask;

        var fused = _fusion
            .Fuse([dense, lexical])
            .Take(Math.Max(topK, _options.FusionCandidates))
            .ToArray();

        var result = QueryTermCoverage.EnsureDistinctiveCoverage(
            text, lexical, fused, Math.Max(1, topK));

        _logger.LogInformation(
            "Hybrid search dense {Dense} lexical {Lexical} fused {Fused}",
            dense.Count,
            lexical.Count,
            result.Count);

        return result;
    }
}
