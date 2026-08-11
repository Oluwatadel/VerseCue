using Dapper;
using Microsoft.Data.Sqlite;
using Versecue.Application.Interfaces;
using Versecue.Domain.Entities;

namespace Versecue.Infrastructure.Persistence;

public sealed class DapperBibleRepository : IBibleRepository
{
    private readonly string _connectionString;

    public DapperBibleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        string translationCode,
        string bookName,
        int chapterNumber,
        int verseStart,
        int? verseEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(translationCode))
            throw new ArgumentException(
                "Translation code is required.",
                nameof(translationCode));

        if (string.IsNullOrWhiteSpace(bookName))
            throw new ArgumentException(
                "Book name is required.",
                nameof(bookName));

        if (chapterNumber <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(chapterNumber));

        if (verseStart <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(verseStart));

        if (verseEnd.HasValue && verseEnd.Value < verseStart)
            throw new ArgumentException(
                "Verse end cannot be less than verse start.",
                nameof(verseEnd));

        const string sql = """
            SELECT
                v.Id,
                v.ChapterId,
                v.VerseNumber,
                v.Text
            FROM BibleVerse v
            INNER JOIN BibleChapter c
                ON c.Id = v.ChapterId
            INNER JOIN BibleBook b
                ON b.Id = c.BookId
            INNER JOIN BibleTranslation t
                ON t.Id = b.TranslationId
            WHERE t.Code = @TranslationCode
              AND (
                    b.Name = @BookName
                    OR EXISTS (
                        SELECT 1
                        FROM json_each(b.Aliases)
                        WHERE value = @BookName
                    )
                  )
              AND c.ChapterNumber = @ChapterNumber
              AND v.VerseNumber >= @VerseStart
              AND (
                    @VerseEnd IS NULL
                    OR v.VerseNumber <= @VerseEnd
                  )
            ORDER BY v.VerseNumber;
            """;

        await using var connection = new SqliteConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                TranslationCode = translationCode,
                BookName = bookName,
                ChapterNumber = chapterNumber,
                VerseStart = verseStart,
                VerseEnd = verseEnd
            },
            cancellationToken: cancellationToken);

        var verses = await connection.QueryAsync<BibleVerse>(
            command);

        return verses.AsList();
    }
}