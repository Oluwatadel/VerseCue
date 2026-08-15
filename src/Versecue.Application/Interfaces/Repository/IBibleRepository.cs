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

    Task<BibleVerseNavigationItem?>
        GetPreviousVerseAsync(
            Guid translationId,
            Guid currentVerseId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleSearchResultItem>>
        SearchVersesAsync(
            Guid translationId,
            string query,
            CancellationToken cancellationToken = default);
}

public sealed class BibleSearchResultItem
{
    public Guid VerseId { get; set; }
    public Guid BookId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int ChapterNumber { get; set; }
    public int VerseNumber { get; set; }
    public int? VerseEndNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid TranslationId { get; set; }
    public string TranslationCode { get; set; } = string.Empty;
}
