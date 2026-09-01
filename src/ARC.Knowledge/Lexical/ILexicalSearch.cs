using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Lexical;

/// <summary>Word / BM25 search over the Lucene document index.</summary>
public interface ILexicalSearch
{
    Task<IReadOnlyList<EvidenceSource>> SearchAsync(
        string text,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken);
}
