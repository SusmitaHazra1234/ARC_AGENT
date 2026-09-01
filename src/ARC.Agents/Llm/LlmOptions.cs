namespace ARC.Agents.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Shadow (default) or AzureOpenAI.</summary>
    public string Provider { get; set; } = "Shadow";

    public string Endpoint { get; set; } = "";

    public string Deployment { get; set; } = "";
}
