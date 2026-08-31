using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Vector;

public sealed record IndexedDocument(
    string Id,
    string Title,
    string Content,
    string Status,
    string? DocumentCategory,
    string? Version,
    string? RegionScope,
    string? BlobLocation,
    float[]? Embedding);

public interface IVectorSearch
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
