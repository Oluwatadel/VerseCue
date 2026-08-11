using Versecue.Domain.Exceptions;

namespace Versecue.Domain.Entities;

/// <summary>
/// Bible chapter entity.
/// </summary>
public class BibleChapter
{
    public Guid Id { get; private set; }
    public Guid BookId { get; private set; }
    public int ChapterNumber { get; private set; }

    // Navigation
    public BibleBook? Book { get; private set; }
    private readonly List<BibleVerse> _verses = [];
    public IReadOnlyCollection<BibleVerse> Verses => _verses.AsReadOnly();

    private BibleChapter() { } // EF Core

    public BibleChapter(Guid bookId, int chapterNumber)
    {
        if (chapterNumber <= 0) 
            throw new NegativeBibleChapterException($"ChapterNumber must be positive, {nameof(chapterNumber)}");
        Id = Guid.NewGuid();
        BookId = bookId;
        ChapterNumber = chapterNumber;
    }

    public BibleChapter(int chapterNumber, BibleBook book)
        : this(book.Id, chapterNumber)
    {
        Book = book;
    }


    public void AddVerse(BibleVerse verse) => _verses.Add(verse);
}