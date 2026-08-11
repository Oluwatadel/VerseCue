
using Versecue.Domain.Entities;

namespace Versecue.Application.Interfaces;

public interface IBibleRepository
{
    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        string translationCode,
        string bookName,
        int chapterNumber,
        int verseStart,
        int? verseEnd = null,
        CancellationToken cancellationToken = default);
}