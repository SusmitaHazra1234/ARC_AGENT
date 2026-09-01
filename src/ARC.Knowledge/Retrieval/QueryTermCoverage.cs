using System.Text.RegularExpressions;
using ARC.Knowledge.Provenance;

namespace ARC.Knowledge.Retrieval;

/// <summary>
/// Keeps distinctive lexical hits (names, policy ids) that dense rank often drops.
/// Same idea as Athena's bibliography-name coverage pin.
/// </summary>
public static partial class QueryTermCoverage
{
    [GeneratedRegex(@"\p{L}[\p{L}\p{N}_-]*|\p{N}+(?:\.\p{N}+)?", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\b[A-Z][A-Za-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex CapitalizedTermRegex();

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "what", "which", "where", "when", "whom", "whose", "this", "that",
        "with", "from", "about", "according", "does", "have", "been",
        "were", "will", "would", "could", "should", "into", "than",
        "then", "them", "they", "their", "there", "some", "such",
        "paper", "document", "question", "answer", "policy", "notice"
    };

    public static IReadOnlyList<string> DistinctiveQueryTerms(string query)
    {
        var capitalized = CapitalizedTermRegex()
            .Matches(query)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => !Stopwords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (capitalized.Length > 0)
            return capitalized;

        return TokenRegex()
            .Matches(query.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(token => token.Length >= 4 && !Stopwords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<EvidenceSource> EnsureDistinctiveCoverage(
        string query,
        IReadOnlyList<EvidenceSource> lexical,
        IReadOnlyList<EvidenceSource> selected,
        int topK)
    {
        var terms = DistinctiveQueryTerms(query);
        if (terms.Count == 0 || lexical.Count == 0)
            return selected;

        var nameHits = lexical
            .Where(source => ContainsAllTerms(Haystack(source), terms))
            .ToArray();
        if (nameHits.Length == 0 && terms.Count > 1)
        {
            nameHits = lexical
                .Where(source => terms.Count(term =>
                    Haystack(source).Contains(term, StringComparison.OrdinalIgnoreCase)) >= 1)
                .ToArray();
        }

        if (nameHits.Length == 0)
            return selected;

        var selectedIds = selected
            .Select(source => source.Reference.DocumentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = nameHits
            .Where(source => !selectedIds.Contains(source.Reference.DocumentId))
            .Take(3)
            .ToArray();

        if (missing.Length == 0)
            return selected;

        return missing.Concat(selected).Take(Math.Max(1, topK)).ToArray();
    }

    private static string Haystack(EvidenceSource source) => $"{source.Title} {source.Snippet}";

    private static bool ContainsAllTerms(string text, IReadOnlyList<string> terms) =>
        terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
