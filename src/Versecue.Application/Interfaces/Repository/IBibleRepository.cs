using Versecue.Application.Bible;
using BibleVerse = Versecue.Application.Bible.BibleVerse;

namespace Versecue.Application.Interfaces;

public interface IBibleRepository
{
    Task<BibleVerse?> GetVerseAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default);
}