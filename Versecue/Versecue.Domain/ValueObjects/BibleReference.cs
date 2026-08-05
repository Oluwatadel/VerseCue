using Versecue.Domain.Enums;

namespace Versecue.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a canonical Bible reference.
/// Equality is by value (Book, Chapter, VerseStart, VerseEnd).
/// Whole-chapter references have VerseStart = VerseEnd = null.
/// </summary>
public readonly record struct BibleReference
{
    public int BookId { get; init; }
    public int ChapterNumber { get; init; }
    public int? VerseStart { get; init; }
    public int? VerseEnd { get; init; }

    public BibleReference(int bookId, int chapterNumber, int? verseStart = null, int? verseEnd = null)
    {
        if (bookId <= 0) throw new ArgumentException("BookId must be positive", nameof(bookId));
        if (chapterNumber <= 0) throw new ArgumentException("ChapterNumber must be positive", nameof(chapterNumber));
        if (verseStart.HasValue && verseStart.Value <= 0) throw new ArgumentException("VerseStart must be positive", nameof(verseStart));
        if (verseEnd.HasValue && verseEnd.Value <= 0) throw new ArgumentException("VerseEnd must be positive", nameof(verseEnd));
        if (verseStart.HasValue && verseEnd.HasValue && verseEnd.Value < verseStart.Value)
            throw new ArgumentException("VerseEnd must be >= VerseStart", nameof(verseEnd));

        BookId = bookId;
        ChapterNumber = chapterNumber;
        VerseStart = verseStart;
        VerseEnd = verseEnd;
    }

    public bool IsWholeChapter => !VerseStart.HasValue && !VerseEnd.HasValue;
    public bool IsSingleVerse => VerseStart.HasValue && VerseEnd.HasValue && VerseStart.Value == VerseEnd.Value;
    public bool IsVerseRange => VerseStart.HasValue && VerseEnd.HasValue && VerseStart.Value != VerseEnd.Value;

    public override string ToString()
    {
        var book = BookId; // Will be resolved via repository for display
        var chapter = ChapterNumber;
        if (IsWholeChapter) return $"{book} {chapter}";
        if (IsSingleVerse) return $"{book} {chapter}:{VerseStart}";
        if (IsVerseRange) return $"{book} {chapter}:{VerseStart}-{VerseEnd}";
        return $"{book} {chapter}";
    }

    public static BibleReference WholeChapter(int bookId, int chapterNumber) => new(bookId, chapterNumber);
    public static BibleReference SingleVerse(int bookId, int chapterNumber, int verse) => new(bookId, chapterNumber, verse, verse);
    public static BibleReference VerseRange(int bookId, int chapterNumber, int verseStart, int verseEnd) => new(bookId, chapterNumber, verseStart, verseEnd);
}