using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ARC.Agents.Llm;

/// <summary>
/// Azure OpenAI / Foundry chat client. Callers depend on <see cref="IChatClient"/> only.
/// Amounts, dates, and eligibility still come from tools and domain rules — never from this client.
/// </summary>
public sealed class AzureOpenAILlmFactory : IChatClient
{
    private readonly IChatClient _inner;

    public AzureOpenAILlmFactory(IOptions<LlmOptions> options)
    {
        var llm = options.Value;
        if (string.IsNullOrWhiteSpace(llm.Endpoint) || string.IsNullOrWhiteSpace(llm.Deployment))
            throw new InvalidOperationException("Llm:Endpoint and Llm:Deployment are required when Llm:Provider is AzureOpenAI.");

        var azure = new AzureOpenAIClient(new Uri(llm.Endpoint.Trim()), new DefaultAzureCredential());
        _inner = azure.GetChatClient(llm.Deployment.Trim()).AsIChatClient();
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => _inner.GetService(serviceType, serviceKey) ?? (serviceType.IsInstanceOfType(this) ? this : null);

    public void Dispose() => _inner.Dispose();
}
