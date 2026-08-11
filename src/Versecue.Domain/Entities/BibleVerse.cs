using Versecue.Domain.Exceptions;

namespace Versecue.Domain.Entities;

/// <summary>
/// Bible verse entity - leaf of the reference data hierarchy.
/// </summary>
public class BibleVerse
{
    public Guid Id { get; private set; }
    public Guid ChapterId { get; private set; }
    public int VerseNumber { get; private set; }
    public string Text { get; private set; }

    // Navigation
    public BibleChapter? Chapter { get; private set; }

    private BibleVerse() { } // EF Core

    public BibleVerse(Guid chapterId, int verseNumber, string text)
    {
        if (verseNumber <= 0) throw new BibleVerseArgumentException($"VerseNumber must be positive, {nameof(verseNumber)}");
        if (string.IsNullOrWhiteSpace(text)) throw new BibleVerseArgumentException($"Text required, {nameof(text)}");

        Id = Guid.NewGuid();
        ChapterId = chapterId;
        VerseNumber = verseNumber;
        Text = text.Trim();
    }

    public BibleVerse(int verseNumber, string text, BibleChapter? chapter = null)
        : this(chapter.Id, verseNumber, text)
    {
        Chapter = chapter;
    }

}