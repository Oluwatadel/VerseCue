using Versecue.Domain.Enums;
using Versecue.Domain.Exceptions;

namespace Versecue.Domain.Entities;

/// <summary>
/// Bible translation entity (e.g., KJV, NIV).
/// Aggregate root for reference data.
/// </summary>
public class BibleTranslation
{
    public int Id { get; private set; }
    public string Code { get; private set; } // e.g., "KJV", "NIV"
    public string Name { get; private set; }
    public string Language { get; private set; } // ISO code
    public string LicenseInfo { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<BibleBook> _books = [];
    public IReadOnlyCollection<BibleBook> Books => _books.AsReadOnly();

    private BibleTranslation() { } // EF Core

    public BibleTranslation(string code, string name, string language, string licenseInfo)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new BibleTranslationArgumentException($"Code required, {nameof(code)}");
        if (string.IsNullOrWhiteSpace(name)) throw new BibleTranslationArgumentException($"Name required, {nameof(name)}");
        if (string.IsNullOrWhiteSpace(language)) throw new BibleTranslationArgumentException($"Language required, {nameof(language)}");

        Code = code.ToUpperInvariant();
        Name = name;
        Language = language;
        LicenseInfo = licenseInfo ?? string.Empty;
        IsActive = true;
    }

    public BibleTranslation(int id, string code, string name, string language, string licenseInfo, bool isActive)
        : this(code, name, language, licenseInfo)
    {
        Id = id;
        IsActive = isActive;
    }


    public void AddBook(BibleBook book)
    {
        _books.Add(book);
    }

    public void SetActive(bool active)
    {
        IsActive = active;
    }
}