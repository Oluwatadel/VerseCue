using Dapper;
using Microsoft.Data.Sqlite;
using Versecue.Application.Bible;
using Versecue.Application.Interfaces;

namespace Versecue.Infrastructure.Services;

public sealed class DapperBibleRepository : IBibleRepository
{
    private readonly string _connectionString;

    public DapperBibleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<BibleVerse?> GetVerseAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default)
    {
        var verses = await GetVersesAsync(
            reference,
            cancellationToken);

        return verses.FirstOrDefault();
    }

    public async Task<IReadOnlyList<BibleVerse>> GetVersesAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var connection =
            new SqliteConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                BookName,
                ChapterNumber,
                VerseNumber,
                Text
            FROM BibleVerses
            WHERE BookName = @BookName
              AND ChapterNumber = @ChapterNumber
              AND VerseNumber >= @VerseStart
              AND (
                    @VerseEnd IS NULL
                    OR VerseNumber <= @VerseEnd
                  )
            ORDER BY VerseNumber;
            """;

        var rows = await connection.QueryAsync<BibleVerse>(
            new CommandDefinition(
                sql,
                new
                {
                    BookName = reference.BookName,
                    ChapterNumber = reference.ChapterNumber,
                    VerseStart = reference.VerseStart,
                    VerseEnd = reference.VerseEnd
                },
                cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
