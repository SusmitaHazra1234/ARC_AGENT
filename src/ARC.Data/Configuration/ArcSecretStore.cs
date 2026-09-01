using Azure.Security.KeyVault.Secrets;

namespace ARC.Data.Configuration;

public sealed class ArcSecretStore : IArcSecretStore
{
    private readonly SecretClient _client;

    public ArcSecretStore(SecretClient client)
    {
        _client = client;
    }

    public Uri VaultUri => _client.VaultUri;

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetSecretAsync(name, cancellationToken: cancellationToken);
        return response.Value.Value;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var property in _client.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            if (property.Enabled == false)
                continue;

            var secret = await _client.GetSecretAsync(property.Name, cancellationToken: cancellationToken);
            secrets[property.Name] = secret.Value.Value;
        }

        return secrets;
    }
}
