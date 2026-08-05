using Versecue.Domain.Entities;
using Versecue.Domain.ValueObjects;

namespace Versecue.Application.Interfaces;

/// <summary>
/// Repository for Bible reference data (read-only at runtime).
/// Accessed via Dapper for performance.
/// </summary>
public interface IBibleRepository
{
    Task<BibleTranslation?> GetTranslationByIdAsync(int id, CancellationToken ct = default);
    Task<BibleTranslation?> GetTranslationByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<BibleTranslation>> GetActiveTranslationsAsync(CancellationToken ct = default);

    Task<BibleBook?> GetBookByIdAsync(int id, CancellationToken ct = default);
    Task<BibleBook?> GetBookByAliasAsync(int translationId, string alias, CancellationToken ct = default);
    Task<IReadOnlyList<BibleBook>> GetBooksByTranslationAsync(int translationId, CancellationToken ct = default);

    Task<BibleChapter?> GetChapterAsync(int bookId, int chapterNumber, CancellationToken ct = default);
    Task<IReadOnlyList<BibleChapter>> GetChaptersByBookAsync(int bookId, CancellationToken ct = default);

    Task<BibleVerse?> GetVerseAsync(int chapterId, int verseNumber, CancellationToken ct = default);
    Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int chapterId, CancellationToken ct = default);
    Task<IReadOnlyList<BibleVerse>> GetVerseRangeAsync(int chapterId, int verseStart, int verseEnd, CancellationToken ct = default);

    /// <summary>
    /// Hot path: Get passage text for a resolved reference.
    /// Returns concatenated verse text with verse numbers.
    /// </summary>
    Task<string?> GetPassageTextAsync(BibleReference reference, int translationId, CancellationToken ct = default);

    /// <summary>
    /// Search verses by text (for manual search).
    /// </summary>
    Task<IReadOnlyList<(BibleVerse Verse, BibleBook Book, BibleChapter Chapter)>> SearchVersesAsync(
        int translationId, string query, int limit = 50, CancellationToken ct = default);
}