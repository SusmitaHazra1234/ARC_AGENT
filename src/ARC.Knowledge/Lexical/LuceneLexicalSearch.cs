using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Lexical;

public sealed class LuceneLexicalSearch(LuceneLexicalIndex index, ILexicalCorpus corpus) : ILexicalSearch
{
    public async Task<IReadOnlyList<EvidenceSource>> SearchAsync(
        string text,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken)
    {
        await index.EnsureReadyAsync(corpus, cancellationToken);
        return await index.SearchAsync(text, topK, region, documentCategory, requiredVersion, cancellationToken);
    }
}
