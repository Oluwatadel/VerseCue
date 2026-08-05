using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Versecue.Application.Interfaces;
using Versecue.Application.UseCases;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;

namespace Versecue.Application.Tests;

public class BibleReferenceNormalizationTests
{
    [Fact]
    public async Task NormalizeAsync_StandardReference_ReturnsCorrectBibleReference()
    {
        // Arrange
        var mockRepo = new MockBibleRepository();
        var normalizer = new BibleReferenceNormalizationService();

        // Act
        var result = await normalizer.NormalizeAsync("John 3:16", 1, mockRepo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(43, result.Value.BookId);
        Assert.Equal(3, result.Value.ChapterNumber);
        Assert.Equal(16, result.Value.VerseStart);
        Assert.Equal(16, result.Value.VerseEnd);
    }

    [Fact]
    public async Task NormalizeAsync_SpokenNumberWords_ReturnsCorrectBibleReference()
    {
        // Arrange
        var mockRepo = new MockBibleRepository();
        var normalizer = new BibleReferenceNormalizationService();

        // Act
        var result = await normalizer.NormalizeAsync("Genesis chapter one verse two", 1, mockRepo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Value.BookId);
        Assert.Equal(1, result.Value.ChapterNumber);
        Assert.Equal(2, result.Value.VerseStart);
        Assert.Equal(2, result.Value.VerseEnd);
    }

    [Fact]
    public async Task NormalizeAsync_RangeReference_ReturnsCorrectBibleReference()
    {
        // Arrange
        var mockRepo = new MockBibleRepository();
        var normalizer = new BibleReferenceNormalizationService();

        // Act
        var result = await normalizer.NormalizeAsync("Romans 8:28-30", 1, mockRepo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(45, result.Value.BookId);
        Assert.Equal(8, result.Value.ChapterNumber);
        Assert.Equal(28, result.Value.VerseStart);
        Assert.Equal(30, result.Value.VerseEnd);
    }
}

public class MockBibleRepository : IBibleRepository
{
    private readonly List<BibleBook> _books;

    public MockBibleRepository()
    {
        var translation = new BibleTranslation(1, "KJV", "King James", "en", "", true);
        _books = new List<BibleBook>
        {
            new BibleBook(1, 1, 1, "Genesis", Testament.Old, new[] { "Gen", "Genesis" }, translation),
            new BibleBook(43, 1, 43, "John", Testament.New, new[] { "Jn", "John", "Jhn" }, translation),
            new BibleBook(45, 1, 45, "Romans", Testament.New, new[] { "Rom", "Romans" }, translation)
        };
    }

    public Task<IReadOnlyList<BibleBook>> GetBooksByTranslationAsync(int translationId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<BibleBook>>(_books);
    }

    public Task<BibleBook?> GetBookByAliasAsync(int translationId, string alias, CancellationToken ct = default)
    {
        var book = _books.FirstOrDefault(b => b.Name.Equals(alias, StringComparison.OrdinalIgnoreCase) || b.MatchesAlias(alias));
        return Task.FromResult(book);
    }

    // Unused interface stubs
    public Task<BibleTranslation?> GetTranslationByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<BibleTranslation?> GetTranslationByCodeAsync(string code, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<BibleTranslation>> GetActiveTranslationsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<BibleBook?> GetBookByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<BibleChapter?> GetChapterAsync(int bookId, int chapterNumber, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<BibleChapter>> GetChaptersByBookAsync(int bookId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<BibleVerse?> GetVerseAsync(int chapterId, int verseNumber, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<BibleVerse>> GetVersesAsync(int chapterId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<BibleVerse>> GetVerseRangeAsync(int chapterId, int verseStart, int verseEnd, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<string?> GetPassageTextAsync(BibleReference reference, int translationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<(BibleVerse Verse, BibleBook Book, BibleChapter Chapter)>> SearchVersesAsync(int translationId, string query, int limit = 50, CancellationToken ct = default) => throw new NotImplementedException();
}