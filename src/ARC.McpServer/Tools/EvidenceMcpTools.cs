using System.ComponentModel;
using ARC.Domain.Enums;
using ARC.Tools.Evidence;
using ModelContextProtocol.Server;

namespace ARC.McpServer.Tools;

[McpServerToolType]
public sealed class EvidenceMcpTools
{
    private readonly EvidenceCaseFileTool _tool;

    public EvidenceMcpTools(EvidenceCaseFileTool tool) => _tool = tool;

    [McpServerTool, Description("Prepare a Section 138 evidence case file and completeness score.")]
    public async Task<string> PrepareCaseFile(
        [Description("Dealer URN")] string dealerUrn,
        [Description("JSON array of evidence items: [{\"type\":\"LedgerExtract\",\"location\":\"legal-worm/...\"}]")] string documentsJson,
        [Description("Cycle id")] string cycleId,
        [Description("Optional correlation id")] string? correlationId = null,
        [Description("Optional case reference")] string? caseReference = null,
        CancellationToken cancellationToken = default)
    {
        var documents = McpJson.Deserialize<List<EvidenceDocumentDto>>(documentsJson)
            .Select(d => new EvidenceItem(Enum.Parse<DocumentType>(d.Type, ignoreCase: true), d.Location))
            .ToList();

        var result = await _tool.PrepareAsync(
            new PrepareCaseFileRequest(dealerUrn, documents, cycleId, correlationId, caseReference),
            cancellationToken);

        return McpJson.Serialize(result);
    }

    private sealed record EvidenceDocumentDto(string Type, string Location);
}
