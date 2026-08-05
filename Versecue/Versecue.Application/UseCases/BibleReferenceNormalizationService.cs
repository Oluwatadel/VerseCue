using System.Text.RegularExpressions;
using Versecue.Application.Interfaces;
using Versecue.Domain.Entities;
using Versecue.Domain.ValueObjects;

namespace Versecue.Application.UseCases;

public sealed class BibleReferenceNormalizationService
{
    private static readonly Dictionary<string, int> SpokenNumbersMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "one", 1 }, { "first", 1 },
        { "two", 2 }, { "second", 2 },
        { "three", 3 }, { "third", 3 },
        { "four", 4 }, { "fourth", 4 },
        { "five", 5 }, { "fifth", 5 },
        { "six", 6 }, { "sixth", 6 },
        { "seven", 7 }, { "seventh", 7 },
        { "eight", 8 }, { "eighth", 8 },
        { "nine", 9 }, { "ninth", 9 },
        { "ten", 10 }, { "tenth", 10 },
        { "eleven", 11 }, { "twelfth", 12 }, { "twelve", 12 },
        { "thirteen", 13 }, { "fourteen", 14 }, { "fifteen", 15 },
        { "sixteen", 16 }, { "seventeen", 17 }, { "eighteen", 18 },
        { "nineteen", 19 }, { "twenty", 20 }, { "thirty", 30 },
        { "forty", 40 }, { "fifty", 50 }, { "sixty", 60 },
        { "seventy", 70 }, { "eighty", 80 }, { "ninety", 90 },
        { "hundred", 100 }
    };

    private static readonly Regex SpokenPattern = new(
        @"\b(chapter|verse|verses|to|through|and)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<BibleReference?> NormalizeAsync(
        string rawText,
        int translationId,
        IBibleRepository bibleRepository,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;

        // 1. Clean the text, normalize spaces
        var cleaned = Regex.Replace(rawText.Trim(), @"\s+", " ");

        // 2. Fetch all books for the translation from the repo
        var books = await bibleRepository.GetBooksByTranslationAsync(translationId, ct);
        if (books.Count == 0) return null;

        // 3. Try to extract book name and digits
        // We match: Optional Book Prefix (1st, 1, One, I) + Book Name + Numbers
        // E.g. "1 Corinthians 13:4-7", "Genesis chapter 1 verse 1", "John 3 16"

        // Find if any book alias matches a prefix of our cleaned text
        BibleBook? matchedBook = null;
        string remainingText = "";

        // Sort books by name length descending so longer aliases match first (e.g. "1 Corinthians" before "1 Cor")
        var sortedBooks = books.OrderByDescending(b => b.Name.Length).ToList();

        foreach (var book in sortedBooks)
        {
            var aliases = book.GetAliases();
            // Also include book's canonical name itself
            var allNames = aliases.Concat(new[] { book.Name }).Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in allNames)
            {
                if (cleaned.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure the matched name is followed by a space, digit, or end of string
                    var endIdx = name.Length;
                    if (cleaned.Length == endIdx || char.IsWhiteSpace(cleaned[endIdx]) || char.IsDigit(cleaned[endIdx]))
                    {
                        matchedBook = book;
                        remainingText = cleaned.Substring(endIdx).Trim();
                        break;
                    }
                }
            }
            if (matchedBook != null) break;
        }

        if (matchedBook == null) return null;

        // 4. Parse the remaining text for Chapter and Verses
        // Replace spoken indicators like "chapter", "verse", "through" to simplify parsing
        remainingText = SpokenPattern.Replace(remainingText, " ");
        remainingText = Regex.Replace(remainingText, @"\s+", " ");

        // Convert any spoken numbers to digits
        remainingText = ConvertSpokenNumbersToDigits(remainingText);

        // Find all digits in the remaining text
        var digitMatches = Regex.Matches(remainingText, @"\d+");
        if (digitMatches.Count == 0) return null;

        var numbers = digitMatches.Cast<Match>().Select(m => int.Parse(m.Value)).ToList();

        int chapter = numbers[0];
        int? verseStart = null;
        int? verseEnd = null;

        if (numbers.Count > 1)
        {
            verseStart = numbers[1];
        }
        if (numbers.Count > 2)
        {
            verseEnd = numbers[2];
        }
        else if (verseStart.HasValue)
        {
            verseEnd = verseStart; // Default single verse
        }

        // Validate the reference ranges (basic check, detailed validation is database-dependent)
        if (chapter <= 0) return null;
        if (verseStart.HasValue && verseStart.Value <= 0) return null;
        if (verseEnd.HasValue && verseEnd.Value < verseStart.Value) return null;

        return new BibleReference(matchedBook.Id, chapter, verseStart, verseEnd);
    }

    private string ConvertSpokenNumbersToDigits(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (SpokenNumbersMap.TryGetValue(word, out int val))
            {
                // Simple parser for combined spoken numbers like "twenty three" -> 23
                if (i + 1 < words.Length && SpokenNumbersMap.TryGetValue(words[i + 1], out int nextVal) && val >= 20 && val < 100 && nextVal < 10)
                {
                    result.Add((val + nextVal).ToString());
                    i++; // skip next
                }
                else
                {
                    result.Add(val.ToString());
                }
            }
            else
            {
                result.Add(word);
            }
        }

        return string.Join(" ", result);
    }
}
