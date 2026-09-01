using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;
using ARC.Knowledge.Configuration;
using ARC.Knowledge.Provenance;
using ARC.Knowledge.Vector;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace ARC.Knowledge.Lexical;

/// <summary>On-disk Lucene BM25 index for lexical (word) search of policy/document text.</summary>
public sealed class LuceneLexicalIndex : IDisposable
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
    private const float Bm25K1 = 1.5f;
    private const float Bm25B = 0.75f;

    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LuceneDirectory? _indexDirectory;
    private DirectoryReader? _reader;
    private IndexSearcher? _searcher;

    public LuceneLexicalIndex(IOptions<ArcKnowledgeOptions> options)
        : this(options.Value.LexicalIndexDirectory)
    {
    }

    public LuceneLexicalIndex(string directory)
    {
        _directory = Path.GetFullPath(string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Path.GetTempPath(), "arc-lucene")
            : directory);
    }

    public int DocumentCount => _searcher?.IndexReader.NumDocs ?? 0;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            System.IO.Directory.CreateDirectory(_directory);
            CloseReader();

            _indexDirectory = FSDirectory.Open(new DirectoryInfo(_directory));
            if (!DirectoryReader.IndexExists(_indexDirectory))
                return;

            _reader = DirectoryReader.Open(_indexDirectory);
            _searcher = CreateSearcher(_reader);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnsureReadyAsync(ILexicalCorpus corpus, CancellationToken cancellationToken)
    {
        await OpenAsync(cancellationToken);
        if (DocumentCount > 0)
            return;

        var source = await corpus.GetActiveDocumentsAsync(cancellationToken);
        if (source.Count == 0)
            return;

        await RebuildAsync(source, cancellationToken);
    }

    public async Task RebuildAsync(IEnumerable<IndexedDocument> documents, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            System.IO.Directory.CreateDirectory(_directory);
            CloseReader();
            _indexDirectory?.Dispose();
            _indexDirectory = FSDirectory.Open(new DirectoryInfo(_directory));

            using var analyzer = CreateAnalyzer();
            var config = new IndexWriterConfig(AppLuceneVersion, analyzer)
            {
                OpenMode = OpenMode.CREATE,
                Similarity = new BM25Similarity(Bm25K1, Bm25B)
            };

            using var writer = new IndexWriter(_indexDirectory, config);
            foreach (var document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.AddDocument(ToLuceneDocument(document));
            }

            writer.Commit();
            _reader = DirectoryReader.Open(_indexDirectory);
            _searcher = CreateSearcher(_reader);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IReadOnlyList<EvidenceSource>> SearchAsync(
        string query,
        int topK,
        string? region,
        string? documentCategory,
        string? requiredVersion,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_searcher is null || string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<EvidenceSource>>([]);

        using var analyzer = CreateAnalyzer();
        var escaped = QueryParserBase.Escape(query.Trim());
        if (string.IsNullOrWhiteSpace(escaped))
            return Task.FromResult<IReadOnlyList<EvidenceSource>>([]);

        var parser = new QueryParser(AppLuceneVersion, "text", analyzer)
        {
            DefaultOperator = Operator.OR
        };

        Query parsed;
        try
        {
            parsed = parser.Parse(escaped);
        }
        catch (ParseException)
        {
            return Task.FromResult<IReadOnlyList<EvidenceSource>>([]);
        }

        var boolean = new BooleanQuery { { parsed, Occur.MUST } };
        if (!string.IsNullOrWhiteSpace(documentCategory))
            boolean.Add(new TermQuery(new Term("documentCategory", documentCategory)), Occur.MUST);
        if (!string.IsNullOrWhiteSpace(requiredVersion))
            boolean.Add(new TermQuery(new Term("version", requiredVersion)), Occur.MUST);

        var hits = _searcher.Search(boolean, Math.Max(1, topK * 4));
        var results = new List<EvidenceSource>(hits.ScoreDocs.Length);
        foreach (var hit in hits.ScoreDocs)
        {
            var document = _searcher.Doc(hit.Doc);
            var regionScope = document.Get("regionScope");
            if (!string.IsNullOrWhiteSpace(region)
                && !string.IsNullOrWhiteSpace(regionScope)
                && regionScope.IndexOf(region, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var indexed = new IndexedDocument(
                document.Get("id") ?? string.Empty,
                document.Get("title") ?? string.Empty,
                document.Get("text") ?? string.Empty,
                document.Get("status") ?? string.Empty,
                document.Get("documentCategory"),
                document.Get("version"),
                regionScope,
                document.Get("blobLocation"),
                null);
            results.Add(EvidenceMapping.ToSource(indexed, hit.Score, "lucene-lexical"));
            if (results.Count >= Math.Max(1, topK))
                break;
        }

        return Task.FromResult<IReadOnlyList<EvidenceSource>>(results);
    }

    public void Dispose()
    {
        CloseReader();
        _indexDirectory?.Dispose();
        _gate.Dispose();
    }

    private static IndexSearcher CreateSearcher(DirectoryReader reader) =>
        new(reader) { Similarity = new BM25Similarity(Bm25K1, Bm25B) };

    private static StandardAnalyzer CreateAnalyzer() => new(AppLuceneVersion);

    private static Document ToLuceneDocument(IndexedDocument document)
    {
        var text = $"{document.Title} {document.Content}";
        return new Document
        {
            new StringField("id", document.Id ?? string.Empty, Field.Store.YES),
            new TextField("text", text, Field.Store.YES),
            new StringField("title", document.Title ?? string.Empty, Field.Store.YES),
            new StringField("status", document.Status ?? string.Empty, Field.Store.YES),
            new StringField("documentCategory", document.DocumentCategory ?? string.Empty, Field.Store.YES),
            new StringField("version", document.Version ?? string.Empty, Field.Store.YES),
            new StringField("regionScope", document.RegionScope ?? string.Empty, Field.Store.YES),
            new StringField("blobLocation", document.BlobLocation ?? string.Empty, Field.Store.YES)
        };
    }

    private void CloseReader()
    {
        _searcher = null;
        _reader?.Dispose();
        _reader = null;
    }
}
