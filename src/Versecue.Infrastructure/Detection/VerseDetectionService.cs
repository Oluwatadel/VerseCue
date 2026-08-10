using System.Text.RegularExpressions;
using Versecue.Application.Bible;
using Versecue.Application.Interfaces;

namespace Versecue.Infrastructure.Services;

public sealed class VerseDetectionService : IVerseDetectionService
{
    private static readonly Regex ReferenceRegex =
        new(
            @"\b(?<book>[1-3]?\s?[A-Za-z]+(?:\s+[A-Za-z]+)?)\s+" +
            @"(?<chapter>\d+):(?<verseStart>\d+)" +
            @"(?:-(?<verseEnd>\d+))?\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<BibleReference?> DetectReferenceAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult<BibleReference?>(null);

        var match = ReferenceRegex.Match(text);

        if (!match.Success)
            return Task.FromResult<BibleReference?>(null);

        var bookName = match.Groups["book"].Value.Trim();

        if (!int.TryParse(
                match.Groups["chapter"].Value,
                out var chapter))
        {
            return Task.FromResult<BibleReference?>(null);
        }

        if (!int.TryParse(
                match.Groups["verseStart"].Value,
                out var verseStart))
        {
            return Task.FromResult<BibleReference?>(null);
        }

        int? verseEnd = null;

        if (match.Groups["verseEnd"].Success &&
            int.TryParse(
                match.Groups["verseEnd"].Value,
                out var parsedVerseEnd))
        {
            verseEnd = parsedVerseEnd;
        }

        var reference = new BibleReference(
            bookName,
            chapter,
            verseStart,
            verseEnd);

        return Task.FromResult<BibleReference?>(reference);
    }
}