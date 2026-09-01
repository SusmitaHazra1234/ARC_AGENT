using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Vector;

internal static class EvidenceMapping
{
    public static EvidenceSource ToSource(IndexedDocument doc, double? score, string sourceSystem)
    {
        var content = doc.Content ?? string.Empty;
        var snippet = content.Length <= 400 ? content : content[..400];
        return new EvidenceSource(
            new SourceReference(
                doc.Id,
                doc.BlobLocation,
                null,
                doc.Version,
                null,
                sourceSystem,
                DateTimeOffset.UtcNow),
            doc.Title ?? string.Empty,
            snippet,
            score,
            doc.Status ?? string.Empty,
            doc.RegionScope);
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        return denom == 0 ? 0 : dot / denom;
    }
}
