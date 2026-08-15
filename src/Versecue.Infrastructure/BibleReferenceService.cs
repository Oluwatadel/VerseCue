using Versecue.Application.Bible;
using Versecue.Application.Interfaces;

namespace Versecue.Infrastructure.Services;

using System.Text.RegularExpressions;

public sealed class BibleReferenceService : IBibleReferenceService
{
    private static readonly Regex ParseRegex = new(
        @"^\s*(?<book>[1-3]?\s*[A-Za-z]+(?:\s+[A-Za-z]+)?)\s+(?<chapter>\d+)\s*:\s*(?<verseStart>\d+)(?:\s*[-\u2013\u2014]\s*(?<verseEnd>\d+))?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public BibleReference? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var match = ParseRegex.Match(input);
        if (!match.Success)
            return null;

        var bookName = match.Groups["book"].Value.Trim();
        if (!int.TryParse(match.Groups["chapter"].Value, out var chapter))
            return null;

        if (!int.TryParse(match.Groups["verseStart"].Value, out var verseStart))
            return null;

        int? verseEnd = null;
        if (match.Groups["verseEnd"].Success && int.TryParse(match.Groups["verseEnd"].Value, out var parsedEnd))
        {
            verseEnd = parsedEnd;
        }

        return new BibleReference(bookName, chapter, verseStart, verseEnd);
    }

