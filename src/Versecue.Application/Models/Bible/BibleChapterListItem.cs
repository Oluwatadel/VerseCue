namespace Versecue.Application.Models.Bible;

public sealed class BibleChapterListItem
{
    public Guid Id { get; init; }

    public Guid BookId { get; init; }

    public int ChapterNumber { get; init; }
}