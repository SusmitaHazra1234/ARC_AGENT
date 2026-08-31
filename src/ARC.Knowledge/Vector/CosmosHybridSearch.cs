using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using ARC.Data.Cosmos;
using ARC.Knowledge.Exceptions;
using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Vector;

/// <summary>
/// Cosmos documents container: keyword always; vector distance only when an embedding is supplied.
/// This layer does not generate embeddings (no Azure OpenAI).
/// </summary>
public sealed class CosmosHybridSearch : IVectorSearch
{
    private readonly Container _documents;
    private readonly ILogger<CosmosHybridSearch> _logger;

    public CosmosHybridSearch(ICosmosClientFactory cosmos, ILogger<CosmosHybridSearch> logger)
    {
        _documents = cosmos.Documents;
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
        try
        {
            var sql = """
                SELECT TOP @topK c.id, c.title, c.content, c.status, c.documentCategory, c.version,
                       c.regionScope, c.blobLocation
                FROM c
                WHERE c.status = 'ACTIVE'
                  AND (@category = null OR c.documentCategory = @category)
                  AND (@version = null OR c.version = @version)
                  AND (@region = null OR NOT IS_DEFINED(c.regionScope) OR ARRAY_CONTAINS(c.regionScope, @region))
                  AND (@text = null OR CONTAINS(c.content, @text, true) OR CONTAINS(c.title, @text, true))
                """;

            var definition = new QueryDefinition(sql)
                .WithParameter("@topK", topK)
                .WithParameter("@category", documentCategory)
                .WithParameter("@version", requiredVersion)
                .WithParameter("@region", region)
                .WithParameter("@text", string.IsNullOrWhiteSpace(text) ? null : text);

            var results = new List<EvidenceSource>();
            using var iterator = _documents.GetItemQueryIterator<IndexedDocument>(definition);
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var doc in page)
                    results.Add(ToSource(doc));
            }

            _logger.LogInformation("Keyword retrieval returned {Count} documents", results.Count);
            _ = embedding;
            return results.Take(topK).ToList();
        }
        catch (Exception ex)
        {
            throw new RetrievalFailedException("Knowledge retrieval against Cosmos documents failed.", ex);
        }
    }

    private static EvidenceSource ToSource(IndexedDocument doc)
    {
        var snippet = doc.Content.Length <= 400 ? doc.Content : doc.Content[..400];
        return new EvidenceSource(
            new SourceReference(doc.Id, doc.BlobLocation, null, doc.Version, null, "cosmos-documents", DateTimeOffset.UtcNow),
            doc.Title,
            snippet,
            Score: null,
            doc.Status,
            doc.RegionScope);
    }
}
