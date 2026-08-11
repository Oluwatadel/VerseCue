using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Versecue.Application.Interfaces.Repository;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;

namespace Versecue.Infrastructure.Persistence;

public sealed class BibleImportService : IBibleImportService
{
    private readonly VersecueDbContext _db;

    public BibleImportService(VersecueDbContext db)
    {
        _db = db;
    }

    public async Task ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "Bible import file path is required.",
                nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Bible import file was not found.",
                filePath);

        var document = await ReadImportDocumentAsync(
            filePath,
            cancellationToken);

        ValidateDocument(document);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var existingTranslation =
                await _db.BibleTranslations
                    .FirstOrDefaultAsync(
                        x => x.Code == document.Code.ToUpperInvariant(),
                        cancellationToken);

            if (existingTranslation is not null)
            {
                _db.BibleTranslations.Remove(existingTranslation);

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            var translation = new BibleTranslation(
                document.Code,
                document.Name,
                document.Language,
                document.LicenseInfo,
                document.IsActive);

            foreach (var bookDocument in document.Books)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var testament = ParseTestament(
                    bookDocument.Testament,
                    bookDocument.Name);

                var book = new BibleBook(
                    bookDocument.CanonicalOrder,
                    bookDocument.Name,
                    testament,
                    bookDocument.Aliases,
                    translation);

                translation.AddBook(book);

                foreach (var chapterDocument in bookDocument.Chapters)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chapter = new BibleChapter(
                        chapterDocument.Number,
                        book);

                    book.AddChapter(chapter);

                    foreach (var verseDocument in chapterDocument.Verses)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var verse = new BibleVerse(
                            verseDocument.Number,
                            verseDocument.Text,
                            chapter);

                        chapter.AddVerse(verse);
                    }
                }
            }

            _db.BibleTranslations.Add(translation);

            await _db.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }

    private static async Task<BibleImportDocument> ReadImportDocumentAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);

        if (extension.Equals(
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(filePath);

            var document =
                await JsonSerializer.DeserializeAsync<BibleImportDocument>(
                    stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    },
                    cancellationToken);

            return document
                ?? throw new InvalidOperationException(
                    "The Bible import file is empty or invalid.");
        }

        if (extension.Equals(
                ".xml",
                StringComparison.OrdinalIgnoreCase))
        {
            return await ReadXmlImportDocumentAsync(
                filePath,
                cancellationToken);
        }

        throw new InvalidOperationException(
            "Bible import files must be JSON or XML.");
    }

    private static async Task<BibleImportDocument> ReadXmlImportDocumentAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);

        var xml = await XDocument.LoadAsync(
            stream,
            LoadOptions.None,
            cancellationToken);

        var root = xml.Root;

        if (root is null ||
            !root.Name.LocalName.Equals(
                "bible",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "XML Bible import root element must be <bible>.");
        }

        var translationName =
            (string?)root.Attribute("translation") ??
            (string?)root.Attribute("name") ??
            "Bible";

        var document = CreateXmlDocumentMetadata(
            translationName);

        foreach (var testamentElement in root
                     .Elements()
                     .Where(x => x.Name.LocalName.Equals(
                         "testament",
                         StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var testament = GetRequiredAttribute(
                testamentElement,
                "name",
                "Testament name is required.");

            foreach (var bookElement in testamentElement
                         .Elements()
                         .Where(x => x.Name.LocalName.Equals(
                             "book",
                             StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var canonicalOrder = GetRequiredIntAttribute(
                    bookElement,
                    "number",
                    "Book number is required.");

                var bookName = GetBookName(
                    canonicalOrder);

                var book = new BookImportDocument
                {
                    CanonicalOrder = canonicalOrder,
                    Name = bookName,
                    Testament = testament,
                    Aliases = [bookName]
                };

                foreach (var chapterElement in bookElement
                             .Elements()
                             .Where(x => x.Name.LocalName.Equals(
                                 "chapter",
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chapter = new ChapterImportDocument
                    {
                        Number = GetRequiredIntAttribute(
                            chapterElement,
                            "number",
                            $"Chapter number is required for book '{bookName}'.")
                    };

                    foreach (var verseElement in chapterElement
                                 .Elements()
                                 .Where(x => x.Name.LocalName.Equals(
                                     "verse",
                                     StringComparison.OrdinalIgnoreCase)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        chapter.Verses.Add(
                            new VerseImportDocument
                            {
                                Number = GetRequiredIntAttribute(
                                    verseElement,
                                    "number",
                                    $"Verse number is required for book '{bookName}', chapter {chapter.Number}."),
                                Text = NormalizeVerseText(
                                    verseElement.Value)
                            });
                    }

                    book.Chapters.Add(chapter);
                }

                document.Books.Add(book);
            }
        }

        return document;
    }

    private static BibleImportDocument CreateXmlDocumentMetadata(
        string translationName)
    {
        var name = string.IsNullOrWhiteSpace(translationName)
            ? "Bible"
            : translationName.Trim();

        var parts = name
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        var code = parts.Length > 0
            ? parts[^1]
            : name;

        var language = parts.Length > 1
            ? parts[0]
            : "English";

        return new BibleImportDocument
        {
            Code = SanitizeTranslationCode(code),
            Name = name,
            Language = language,
            LicenseInfo = string.Empty,
            IsActive = true
        };
    }

    private static string SanitizeTranslationCode(
        string value)
    {
        var code = new string(
            value
                .Where(char.IsLetterOrDigit)
                .ToArray());

        return string.IsNullOrWhiteSpace(code)
            ? "BIBLE"
            : code.ToUpperInvariant();
    }

    private static string GetRequiredAttribute(
        XElement element,
        string attributeName,
        string errorMessage)
    {
        var value = (string?)element.Attribute(attributeName);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(errorMessage);

        return value.Trim();
    }

    private static int GetRequiredIntAttribute(
        XElement element,
        string attributeName,
        string errorMessage)
    {
        var value = GetRequiredAttribute(
            element,
            attributeName,
            errorMessage);

        if (int.TryParse(value, out var number))
            return number;

        throw new InvalidOperationException(
            $"{errorMessage} Value '{value}' is not a valid number.");
    }

    private static string NormalizeVerseText(
        string value) =>
        string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static string GetBookName(
        int canonicalOrder)
    {
        if (canonicalOrder < 1 ||
            canonicalOrder > BookNames.Length)
        {
            throw new InvalidOperationException(
                $"Book number {canonicalOrder} is outside the canonical 1-66 range.");
        }

        return BookNames[canonicalOrder - 1];
    }

    private static Testament ParseTestament(
        string value,
        string bookName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Testament is required for book '{bookName}'.");
        }

        if (Enum.TryParse<Testament>(
                value,
                ignoreCase: true,
                out var testament))
        {
            return testament;
        }

        throw new InvalidOperationException(
            $"Invalid testament '{value}' for book '{bookName}'.");
    }

    private static void ValidateDocument(
        BibleImportDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Code))
            throw new InvalidOperationException(
                "Bible translation code is required.");

        if (string.IsNullOrWhiteSpace(document.Name))
            throw new InvalidOperationException(
                "Bible translation name is required.");

        if (string.IsNullOrWhiteSpace(document.Language))
            throw new InvalidOperationException(
                "Bible translation language is required.");

        if (document.Books is null ||
            document.Books.Count == 0)
        {
            throw new InvalidOperationException(
                "The Bible import contains no books.");
        }

        var expectedOrder = 1;

        foreach (var book in document.Books)
        {
            if (book.CanonicalOrder != expectedOrder)
            {
                throw new InvalidOperationException(
                    $"Expected book canonical order {expectedOrder}, " +
                    $"but '{book.Name}' has order {book.CanonicalOrder}.");
            }

            if (string.IsNullOrWhiteSpace(book.Name))
            {
                throw new InvalidOperationException(
                    $"Book {expectedOrder} has no name.");
            }

            if (book.Chapters is null ||
                book.Chapters.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Book '{book.Name}' contains no chapters.");
            }

            foreach (var chapter in book.Chapters)
            {
                if (chapter.Number <= 0)
                {
                    throw new InvalidOperationException(
                        $"Book '{book.Name}' contains an invalid chapter number.");
                }

                if (chapter.Verses is null ||
                    chapter.Verses.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Book '{book.Name}', chapter {chapter.Number} contains no verses.");
                }

                foreach (var verse in chapter.Verses)
                {
                    if (verse.Number <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Book '{book.Name}', chapter {chapter.Number} " +
                            $"contains an invalid verse number.");
                    }

                    if (string.IsNullOrWhiteSpace(verse.Text))
                    {
                        throw new InvalidOperationException(
                            $"Book '{book.Name}', chapter {chapter.Number}, " +
                            $"verse {verse.Number} contains no text.");
                    }
                }
            }

            expectedOrder++;
        }

        if (expectedOrder != 67)
        {
            throw new InvalidOperationException(
                $"A complete Bible import requires 66 books. " +
                $"The file contains {expectedOrder - 1}.");
        }
    }

    private sealed class BibleImportDocument
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Language { get; set; } = string.Empty;

        public string LicenseInfo { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public List<BookImportDocument> Books { get; set; } = [];
    }

    private sealed class BookImportDocument
    {
        public int CanonicalOrder { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Testament { get; set; } = string.Empty;

        public List<string> Aliases { get; set; } = [];

        public List<ChapterImportDocument> Chapters { get; set; } = [];
    }

    private sealed class ChapterImportDocument
    {
        public int Number { get; set; }

        public List<VerseImportDocument> Verses { get; set; } = [];
    }

    private sealed class VerseImportDocument
    {
        public int Number { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private static readonly string[] BookNames =
    [
        "Genesis",
        "Exodus",
        "Leviticus",
        "Numbers",
        "Deuteronomy",
        "Joshua",
        "Judges",
        "Ruth",
        "1 Samuel",
        "2 Samuel",
        "1 Kings",
        "2 Kings",
        "1 Chronicles",
        "2 Chronicles",
        "Ezra",
        "Nehemiah",
        "Esther",
        "Job",
        "Psalms",
        "Proverbs",
        "Ecclesiastes",
        "Song of Solomon",
        "Isaiah",
        "Jeremiah",
        "Lamentations",
        "Ezekiel",
        "Daniel",
        "Hosea",
        "Joel",
        "Amos",
        "Obadiah",
        "Jonah",
        "Micah",
        "Nahum",
        "Habakkuk",
        "Zephaniah",
        "Haggai",
        "Zechariah",
        "Malachi",
        "Matthew",
        "Mark",
        "Luke",
        "John",
        "Acts",
        "Romans",
        "1 Corinthians",
        "2 Corinthians",
        "Galatians",
        "Ephesians",
        "Philippians",
        "Colossians",
        "1 Thessalonians",
        "2 Thessalonians",
        "1 Timothy",
        "2 Timothy",
        "Titus",
        "Philemon",
        "Hebrews",
        "James",
        "1 Peter",
        "2 Peter",
        "1 John",
        "2 John",
        "3 John",
        "Jude",
        "Revelation"
    ];
}
