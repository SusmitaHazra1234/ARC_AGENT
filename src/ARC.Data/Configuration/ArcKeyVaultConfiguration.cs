using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace ARC.Data.Configuration;

public static class ArcKeyVaultConfiguration
{
    public const string VaultUriKey = "KeyVault:VaultUri";

    public static Uri? TryGetVaultUri(IConfiguration configuration)
    {
        var value = configuration[VaultUriKey];
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return new Uri(value.Trim());
    }

    /// <summary>
    /// Reads every enabled secret from the vault at <c>KeyVault:VaultUri</c> into configuration.
    /// Skips silently when the vault is unreachable (typical local dev without Azure sign-in).
    /// </summary>
    public static IConfigurationBuilder AddArcKeyVault(this IConfigurationBuilder builder)
    {
        var uri = TryGetVaultUri(builder.Build());
        if (uri is null)
            return builder;

        try
        {
            var client = new SecretClient(uri, new DefaultAzureCredential());
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in client.GetPropertiesOfSecrets())
            {
                if (property.Enabled == false)
                    continue;

                var secret = client.GetSecret(property.Name);
                var name = property.Name;
                var value = secret.Value.Value;
                values[name] = value;
                values[name.Replace("--", ConfigurationPath.KeyDelimiter)] = value;
            }

            return builder.AddInMemoryCollection(values);
        }
        catch (Exception ex) when (ex is CredentialUnavailableException or RequestFailedException or AuthenticationFailedException)
        {
            return builder;
        }
    }
}
