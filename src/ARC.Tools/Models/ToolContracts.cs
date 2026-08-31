namespace ARC.Tools.Models;

public sealed class ArcToolsOptions
{
    public const string SectionName = "ArcTools";

    /// <summary>Process B trigger from source: demand notice + no payment in 60 days.</summary>
    public int Section138NonPaymentDays { get; set; } = 60;

    /// <summary>A2 visit-tier cutoff. Not in source — leave null so Visit is never auto-assigned.</summary>
    public decimal? VisitMaxNetExposure { get; set; }

    /// <summary>ASR below this requires TSI confirmation. Hard floor value is To Be Confirmed.</summary>
    public decimal? VoicePtpConfirmBelow { get; set; }

    /// <summary>ASR below this discards the capture. Hard floor value is To Be Confirmed.</summary>
    public decimal? VoicePtpDiscardBelow { get; set; }
}

public sealed record ToolCallContext(string? CycleId, string? CorrelationId, DateOnly AsOf);
