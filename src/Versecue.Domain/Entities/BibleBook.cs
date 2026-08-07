using System.Text.Json;
using System.Text.Json.Serialization;
using Versecue.Domain.Enums;
using Versecue.Domain.Exceptions;

namespace Versecue.Domain.Entities;

/// <summary>
/// Bible book entity (e.g., Genesis, Romans).
/// Contains aliases for normalization (e.g., "Rom", "Romans", "Rom.").
/// </summary>
public class BibleBook
{
    public int Id { get; private set; }
    public int TranslationId { get; private set; }
    public int CanonicalOrder { get; private set; } // 1-66
    public string Name { get; private set; }
    public Testament Testament { get; private set; }
    public string AliasesJson { get; private set; } // JSON array of aliases

    // Navigation
    public BibleTranslation? Translation { get; private set; }
    private readonly List<BibleChapter> _chapters = [];
    public IReadOnlyCollection<BibleChapter> Chapters => _chapters.AsReadOnly();

    private BibleBook() { } // EF Core

    public BibleBook(int translationId, int canonicalOrder, string name, Testament testament, IEnumerable<string> aliases, BibleTranslation translation)
    {
        if (canonicalOrder < 1 || canonicalOrder > 66)
            throw new CononicalOutOfRangeException($"{nameof(canonicalOrder)}, Must be 1-66");
        if (string.IsNullOrWhiteSpace(name))
            throw new BibleBookArgumentException($"Name required, {nameof(name)}");

        TranslationId = translationId;
        CanonicalOrder = canonicalOrder;
        Name = name;
        Testament = testament;
        AliasesJson = JsonSerializer.Serialize((aliases ?? Array.Empty<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToArray());
        Translation = translation;
    }

    public BibleBook(int id, int translationId, int canonicalOrder, string name, Testament testament, IEnumerable<string> aliases, BibleTranslation translation)
        : this(translationId, canonicalOrder, name, testament, aliases, translation)
    {
        Id = id;
    }


    public IReadOnlyList<string> GetAliases() =>
        string.IsNullOrEmpty(AliasesJson)
            ? []
            : JsonSerializer.Deserialize<string[]>(AliasesJson) ?? [];

    public void AddChapter(BibleChapter chapter) => _chapters.Add(chapter);

    public bool MatchesAlias(string input) =>
        GetAliases().Any(a => a.Equals(input, StringComparison.OrdinalIgnoreCase));
}