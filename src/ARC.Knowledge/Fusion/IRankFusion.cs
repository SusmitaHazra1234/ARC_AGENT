using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Fusion;

public interface IRankFusion
{
    IReadOnlyList<EvidenceSource> Fuse(IReadOnlyList<IReadOnlyList<EvidenceSource>> rankedLists);
}
