using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Versecue.Application.Interfaces;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;

namespace Versecue.Infrastructure.Persistence.Dapper;

/// <summary>
/// Complete Dapper implementation of IBibleRepository that correctly maps 
/// database results to domain entities while preserving all ID values.
/// </summary>
public sealed class DapperBibleRepository : IBibleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperBibleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Translation Methods

    public async Task<BibleTranslation?> GetTranslationByIdAsync(int id, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = @"SELECT Id, Code, Name, Language, LicenseInfo, IsActive 
                              FROM BibleTranslations 
                              WHERE Id = @id";

        var result = await conn.QuerySingleOrDefaultAsync<BibleTranslationDto>(sql, new { id });
        return result?.ToEntity();
    }

    public async Task<BibleTranslation?> GetTranslationByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = @"SELECT Id, Code, Name, Language, LicenseInfo, IsActive 
                              FROM BibleTranslations 
                              WHERE Code = @code COLLATE NOCASE";

        var result = await conn.QuerySingleOrDefaultAsync<BibleTranslationDto>(sql, new { code });
        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<BibleTranslation>> GetActiveTranslationsAsync(CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = "SELECT Id, Code, Name, Language, LicenseInfo, IsActive FROM BibleTranslations WHERE IsActive = 1";

        var results = await conn.QueryAsync<BibleTranslationDto>(sql);
        return results.ToList().ConvertAll(r => r.ToEntity());
    }

    #endregion

    #region Book Methods

    public async Task<BibleBook?> GetBookByIdAsync(int bookId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT b.Id, b.TranslationId, b.CanonicalOrder, b.Name, b.Testament, b.AliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleBooks b
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE b.Id = @bookId
        """;

        var result = await conn.QuerySingleOrDefaultAsync<BibleBookDto>(sql, new { bookId });
        return result?.ToEntity();
    }

    public async Task<BibleBook?> GetBookByAliasAsync(int translationId, string alias, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT b.Id, b.TranslationId, b.CanonicalOrder, b.Name, b.Testament, b.AliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleBooks b
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE b.TranslationId = @translationId AND b.Name = @alias COLLATE NOCASE
        """;

        var result = await conn.QuerySingleOrDefaultAsync<BibleBookDto>(sql, new { translationId, alias });
        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<BibleBook>> GetBooksByTranslationAsync(int translationId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT b.Id, b.TranslationId, b.CanonicalOrder, b.Name, b.Testament, b.AliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleBooks b
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE b.TranslationId = @translationId
            ORDER BY b.CanonicalOrder
        """;

        var results = await conn.QueryAsync<BibleBookDto>(sql, new { translationId });
        return results.ToList().ConvertAll(r => r.ToEntity());
    }

    #endregion

    #region Chapter Methods

    public async Task<BibleChapter?> GetChapterAsync(int bookId, int chapterNumber, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT c.Id, c.BookId, c.ChapterNumber,
                   b.Id as BookId, b.TranslationId as BookTranslationId, b.CanonicalOrder as BookCanonicalOrder, b.Name as BookName, b.Testament as BookTestament, b.AliasesJson as BookAliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleChapters c
            INNER JOIN BibleBooks b ON c.BookId = b.Id
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE c.BookId = @bookId AND c.ChapterNumber = @chapterNumber
        """;

        var result = await conn.QuerySingleOrDefaultAsync<BibleChapterDto>(sql, new { bookId, chapterNumber });
        return result?.ToEntity();
    }

    public async Task<IReadOnlyList<BibleChapter>> GetChaptersByBookAsync(int bookId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT c.Id, c.BookId, c.ChapterNumber,
                   b.Id as BookId, b.TranslationId as BookTranslationId, b.CanonicalOrder as BookCanonicalOrder, b.Name as BookName, b.Testament as BookTestament, b.AliasesJson as BookAliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleChapters c
            INNER JOIN BibleBooks b ON c.BookId = b.Id
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE c.BookId = @bookId
            ORDER BY c.ChapterNumber
        """;

        var results = await conn.QueryAsync<BibleChapterDto>(sql, new { bookId });
        return results.ToList().ConvertAll(r => r.ToEntity());
    }

    #endregion

    #region Verse Methods

    public async Task<BibleVerse?> GetVerseAsync(int chapterId, int verseNumber, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT v.Id, v.ChapterId, v.VerseNumber, v.Text,
                   c.Id as ChapterId, c.BookId, c.ChapterNumber,
                   b.Id as BookId, b.TranslationId as BookTranslationId, b.CanonicalOrder as BookCanonicalOrder, b.Name as BookName, b.Testament as BookTestament, b.AliasesJson as BookAliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleVerses v
            INNER JOIN BibleChapters c ON v.ChapterId = c.Id
            INNER JOIN BibleBooks b ON c.BookId = b.Id
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE v.ChapterId = @chapterId AND v.VerseNumber = @verseNumber
        """;

        var result = await conn.QuerySingleOrDefaultAsync<BibleVerseDto>(sql, new { chapterId, verseNumber });
        return result?.ToVerseEntity();
    }

    public async Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int chapterId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT v.Id, v.ChapterId, v.VerseNumber, v.Text,
                   c.Id as ChapterId, c.BookId, c.ChapterNumber,
                   b.Id as BookId, b.TranslationId as BookTranslationId, b.CanonicalOrder as BookCanonicalOrder, b.Name as BookName, b.Testament as BookTestament, b.AliasesJson as BookAliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleVerses v
            INNER JOIN BibleChapters c ON v.ChapterId = c.Id
            INNER JOIN BibleBooks b ON c.BookId = b.Id
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE v.ChapterId = @chapterId
            ORDER BY v.VerseNumber
        """;

        var results = await conn.QueryAsync<BibleVerseDto>(sql, new { chapterId });
        return results.ToList().ConvertAll(r => r.ToVerseEntity());
    }

    public async Task<IReadOnlyList<BibleVerse>> GetVerseRangeAsync(int chapterId, int verseStart, int verseEnd, CancellationToken ct = default)
    {
        if (verseStart > verseEnd)
            throw new ArgumentException("Start verse must be less than or equal to end verse", nameof(verseStart));

        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT v.Id, v.ChapterId, v.VerseNumber, v.Text,
                   c.Id as ChapterId, c.BookId, c.ChapterNumber,
                   b.Id as BookId, b.TranslationId as BookTranslationId, b.CanonicalOrder as BookCanonicalOrder, b.Name as BookName, b.Testament as BookTestament, b.AliasesJson as BookAliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleVerses v
            INNER JOIN BibleChapters c ON v.ChapterId = c.Id
            INNER JOIN BibleBooks b ON c.BookId = b.Id
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE v.ChapterId = @chapterId 
              AND v.VerseNumber BETWEEN @verseStart AND @verseEnd
            ORDER BY v.VerseNumber
        """;

        var results = await conn.QueryAsync<BibleVerseDto>(sql, new { chapterId, verseStart, verseEnd });
        return results.ToList().ConvertAll(r => r.ToVerseEntity());
    }

    public async Task<string?> GetPassageTextAsync(BibleReference reference, int translationId, CancellationToken ct = default)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        if (reference.IsSingleVerse)
        {
            const string sql = """
                SELECT v.Text
                FROM BibleVerses v
                INNER JOIN BibleChapters c ON v.ChapterId = c.Id
                INNER JOIN BibleBooks b ON c.BookId = b.Id
                WHERE b.Id = @bookId AND c.ChapterNumber = @chapterNumber AND v.VerseNumber = @verseNumber
                  AND b.TranslationId = @translationId
            """;

            var text = await conn.QuerySingleOrDefaultAsync<string>(sql, new
            {
                bookId = reference.BookId,
                chapterNumber = reference.ChapterNumber,
                verseNumber = reference.VerseStart!.Value,
                translationId
            });

            return text;
        }
        else
        {
            const string sql = """
                SELECT v.Text
                FROM BibleVerses v
                INNER JOIN BibleChapters c ON v.ChapterId = c.Id
                INNER JOIN BibleBooks b ON c.BookId = b.Id
                WHERE b.Id = @bookId AND c.ChapterNumber = @chapterNumber 
                  AND v.VerseNumber BETWEEN @startVerse AND @endVerse
                  AND b.TranslationId = @translationId
                ORDER BY v.VerseNumber
            """;

            var texts = await conn.QueryAsync<string>(sql, new
            {
                bookId = reference.BookId,
                chapterNumber = reference.ChapterNumber,
                startVerse = reference.VerseStart!.Value,
                endVerse = reference.VerseEnd!.Value,
                translationId
            });

            return string.Join(" ", texts);
        }
    }

    public async Task<IReadOnlyList<(BibleVerse Verse, BibleBook Book, BibleChapter Chapter)>> SearchVersesAsync(
        int translationId, string query, int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<(BibleVerse Verse, BibleBook Book, BibleChapter Chapter)>();

        if (limit <= 0)
            limit = 50;

        using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT v.Id, v.ChapterId, v.VerseNumber, v.Text,
                   c.Id as ChapterId, c.BookId, c.ChapterNumber,
                   b.Id as BookId, b.TranslationId as BookTranslationId, b.CanonicalOrder as BookCanonicalOrder, b.Name as BookName, b.Testament as BookTestament, b.AliasesJson as BookAliasesJson,
                   t.Id as TranslationId, t.Code as TranslationCode, t.Name as TranslationName, t.Language as TranslationLanguage,
                   t.LicenseInfo as TranslationLicenseInfo, t.IsActive as TranslationIsActive
            FROM BibleVerses v
            INNER JOIN BibleChapters c ON v.ChapterId = c.Id
            INNER JOIN BibleBooks b ON c.BookId = b.Id
            INNER JOIN BibleTranslations t ON b.TranslationId = t.Id
            WHERE b.TranslationId = @translationId 
              AND v.Text LIKE @query
            ORDER BY 
                CASE 
                    WHEN v.Text LIKE @exactMatch THEN 0
                    WHEN v.Text LIKE @startsWith THEN 1
                    ELSE 2
                END,
                b.CanonicalOrder, c.ChapterNumber, v.VerseNumber
            LIMIT @limit
        """;

        var searchQuery = $"%{query}%";
        var exactMatch = query;
        var startsWith = $"{query}%";

        var results = await conn.QueryAsync<BibleVerseDto>(sql, new
        {
            translationId,
            query = searchQuery,
            exactMatch,
            startsWith,
            limit
        });

        return results.Select(r => (r.ToVerseEntity(), r.ToBookEntity(), r.ToChapterEntity())).ToList();
    }

    #endregion

    #region DTOs for Clean Mapping

    private sealed class BibleTranslationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Language { get; set; } = "";
        public string LicenseInfo { get; set; } = "";
        public bool IsActive { get; set; }

        public BibleTranslation ToEntity() => new(Id, Code, Name, Language, LicenseInfo, IsActive);
    }

    private sealed class BibleBookDto
    {
        public int Id { get; set; }
        public int TranslationId { get; set; }
        public int CanonicalOrder { get; set; }
        public string Name { get; set; } = "";
        public string Testament { get; set; } = "";
        public string AliasesJson { get; set; } = "";
        public string TranslationCode { get; set; } = "";
        public string TranslationName { get; set; } = "";
        public string TranslationLanguage { get; set; } = "";
        public string TranslationLicenseInfo { get; set; } = "";
        public bool TranslationIsActive { get; set; }

        public BibleBook ToEntity()
        {
            var translation = new BibleTranslation(TranslationId, TranslationCode, TranslationName, TranslationLanguage, TranslationLicenseInfo, TranslationIsActive);
            
            var aliases = string.IsNullOrEmpty(AliasesJson)
                ? Array.Empty<string>()
                : System.Text.Json.JsonSerializer.Deserialize<string[]>(AliasesJson) ?? Array.Empty<string>();

            return new BibleBook(Id, TranslationId, CanonicalOrder, Name, Enum.Parse<Testament>(Testament), aliases, translation);
        }
    }

    private sealed class BibleChapterDto
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int ChapterNumber { get; set; }
        public int BookTranslationId { get; set; }
        public int BookCanonicalOrder { get; set; }
        public string BookName { get; set; } = "";
        public string BookTestament { get; set; } = "";
        public string BookAliasesJson { get; set; } = "";
        public int TranslationId { get; set; }
        public string TranslationCode { get; set; } = "";
        public string TranslationName { get; set; } = "";
        public string TranslationLanguage { get; set; } = "";
        public string TranslationLicenseInfo { get; set; } = "";
        public bool TranslationIsActive { get; set; }

        public BibleChapter ToEntity()
        {
            var translation = new BibleTranslation(TranslationId, TranslationCode, TranslationName, TranslationLanguage, TranslationLicenseInfo, TranslationIsActive);
            
            var aliases = string.IsNullOrEmpty(BookAliasesJson)
                ? Array.Empty<string>()
                : System.Text.Json.JsonSerializer.Deserialize<string[]>(BookAliasesJson) ?? Array.Empty<string>();

            var book = new BibleBook(BookId, BookTranslationId, BookCanonicalOrder, BookName, Enum.Parse<Testament>(BookTestament), aliases, translation);

            return new BibleChapter(Id, BookId, ChapterNumber, book);
        }
    }

    private sealed class BibleVerseDto
    {
        public int Id { get; set; }
        public int ChapterId { get; set; }
        public int VerseNumber { get; set; }
        public string Text { get; set; } = "";
        public int BookId { get; set; }
        public int ChapterNumber { get; set; }
        public int BookTranslationId { get; set; }
        public int BookCanonicalOrder { get; set; }
        public string BookName { get; set; } = "";
        public string BookTestament { get; set; } = "";
        public string BookAliasesJson { get; set; } = "";
        public int TranslationId { get; set; }
        public string TranslationCode { get; set; } = "";
        public string TranslationName { get; set; } = "";
        public string TranslationLanguage { get; set; } = "";
        public string TranslationLicenseInfo { get; set; } = "";
        public bool TranslationIsActive { get; set; }

        public BibleVerse ToVerseEntity() => new(Id, ChapterId, VerseNumber, Text, ToChapterEntity());

        public BibleBook ToBookEntity()
        {
            var translation = new BibleTranslation(TranslationId, TranslationCode, TranslationName, TranslationLanguage, TranslationLicenseInfo, TranslationIsActive);
            
            var aliases = string.IsNullOrEmpty(BookAliasesJson)
                ? Array.Empty<string>()
                : System.Text.Json.JsonSerializer.Deserialize<string[]>(BookAliasesJson) ?? Array.Empty<string>();

            return new BibleBook(BookId, BookTranslationId, BookCanonicalOrder, BookName, Enum.Parse<Testament>(BookTestament), aliases, translation);
        }

        public BibleChapter ToChapterEntity() => new(ChapterId, BookId, ChapterNumber, ToBookEntity());
    }

    #endregion
}