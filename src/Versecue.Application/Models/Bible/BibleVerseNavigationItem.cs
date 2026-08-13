namespace Versecue.Application.Models.Bible;

public sealed class BibleVerseNavigationItem
{
    public Guid TranslationId { get; init; }

    public string TranslationCode { get; init; } = string.Empty;

    public Guid BookId { get; init; }

    public string BookName { get; init; } = string.Empty;

    public int BookCanonicalOrder { get; init; }

    public Guid ChapterId { get; init; }

    public int ChapterNumber { get; init; }

    public BibleVerseListItem Verse { get; init; } = null!;
}
