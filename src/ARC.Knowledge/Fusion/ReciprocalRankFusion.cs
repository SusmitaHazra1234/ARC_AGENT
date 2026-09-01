using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Fusion;

/// <summary>
/// Reciprocal Rank Fusion (Cormack et al.). Score = Σ 1/(k + rank). Deterministic on ties.
/// </summary>
public sealed class ReciprocalRankFusion(int k = 60) : IRankFusion
{
    public IReadOnlyList<EvidenceSource> Fuse(IReadOnlyList<IReadOnlyList<EvidenceSource>> rankedLists)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var records = new Dictionary<string, EvidenceSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var list in rankedLists)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var id = item.Reference.DocumentId;
                var rank = i + 1;
                scores[id] = scores.GetValueOrDefault(id) + (1.0 / (k + rank));
                records[id] = item;
            }
        }

        return scores
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => records[x.Key] with { Score = x.Value })
            .ToArray();
    }
}
