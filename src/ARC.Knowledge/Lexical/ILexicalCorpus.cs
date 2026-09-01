using ARC.Knowledge.Vector;

namespace ARC.Knowledge.Lexical;

public interface ILexicalCorpus
{
    Task<IReadOnlyList<IndexedDocument>> GetActiveDocumentsAsync(CancellationToken cancellationToken);
}
