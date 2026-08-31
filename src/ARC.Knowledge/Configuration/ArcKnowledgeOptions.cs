namespace ARC.Knowledge.Configuration;

public sealed class ArcKnowledgeOptions
{
    public const string SectionName = "ArcKnowledge";

    /// <summary>Document Intelligence endpoint. Key is never stored here — use managed identity.</summary>
    public string DocumentIntelligenceEndpoint { get; set; } = "";

    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>Model ids are environment-specific. Defaults are extraction models, not legal rules.</summary>
    public string ChequeModelId { get; set; } = "prebuilt-check.us";

    public string LayoutModelId { get; set; } = "prebuilt-layout";

    public int RetrievalTopK { get; set; } = 8;

    /// <summary>Source auto-accept thresholds for extraction quality only — not eligibility.</summary>
    public decimal ChequeNumberConfidence { get; set; } = 0.90m;
    public decimal MicrConfidence { get; set; } = 0.90m;
    public decimal AmountConfidence { get; set; } = 0.85m;
}
