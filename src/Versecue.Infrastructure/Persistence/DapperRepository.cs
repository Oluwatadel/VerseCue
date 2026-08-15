using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Versecue.Application.Interfaces;
using Versecue.Application.Models.Bible;
using Versecue.Domain.Enums;
using Versecue.Infrastructure.Persistence.Helper;

namespace Versecue.Infrastructure.Persistence;

public sealed class DapperBibleRepository : IBibleRepository
{
    private readonly string _connectionString;

    public DapperBibleRepository(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("VerseCue")
            ?? throw new InvalidOperationException(
                "Connection string 'VerseCue' was not configured.");

        //System.Diagnostics.Debug.WriteLine(
        //    $"DAPPER CONNECTION: {_connectionString}");

        SqlMapper.AddTypeHandler(new GuidTypeHandler());
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    // ============================================================
    // TRANSLATIONS
    // ============================================================

    public async Task<IReadOnlyList<BibleTranslationListItem>>
        GetActiveTranslationsAsync(
            CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id,
                Code,
                Name,
                Language
            FROM BibleTranslations
            WHERE IsActive = 1
            ORDER BY Name;
            """;

        await using var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            cancellationToken: cancellationToken);

        /*
         * SQLite stores Guid values as TEXT.
         *
         * Therefore Id is intentionally read as string.
         */

        var rows =
            await connection.QueryAsync<TranslationRow>(command);

        var result =
            rows.Select(row => new BibleTranslationListItem
            {
                Id = Guid.Parse(row.Id),
                Code = row.Code,
                Name = row.Name,
                Language = row.Language
            })
            .ToList();

        System.Diagnostics.Debug.WriteLine(
            $"DAPPER: Found {result.Count} active translations.");

        return result;
    }

    // ============================================================
    // BOOKS
    // ============================================================

    public async Task<IReadOnlyList<BibleBookListItem>>
    GetBooksByTranslationAsync(
        Guid translationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        // 1. Check the exact ID being sent.
        System.Diagnostics.Debug.WriteLine(
            $"DAPPER BOOK QUERY ID: {translationId}");

        System.Diagnostics.Debug.WriteLine(
            $"DAPPER BOOK QUERY ID STRING: {translationId:D}");

        // 2. See what SQLite actually has.
        const string diagnosticSql = """
        SELECT
            Id,
            TranslationId,
            Name
        FROM BibleBooks
        ORDER BY CanonicalOrder
        LIMIT 5;
        """;

        var diagnostic =
            await connection.QueryAsync(
                new CommandDefinition(
                    diagnosticSql,
                    cancellationToken: cancellationToken));

        foreach (var row in diagnostic)
        {
            System.Diagnostics.Debug.WriteLine(
                $"DB BOOK: {row.Name} | " +
                $"TranslationId={row.TranslationId} | " +
                $"Type={row.TranslationId?.GetType().Name}");
        }

        // 3. Now query using the exact SQLite TEXT representation.
        const string sql = """
        SELECT
            Id,
            TranslationId,
            CanonicalOrder,
            Name,
            Testament
        FROM BibleBooks
        WHERE LOWER(TranslationId) = LOWER(@TranslationId)
        ORDER BY CanonicalOrder;
        """;

        var command = new CommandDefinition(
            sql,
            new
            {
                TranslationId = translationId.ToString("D")
            },
            cancellationToken: cancellationToken);

        var books =
            await connection.QueryAsync<BibleBookListItem>(
                command);

        var result = books.AsList();

        System.Diagnostics.Debug.WriteLine(
            $"DAPPER: Found {result.Count} books for translation {translationId}");

        foreach (var book in result.Take(5))
        {
            System.Diagnostics.Debug.WriteLine(
                $"BOOK: {book.Name} | " +
                $"TranslationId={book.TranslationId}");
        }

        return result;
    }
    // ============================================================
    // CHAPTERS
    // ============================================================

    public async Task<IReadOnlyList<BibleChapterListItem>>
        GetChaptersByBookAsync(
            Guid bookId,
            CancellationToken cancellationToken = default)
    {
        const string sql = """
                SELECT
                    Id,
                    BookId,
                    ChapterNumber
                FROM BibleChapters
                WHERE BookId = @BookId
                ORDER BY ChapterNumber;
                """;

        await using var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                BookId =
                    bookId.ToString().ToUpperInvariant()
            },
            cancellationToken: cancellationToken);

        var rows =
            await connection.QueryAsync<ChapterRow>(command);

        var result =
            rows.Select(row => new BibleChapterListItem
            {
                Id =
                    Guid.Parse(row.Id),

                BookId =
                    Guid.Parse(row.BookId),

                ChapterNumber =
                    row.ChapterNumber
            })
            .ToList();

        System.Diagnostics.Debug.WriteLine(
            $"DAPPER: Found {result.Count} chapters " +
            $"for book {bookId}");

        return result;
    }

    // ============================================================
    // VERSES - BY CHAPTER ID
    // ============================================================

    public async Task<IReadOnlyList<BibleVerseListItem>> GetVersesAsync(
    Guid chapterId,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            Id,
            ChapterId,
            VerseNumber,
            VerseEndNumber,
            Text
        FROM BibleVerses
        WHERE UPPER(ChapterId) = UPPER(@ChapterId)
        ORDER BY VerseNumber;
        """;

        await using var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        var chapterIdString = chapterId.ToString();

        Console.WriteLine(
            $"DAPPER VERSE QUERY ID: {chapterIdString}");

        var rows = (
            await connection.QueryAsync<BibleVerseDbRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ChapterId = chapterIdString
                    },
                    cancellationToken: cancellationToken))
        ).ToList();

        Console.WriteLine(
            $"DAPPER: Found {rows.Count} verses for chapter {chapterId}");

        return rows.Select(x => new BibleVerseListItem
        {
            Id = Guid.Parse(x.Id),
            ChapterId = Guid.Parse(x.ChapterId),
            VerseNumber = x.VerseNumber,
            VerseEndNumber = x.VerseEndNumber,
            Text = x.Text
        }).ToList();
    }

    // ============================================================
    // VERSES - BY TRANSLATION / BOOK / CHAPTER / RANGE
    // ============================================================

    public async Task<IReadOnlyList<BibleVerseListItem>>
        GetVersesAsync(
            string translationCode,
            string bookName,
            int chapterNumber,
            int verseStart,
            int? verseEnd = null,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(translationCode))
        {
            throw new ArgumentException(
                "Translation code is required.",
                nameof(translationCode));
        }

        if (string.IsNullOrWhiteSpace(bookName))
        {
            throw new ArgumentException(
                "Book name is required.",
                nameof(bookName));
        }

        if (chapterNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chapterNumber));
        }

        if (verseStart <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verseStart));
        }

        if (verseEnd.HasValue &&
            verseEnd.Value < verseStart)
        {
            throw new ArgumentException(
                "Verse end cannot be less than verse start.",
                nameof(verseEnd));
        }

        const string sql = """
            SELECT
                v.Id,
                v.ChapterId,
                v.VerseNumber,
                v.VerseEndNumber,
                v.Text
            FROM BibleVerses v

            INNER JOIN BibleChapters c
                ON c.Id = v.ChapterId

            INNER JOIN BibleBooks b
                ON b.Id = c.BookId

            INNER JOIN BibleTranslations t
                ON t.Id = b.TranslationId

            WHERE t.Code = @TranslationCode
              AND b.Name = @BookName
              AND c.ChapterNumber = @ChapterNumber
              AND v.VerseNumber >= @VerseStart
              AND (
                    @VerseEnd IS NULL
                    OR v.VerseNumber <= @VerseEnd
                  )

            ORDER BY v.VerseNumber;
            """;

        await using var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                TranslationCode =
                    translationCode.Trim().ToUpperInvariant(),

                BookName =
                    bookName.Trim(),

                ChapterNumber =
                    chapterNumber,

                VerseStart =
                    verseStart,

                VerseEnd =
                    verseEnd
            },
            cancellationToken: cancellationToken);

        var rows =
            await connection.QueryAsync<VerseRow>(command);

        var result =
            rows.Select(row => new BibleVerseListItem
            {
                Id =
                    Guid.Parse(row.Id),

                ChapterId =
                    Guid.Parse(row.ChapterId),

                VerseNumber =
                    row.VerseNumber,

                VerseEndNumber =
                    row.VerseEndNumber,

                Text =
                    row.Text
            })
            .ToList();

        System.Diagnostics.Debug.WriteLine(
            $"DAPPER: Found {result.Count} verses " +
            $"for {translationCode} {bookName} " +
            $"chapter {chapterNumber}");

        return result;
    }

    // ============================================================
    // NEXT VERSE
    // ============================================================

    public async Task<BibleVerseNavigationItem?>
        GetNextVerseAsync(
            Guid translationId,
            Guid currentVerseId,
            CancellationToken cancellationToken = default)
    {
        const string currentVerseSql = """
            SELECT
                b.CanonicalOrder,
                c.ChapterNumber,
                v.VerseNumber
            FROM BibleVerses v

            INNER JOIN BibleChapters c
                ON c.Id = v.ChapterId

            INNER JOIN BibleBooks b
                ON b.Id = c.BookId

            WHERE v.Id = @CurrentVerseId COLLATE NOCASE
              AND b.TranslationId = @TranslationId COLLATE NOCASE

            LIMIT 1;
            """;

        const string nextVerseSql = """
            SELECT
                t.Id AS TranslationId,
                t.Code AS TranslationCode,
                b.Id AS BookId,
                b.Name AS BookName,
                b.CanonicalOrder AS BookCanonicalOrder,
                c.Id AS ChapterId,
                c.ChapterNumber,
                v.Id AS VerseId,
                v.VerseNumber,
                v.VerseEndNumber,
                v.Text
            FROM BibleVerses v

            INNER JOIN BibleChapters c
                ON c.Id = v.ChapterId

            INNER JOIN BibleBooks b
                ON b.Id = c.BookId

            INNER JOIN BibleTranslations t
                ON t.Id = b.TranslationId

            WHERE t.Id = @TranslationId COLLATE NOCASE
              AND
              (
                    b.CanonicalOrder > @CanonicalOrder
                 OR (
                        b.CanonicalOrder = @CanonicalOrder
                    AND c.ChapterNumber > @ChapterNumber
                    )
                 OR (
                        b.CanonicalOrder = @CanonicalOrder
                    AND c.ChapterNumber = @ChapterNumber
                    AND v.VerseNumber > @VerseNumber
                    )
              )

            ORDER BY
                b.CanonicalOrder,
                c.ChapterNumber,
                v.VerseNumber

            LIMIT 1;
            """;

        await using var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        var currentVerse =
            await connection.QueryFirstOrDefaultAsync<CurrentVerseRow>(
                new CommandDefinition(
                    currentVerseSql,
                    new
                    {
                        TranslationId =
                            translationId.ToString("D"),

                        CurrentVerseId =
                            currentVerseId.ToString("D")
                    },
                    commandTimeout: 5,
                    cancellationToken: cancellationToken));

        if (currentVerse is null)
        {
            return null;
        }

        var row =
            await connection.QueryFirstOrDefaultAsync<NextVerseRow>(
                new CommandDefinition(
                    nextVerseSql,
                    new
                    {
                        TranslationId =
                            translationId.ToString("D"),

                        CanonicalOrder =
                            currentVerse.CanonicalOrder,

                        ChapterNumber =
                            currentVerse.ChapterNumber,

                        VerseNumber =
                            currentVerse.VerseNumber
                    },
                    commandTimeout: 5,
                    cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return new BibleVerseNavigationItem
        {
            TranslationId =
                Guid.Parse(row.TranslationId),

            TranslationCode =
                row.TranslationCode,

            BookId =
                Guid.Parse(row.BookId),

            BookName =
                row.BookName,

            BookCanonicalOrder =
                row.BookCanonicalOrder,

            ChapterId =
                Guid.Parse(row.ChapterId),

            ChapterNumber =
                row.ChapterNumber,

            Verse =
                new BibleVerseListItem
                {
                    Id =
                        Guid.Parse(row.VerseId),

                    ChapterId =
                        Guid.Parse(row.ChapterId),

                    VerseNumber =
                        row.VerseNumber,

                    VerseEndNumber =
                        row.VerseEndNumber,

                    Text =
                        row.Text
                }
        };
    }

    public async Task<BibleVerseNavigationItem?>
        GetPreviousVerseAsync(
            Guid translationId,
            Guid currentVerseId,
            CancellationToken cancellationToken = default)
    {
        const string currentVerseSql = @"
            SELECT
                b.CanonicalOrder,
                c.ChapterNumber,
                v.VerseNumber
            FROM BibleVerses v
            INNER JOIN BibleChapters c
                ON c.Id = v.ChapterId
            INNER JOIN BibleBooks b
                ON b.Id = c.BookId
            WHERE v.Id = @CurrentVerseId COLLATE NOCASE
              AND b.TranslationId = @TranslationId COLLATE NOCASE
            LIMIT 1;
            ";

        const string prevVerseSql = @"
            SELECT
                t.Id AS TranslationId,
                t.Code AS TranslationCode,
                b.Id AS BookId,
                b.Name AS BookName,
                b.CanonicalOrder AS BookCanonicalOrder,
                c.Id AS ChapterId,
                c.ChapterNumber,
                v.Id AS VerseId,
                v.VerseNumber,
                v.VerseEndNumber,
                v.Text
            FROM BibleVerses v
            INNER JOIN BibleChapters c
                ON c.Id = v.ChapterId
            INNER JOIN BibleBooks b
                ON b.Id = c.BookId
            INNER JOIN BibleTranslations t
                ON t.Id = b.TranslationId
            WHERE t.Id = @TranslationId COLLATE NOCASE
              AND
              (
                    b.CanonicalOrder < @CanonicalOrder
                 OR (
                        b.CanonicalOrder = @CanonicalOrder
                    AND c.ChapterNumber < @ChapterNumber
                    )
                 OR (
                        b.CanonicalOrder = @CanonicalOrder
                    AND c.ChapterNumber = @ChapterNumber
                    & v.VerseNumber < @VerseNumber
                    )
              )
            ORDER BY
                b.CanonicalOrder DESC,
                c.ChapterNumber DESC,
                v.VerseNumber DESC
            LIMIT 1;
            ";

        // Wait! In the prevVerseSql, we need "AND" instead of "&". Let's correct it:
        // OR ( b.CanonicalOrder = @CanonicalOrder AND c.ChapterNumber = @ChapterNumber AND v.VerseNumber < @VerseNumber )

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var currentVerse =
            await connection.QueryFirstOrDefaultAsync<CurrentVerseRow>(
                new CommandDefinition(
                    currentVerseSql,
                    new
                    {
                        TranslationId =
                            translationId.ToString("D"),
                        CurrentVerseId =
                            currentVerseId.ToString("D")
                    },
                    commandTimeout: 5,
                    cancellationToken: cancellationToken));

        if (currentVerse is null)
        {
            return null;
        }

        var queryPrevSql = prevVerseSql.Replace("&", "AND");

        var row =
            await connection.QueryFirstOrDefaultAsync<NextVerseRow>(
                new CommandDefinition(
                    queryPrevSql,
                    new
                    {
                        TranslationId =
                            translationId.ToString("D"),
                        CanonicalOrder =
                            currentVerse.CanonicalOrder,
                        ChapterNumber =
                            currentVerse.ChapterNumber,
                        VerseNumber =
                            currentVerse.VerseNumber
                    },
                    commandTimeout: 5,
                    cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return new BibleVerseNavigationItem
        {
            TranslationId =
                Guid.Parse(row.TranslationId),
            TranslationCode =
                row.TranslationCode,
            BookId =
                Guid.Parse(row.BookId),
            BookName =
                row.BookName,
            BookCanonicalOrder =
                row.BookCanonicalOrder,
            ChapterId =
                Guid.Parse(row.ChapterId),
            ChapterNumber =
                row.ChapterNumber,
            Verse =
                new BibleVerseListItem
                {
                    Id =
                        Guid.Parse(row.VerseId),
                    ChapterId =
                        Guid.Parse(row.ChapterId),
                    VerseNumber =
                        row.VerseNumber,
                    VerseEndNumber =
                        row.VerseEndNumber,
                    Text =
                        row.Text
                }
        };
    }

    public async Task<IReadOnlyList<BibleSearchResultItem>>
        SearchVersesAsync(
            Guid translationId,
            string query,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<BibleSearchResultItem>();
        }

        const string sql = @"
            SELECT
                v.Id AS VerseId,
                b.Id AS BookId,
                b.Name AS BookName,
                c.ChapterNumber,
                v.VerseNumber,
                v.VerseEndNumber,
                v.Text,
                t.Id AS TranslationId,
                t.Code AS TranslationCode
            FROM BibleVerses v
            INNER JOIN BibleChapters c
                ON c.Id = v.ChapterId
            INNER JOIN BibleBooks b
                ON b.Id = c.BookId
            INNER JOIN BibleTranslations t
                ON t.Id = b.TranslationId
            WHERE t.Id = @TranslationId COLLATE NOCASE
              AND v.Text LIKE @Query
            ORDER BY
                b.CanonicalOrder,
                c.ChapterNumber,
                v.VerseNumber
            LIMIT 100;
            ";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            sql,
            new
            {
                TranslationId = translationId.ToString("D"),
                Query = $"%{query.Trim()}%"
            },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<BibleSearchResultItem>(command);
        return rows.ToList();
    }

    // ============================================================
    // INTERNAL DAPPER ROW TYPES
    // ============================================================

    /*
     * IMPORTANT:
     *
     * Do not use Guid for these SQLite Id columns.
     *
     * Your database has:
     *
     * Id TEXT
     * TranslationId TEXT
     * BookId TEXT
     * ChapterId TEXT
     *
     * So Dapper reads them as string.
     *
     * We convert them with Guid.Parse() afterwards.
     */

    private sealed class TranslationRow
    {
        public string Id { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;
    }

    private sealed class BibleBookRow
    {
        public string Id { get; set; } = string.Empty;

        public string TranslationId { get; set; } = string.Empty;

        public int CanonicalOrder { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Testament { get; set; }
    }

    private sealed class ChapterRow
    {
        public string Id { get; set; } = string.Empty;

        public string BookId { get; set; } = string.Empty;

        public int ChapterNumber { get; set; }
    }

    private sealed class VerseRow
    {
        public string Id { get; set; } = string.Empty;

        public string ChapterId { get; set; } = string.Empty;

        public int VerseNumber { get; set; }

        public int? VerseEndNumber { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class BibleVerseDbRow
    {
        public string Id { get; set; } = string.Empty;

        public string ChapterId { get; set; } = string.Empty;

        public int VerseNumber { get; set; }

        public int? VerseEndNumber { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class NextVerseRow
    {
        public string TranslationId { get; set; } = string.Empty;

        public string TranslationCode { get; set; } = string.Empty;

        public string BookId { get; set; } = string.Empty;

        public string BookName { get; set; } = string.Empty;

        public int BookCanonicalOrder { get; set; }

        public string ChapterId { get; set; } = string.Empty;

        public int ChapterNumber { get; set; }

        public string VerseId { get; set; } = string.Empty;

        public int VerseNumber { get; set; }

        public int? VerseEndNumber { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private sealed class CurrentVerseRow
    {
        public int CanonicalOrder { get; set; }

        public int ChapterNumber { get; set; }

        public int VerseNumber { get; set; }
    }
}
