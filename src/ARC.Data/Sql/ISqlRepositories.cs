using ARC.Domain.Entities;
using ARC.Domain.ValueObjects;

namespace ARC.Data.Sql;

public interface IDealerRepository
{
    Task<Dealer?> GetAsync(DealerUrn urn, CancellationToken cancellationToken);
    Task<IReadOnlyList<Dealer>> ListByRegionAsync(string region, CancellationToken cancellationToken);
    /// <summary>Privileged cycle fan-out. TSI region isolation belongs on ARC.Api, not this job.</summary>
    Task<IReadOnlyList<Dealer>> ListAllAsync(CancellationToken cancellationToken);
}

public interface ILedgerRepository
{
    Task<IReadOnlyList<LedgerPosition>> ListByDealerAsync(DealerUrn urn, CancellationToken cancellationToken);
}

public interface IChequeRepository
{
    Task<IReadOnlyList<SecurityCheque>> ListChequesAsync(DealerUrn urn, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChequeReturnMemo>> ListReturnMemosAsync(DealerUrn urn, CancellationToken cancellationToken);
}

public interface IGateDecisionRepository
{
    Task SaveAsync(CycleId cycleId, DealerUrn dealerUrn, GateDecision decision, CancellationToken cancellationToken);
    Task<IReadOnlyList<GateDecision>> ListAsync(CycleId cycleId, DealerUrn dealerUrn, CancellationToken cancellationToken);
}

public interface ILegalCaseRepository
{
    Task<LegalCase?> GetAsync(DealerUrn urn, CancellationToken cancellationToken);
    Task UpsertAsync(LegalCase legalCase, CancellationToken cancellationToken);
}

/// <summary>SQL index of a recovery run. Full RecoveryState lives in Cosmos.</summary>
public interface IRecoveryCaseRepository
{
    Task UpsertIndexAsync(RecoveryCaseIndex index, CancellationToken cancellationToken);
    Task<RecoveryCaseIndex?> GetAsync(CycleId cycleId, DealerUrn dealerUrn, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecoveryCaseIndex>> ListByCycleAsync(CycleId cycleId, string? region, string? depot, CancellationToken cancellationToken);
}

public sealed record RecoveryCaseIndex(
    CycleId CycleId,
    DealerUrn DealerUrn,
    string Status,
    string CorrelationId,
    string? WaitingGate,
    DateTimeOffset UpdatedUtc);
