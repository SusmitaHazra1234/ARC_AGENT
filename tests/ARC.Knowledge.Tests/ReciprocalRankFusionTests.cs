using ARC.Knowledge.Fusion;
using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Tests;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Fuse_promotes_item_present_in_both_lists()
    {
        var dense = new[] { S("A", 0.99), S("B", 0.90), S("C", 0.80) };
        var lexical = new[] { S("B", 12), S("D", 10), S("A", 8) };

        var result = new ReciprocalRankFusion(60).Fuse([dense, lexical]);

        Assert.Equal("B", result[0].Reference.DocumentId);
        Assert.Equal("A", result[1].Reference.DocumentId);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Fuse_is_deterministic_on_ties()
    {
        var result = new ReciprocalRankFusion(60)
            .Fuse([[S("B", 1)], [S("A", 1)]]);

        Assert.Equal(new[] { "A", "B" }, result.Select(x => x.Reference.DocumentId));
    }

    private static EvidenceSource S(string id, double score) =>
        new(
            new SourceReference(id, null, null, "v1", null, "test", DateTimeOffset.UtcNow),
            id,
            "snippet",
            score,
            "ACTIVE",
            null);
}
