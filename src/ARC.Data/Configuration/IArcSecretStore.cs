namespace ARC.Data.Configuration;

public interface IArcSecretStore
{
    Uri VaultUri { get; }

    Task<string?> GetAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);
}
