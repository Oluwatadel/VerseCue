using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;
using Versecue.Infrastructure.Persistence.Ef;

namespace Versecue.Infrastructure.Persistence.Import;

public sealed class ImportTranslationDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Language { get; set; } = "";
    public string LicenseInfo { get; set; } = "";
    public List<ImportBookDto> Books { get; set; } = new();
}

public sealed class ImportBookDto
{
    public string Name { get; set; } = "";
    public int CanonicalOrder { get; set; }
    public string Testament { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public List<ImportChapterDto> Chapters { get; set; } = new();
}

public sealed class ImportChapterDto
{
    public int ChapterNumber { get; set; }
    public List<ImportVerseDto> Verses { get; set; } = new();
}

public sealed class ImportVerseDto
{
    public int VerseNumber { get; set; }
    public string Text { get; set; } = "";
}

public sealed class BibleImportService
{
    private readonly VersecueDbContext _dbContext;

    public BibleImportService(VersecueDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ImportFromJsonAsync(string jsonContent, CancellationToken ct = default)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var dto = JsonSerializer.Deserialize<ImportTranslationDto>(jsonContent, options);
        if (dto == null) throw new ArgumentException("Invalid translation JSON structure.");

        await SaveImportedDtoAsync(dto, ct);
    }

    public async Task ImportFromXmlAsync(string xmlContent, CancellationToken ct = default)
    {
        var doc = XDocument.Parse(xmlContent);
        var transElement = doc.Root;
        if (transElement == null) throw new ArgumentException("Invalid XML document.");

        var dto = new ImportTranslationDto
        {
            Code = transElement.Attribute("Code")?.Value ?? transElement.Attribute("code")?.Value ?? "",
            Name = transElement.Attribute("Name")?.Value ?? transElement.Attribute("name")?.Value ?? "",
            Language = transElement.Attribute("Language")?.Value ?? transElement.Attribute("language")?.Value ?? "",
            LicenseInfo = transElement.Attribute("LicenseInfo")?.Value ?? transElement.Attribute("licenseInfo")?.Value ?? ""
        };

        if (string.IsNullOrWhiteSpace(dto.Code)) throw new ArgumentException("XML Root missing Code attribute.");

        foreach (var bookEl in transElement.Elements("Book").Concat(transElement.Elements("book")))
        {
            var bDto = new ImportBookDto
            {
                Name = bookEl.Attribute("Name")?.Value ?? bookEl.Attribute("name")?.Value ?? "",
                Testament = bookEl.Attribute("Testament")?.Value ?? bookEl.Attribute("testament")?.Value ?? "Old"
            };

            var orderStr = bookEl.Attribute("CanonicalOrder")?.Value ?? bookEl.Attribute("canonicalOrder")?.Value;
            if (int.TryParse(orderStr, out var order)) bDto.CanonicalOrder = order;

            var aliasesStr = bookEl.Attribute("Aliases")?.Value ?? bookEl.Attribute("aliases")?.Value;
            if (!string.IsNullOrEmpty(aliasesStr))
            {
                bDto.Aliases = aliasesStr.Split(',').Select(a => a.Trim()).ToList();
            }

            foreach (var chapEl in bookEl.Elements("Chapter").Concat(bookEl.Elements("chapter")))
            {
                var cDto = new ImportChapterDto();
                var chapNumStr = chapEl.Attribute("ChapterNumber")?.Value ?? chapEl.Attribute("chapterNumber")?.Value;
                if (int.TryParse(chapNumStr, out var chapNum)) cDto.ChapterNumber = chapNum;

                foreach (var verseEl in chapEl.Elements("Verse").Concat(chapEl.Elements("verse")))
                {
                    var vDto = new ImportVerseDto
                    {
                        Text = verseEl.Value
                    };
                    var vNumStr = verseEl.Attribute("VerseNumber")?.Value ?? verseEl.Attribute("verseNumber")?.Value;
                    if (int.TryParse(vNumStr, out var vNum)) vDto.VerseNumber = vNum;

                    cDto.Verses.Add(vDto);
                }
                bDto.Chapters.Add(cDto);
            }
            dto.Books.Add(bDto);
        }

        await SaveImportedDtoAsync(dto, ct);
    }

    private async Task SaveImportedDtoAsync(ImportTranslationDto dto, CancellationToken ct)
    {
        // Check if translation code exists
        var existing = _dbContext.BibleTranslations.FirstOrDefault(t => t.Code.Equals(dto.Code, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            throw new InvalidOperationException($"Translation with code '{dto.Code}' already exists in database.");
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var translation = new BibleTranslation(dto.Code, dto.Name, dto.Language, dto.LicenseInfo);
            _dbContext.BibleTranslations.Add(translation);
            await _dbContext.SaveChangesAsync(ct);

            foreach (var bDto in dto.Books)
            {
                var testament = Enum.TryParse<Testament>(bDto.Testament, true, out var tEnum) ? tEnum : Testament.Old;
                var book = new BibleBook(translation.Id, bDto.CanonicalOrder, bDto.Name, testament, bDto.Aliases, translation);
                _dbContext.BibleBooks.Add(book);
                await _dbContext.SaveChangesAsync(ct);

                foreach (var cDto in bDto.Chapters)
                {
                    var chapter = new BibleChapter(book.Id, cDto.ChapterNumber);
                    _dbContext.BibleChapters.Add(chapter);
                    await _dbContext.SaveChangesAsync(ct);

                    var verses = cDto.Verses.Select(vDto => new BibleVerse(chapter.Id, vDto.VerseNumber, vDto.Text)).ToList();
                    _dbContext.BibleVerses.AddRange(verses);
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
