
using Versecue.Domain.Entities;

namespace Versecue.Application.Interfaces;

public interface IBibleRepository
{
    Task<IReadOnlyList<BibleTranslation>> GetActiveTranslationsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleBook>> GetBooksByTranslationAsync(
        Guid translationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleChapter>> GetChaptersByBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        Guid chapterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        string translationCode,
        string bookName,
        int chapterNumber,
        int verseStart,
        int? verseEnd = null,
        CancellationToken cancellationToken = default);
}