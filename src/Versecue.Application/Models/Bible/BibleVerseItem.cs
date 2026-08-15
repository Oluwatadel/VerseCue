namespace Versecue.Application.Models.Bible;

public sealed class BibleVerseListItem
{
    public Guid Id { get; init; }

    public Guid ChapterId { get; init; }

    public int VerseNumber { get; init; }

    public int? VerseEndNumber { get; init; }

    public string Text { get; init; } = string.Empty;
}

