using Versecue.Application.Models.Bible;

namespace Versecue.Application.Interfaces;

public interface IBibleRepository
{
    Task<IReadOnlyList<BibleTranslationListItem>>
        GetActiveTranslationsAsync(
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleBookListItem>>
        GetBooksByTranslationAsync(
            Guid translationId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleChapterListItem>>
        GetChaptersByBookAsync(
            Guid bookId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerseListItem>>
        GetVersesAsync(
            Guid chapterId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerseListItem>>
        GetVersesAsync(
            string translationCode,
            string bookName,
            int chapterNumber,
            int verseStart,
            int? verseEnd = null,
            CancellationToken cancellationToken = default);

    Task<BibleVerseNavigationItem?>
        GetNextVerseAsync(
            Guid translationId,
            Guid currentVerseId,
            CancellationToken cancellationToken = default);
}
