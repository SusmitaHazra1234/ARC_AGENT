using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Vector;

/// <summary>Meaning search (embeddings). Cosmos is the store; this layer does not generate embeddings.</summary>
public interface IDenseSearch
{
    Task<IReadOnlyList<EvidenceSource>> SearchAsync(
        string text,
        float[]? embedding,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        int topK,
        CancellationToken cancellationToken);
}
