using Versecue.Domain.Exceptions;

namespace Versecue.Domain.Entities;

/// <summary>
/// Bible verse entity - leaf of the reference data hierarchy.
/// </summary>
public class BibleVerse
{
    public int Id { get; private set; }
    public int ChapterId { get; private set; }
    public int VerseNumber { get; private set; }
    public string Text { get; private set; }

    // Navigation
    public BibleChapter? Chapter { get; private set; }

    private BibleVerse() { } // EF Core

    public BibleVerse(int chapterId, int verseNumber, string text)
    {
        if (verseNumber <= 0) throw new BibleVerseArgumentException($"VerseNumber must be positive, {nameof(verseNumber)}");
        if (string.IsNullOrWhiteSpace(text)) throw new BibleVerseArgumentException($"Text required, {nameof(text)}");

        ChapterId = chapterId;
        VerseNumber = verseNumber;
        Text = text.Trim();
    }

    public BibleVerse(int id, int chapterId, int verseNumber, string text, BibleChapter? chapter = null)
        : this(chapterId, verseNumber, text)
    {
        Id = id;
        Chapter = chapter;
    }

}