    private static readonly Dictionary<string, string>
        BookAliases = new()
        {
            ["gen"] = "Genesis",
            ["genesis"] = "Genesis",

            ["ex"] = "Exodus",
            ["exo"] = "Exodus",
            ["exodus"] = "Exodus",

            ["lev"] = "Leviticus",
            ["leviticus"] = "Leviticus",

            ["num"] = "Numbers",
            ["numbers"] = "Numbers",

            ["deut"] = "Deuteronomy",
            ["deuteronomy"] = "Deuteronomy",

            ["josh"] = "Joshua",
            ["joshua"] = "Joshua",

            ["judg"] = "Judges",
            ["judges"] = "Judges",

            ["ruth"] = "Ruth",

            ["1 sam"] = "1 Samuel",
            ["1sam"] = "1 Samuel",
            ["1 samuel"] = "1 Samuel",

            ["2 sam"] = "2 Samuel",
            ["2sam"] = "2 Samuel",
            ["2 samuel"] = "2 Samuel",

            ["1 kings"] = "1 Kings",
            ["1king"] = "1 Kings",
            ["1kings"] = "1 Kings",

            ["2 kings"] = "2 Kings",
            ["2king"] = "2 Kings",
            ["2kings"] = "2 Kings",

            ["1 chron"] = "1 Chronicles",
            ["1 chronicles"] = "1 Chronicles",

            ["2 chron"] = "2 Chronicles",
            ["2 chronicles"] = "2 Chronicles",

            ["ezra"] = "Ezra",

            ["neh"] = "Nehemiah",
            ["nehemiah"] = "Nehemiah",

            ["esth"] = "Esther",
            ["esther"] = "Esther",

            ["job"] = "Job",

            ["ps"] = "Psalms",
            ["psalm"] = "Psalms",
            ["psalms"] = "Psalms",

            ["prov"] = "Proverbs",
            ["proverbs"] = "Proverbs",

            ["eccl"] = "Ecclesiastes",
            ["ecclesiastes"] = "Ecclesiastes",

            ["song"] = "Song of Solomon",
            ["song of solomon"] = "Song of Solomon",

            ["isa"] = "Isaiah",
            ["isaiah"] = "Isaiah",

            ["jer"] = "Jeremiah",
            ["jeremiah"] = "Jeremiah",

            ["lam"] = "Lamentations",
            ["lamentations"] = "Lamentations",

            ["ezek"] = "Ezekiel",
            ["ezekiel"] = "Ezekiel",

            ["dan"] = "Daniel",
            ["daniel"] = "Daniel",

            ["hos"] = "Hosea",
            ["hosea"] = "Hosea",

            ["joel"] = "Joel",
            ["amos"] = "Amos",
            ["obad"] = "Obadiah",
            ["obadiah"] = "Obadiah",
            ["jonah"] = "Jonah",
            ["mic"] = "Micah",
            ["micah"] = "Micah",
            ["nah"] = "Nahum",
            ["nahum"] = "Nahum",
            ["hab"] = "Habakkuk",
            ["habakkuk"] = "Habakkuk",
            ["zeph"] = "Zephaniah",
            ["zephaniah"] = "Zephaniah",
            ["hag"] = "Haggai",
            ["haggai"] = "Haggai",
            ["zech"] = "Zechariah",
            ["zechariah"] = "Zechariah",
            ["mal"] = "Malachi",
            ["malachi"] = "Malachi",

            ["matt"] = "Matthew",
            ["matthew"] = "Matthew",

            ["mark"] = "Mark",
            ["mk"] = "Mark",

            ["luke"] = "Luke",
            ["lk"] = "Luke",

            ["john"] = "John",
            ["jn"] = "John",

            ["acts"] = "Acts",

            ["rom"] = "Romans",
            ["romans"] = "Romans",

            ["1 cor"] = "1 Corinthians",
            ["1 corinthians"] = "1 Corinthians",

            ["2 cor"] = "2 Corinthians",
            ["2 corinthians"] = "2 Corinthians",

            ["gal"] = "Galatians",
            ["galatians"] = "Galatians",

            ["eph"] = "Ephesians",
            ["ephesians"] = "Ephesians",

            ["phil"] = "Philippians",
            ["philippians"] = "Philippians",

            ["col"] = "Colossians",
            ["colossians"] = "Colossians",

            ["1 thess"] = "1 Thessalonians",
            ["1 thessalonians"] = "1 Thessalonians",

            ["2 thess"] = "2 Thessalonians",
            ["2 thessalonians"] = "2 Thessalonians",

            ["1 tim"] = "1 Timothy",
            ["1 timothy"] = "1 Timothy",

            ["2 tim"] = "2 Timothy",
            ["2 timothy"] = "2 Timothy",

            ["titus"] = "Titus",
            ["philem"] = "Philemon",
            ["philemon"] = "Philemon",

            ["heb"] = "Hebrews",
            ["hebrews"] = "Hebrews",

            ["james"] = "James",
            ["jas"] = "James",

            ["1 pet"] = "1 Peter",
            ["1 peter"] = "1 Peter",

            ["2 pet"] = "2 Peter",
            ["2 peter"] = "2 Peter",

            ["1 john"] = "1 John",
            ["1jn"] = "1 John",

            ["2 john"] = "2 John",
            ["2jn"] = "2 John",

            ["3 john"] = "3 John",
            ["3jn"] = "3 John",

            ["jude"] = "Jude",

            ["rev"] = "Revelation",
            ["revelation"] = "Revelation"
        };

    public Task<BibleReference?> NormalizeAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (reference is null)
            return Task.FromResult<BibleReference?>(null);

        if (reference.ChapterNumber <= 0)
            return Task.FromResult<BibleReference?>(null);

        if (reference.VerseStart <= 0)
            return Task.FromResult<BibleReference?>(null);

        if (reference.VerseEnd.HasValue &&
            reference.VerseEnd.Value < reference.VerseStart)
        {
            return Task.FromResult<BibleReference?>(null);
        }

        var bookKey =
            reference.BookName
                .Trim()
                .ToLowerInvariant();

        if (!BookAliases.TryGetValue(
                bookKey,
                out var canonicalBook))
        {
            return Task.FromResult<BibleReference?>(null);
        }

        var normalized =
            new BibleReference(
                canonicalBook,
                reference.ChapterNumber,
                reference.VerseStart,
                reference.VerseEnd);

        return Task.FromResult<BibleReference?>(
            normalized);
    }
}