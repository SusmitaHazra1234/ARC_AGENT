using System.ComponentModel;
using ARC.Tools.Knowledge;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class KnowledgeMcpTools
{
    private readonly KnowledgeRetrievalTool _tool;

    public KnowledgeMcpTools(KnowledgeRetrievalTool tool) => _tool = tool;

    [McpServerTool, Description("Search governed knowledge documents with citations.")]
    public async Task<string> SearchDocuments(
        [Description("Search text")] string text,
        [Description("Optional dealer URN")] string? dealerUrn = null,
        [Description("Optional region")] string? region = null,
        [Description("Optional document category")] string? documentCategory = null,
        [Description("Optional required version")] string? requiredVersion = null,
        [Description("Optional correlation id")] string? correlationId = null,
        [Description("Maximum results (1-8)")] int topK = 8,
        CancellationToken cancellationToken = default)
    {
        var result = await _tool.SearchDocumentsAsync(
            new SearchDocumentsRequest(text, dealerUrn, region, documentCategory, requiredVersion, correlationId, topK),
            cancellationToken);

        return McpJson.Serialize(result);
    }

    [McpServerTool, Description("Traverse the dealer knowledge graph.")]
    public async Task<string> TraverseGraph(
        [Description("Dealer URN")] string dealerUrn,
        [Description("Optional correlation id")] string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tool.TraverseGraphAsync(
            new TraverseGraphRequest(dealerUrn, correlationId),
            cancellationToken);

        return McpJson.Serialize(result);
    }
}
