namespace ARC.Knowledge.Retrieval;

public sealed record RetrievalQuery(
    string Text,
    string? DealerUrn,
    string? Region,
    string? DocumentCategory,
    string? RequiredVersion,
    string? CorrelationId,
    float[]? Embedding = null,
    int TopK = 8);
