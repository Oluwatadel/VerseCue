using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Versecue.Application.Interfaces;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;

namespace Versecue.Application.UseCases;

public sealed class BibleReferenceDetectionService
{
    private readonly IBibleRepository _bibleRepository;
    private readonly BibleReferenceNormalizationService _normalizer;
    private readonly ILlmService _llmService;

    // A broad regex pattern to extract potential book name followed by numbers/phrases
    private static readonly Regex PotentialRefRegex = new(
        @"\b(?:[123]\s+|I{1,3}\s+)?([A-Za-z]+)\s+(?:chapter\s+)?\d+[\s\w:-]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public BibleReferenceDetectionService(
        IBibleRepository bibleRepository,
        BibleReferenceNormalizationService normalizer,
        ILlmService llmService)
    {
        _bibleRepository = bibleRepository;
        _normalizer = normalizer;
        _llmService = llmService;
    }

    public async Task<IReadOnlyList<DetectedReference>> DetectReferencesAsync(
        Guid sessionId,
        Guid segmentId,
        string transcriptText,
        int translationId,
        double aiThreshold = 0.7,
        CancellationToken ct = default)
    {
        var results = new List<DetectedReference>();
        if (string.IsNullOrWhiteSpace(transcriptText)) return results;

        // 1. Enumerate potential matches in the transcript text using Regex
        var matches = PotentialRefRegex.Matches(transcriptText);

        foreach (Match match in matches)
        {
            var matchText = match.Value;

            // 2. Try deterministic parsing and normalization
            var resolvedRef = await _normalizer.NormalizeAsync(matchText, translationId, _bibleRepository, ct);
            
            if (resolvedRef.HasValue)
            {
                // Validate against the database
                var isValid = await ValidateReferenceAsync(resolvedRef.Value, translationId, ct);
                if (isValid)
                {
                    var detRef = new DetectedReference(sessionId, matchText, DetectionSource.Deterministic, 1.0, segmentId);
                    detRef.Resolve(resolvedRef.Value.BookId, resolvedRef.Value.ChapterNumber, resolvedRef.Value.VerseStart, resolvedRef.Value.VerseEnd);
                    detRef.Validate();
                    detRef.SubmitForApproval();
                    results.Add(detRef);
                    continue; // Deterministic match succeeded
                }
            }

            // 3. Fallback to LLM if deterministic confidence is low/fails but it looks like a reference
            if (aiThreshold < 1.0)
            {
                var llmRes = await _llmService.ResolveReferenceAsync(matchText, transcriptText, ct);
                if (llmRes.IsSuccess && llmRes.BookName != null && llmRes.ChapterNumber.HasValue)
                {
                    // Find the book by name/alias
                    var book = await _bibleRepository.GetBookByAliasAsync(translationId, llmRes.BookName, ct);
                    if (book != null)
                    {
                        var tempRef = new BibleReference(book.Id, llmRes.ChapterNumber.Value, llmRes.VerseStart, llmRes.VerseEnd);
                        var isValid = await ValidateReferenceAsync(tempRef, translationId, ct);
                        if (isValid)
                        {
                            var detRef = new DetectedReference(sessionId, matchText, DetectionSource.AIAssisted, llmRes.Confidence, segmentId);
                            detRef.Resolve(book.Id, llmRes.ChapterNumber.Value, llmRes.VerseStart, llmRes.VerseEnd);
                            detRef.Validate();
                            detRef.SubmitForApproval();
                            results.Add(detRef);
                        }
                    }
                }
            }
        }

        return results;
    }

    private async Task<bool> ValidateReferenceAsync(BibleReference reference, int translationId, CancellationToken ct)
    {
        try
        {
            // Retrieve the chapter to check count
            var chapter = await _bibleRepository.GetChapterAsync(reference.BookId, reference.ChapterNumber, ct);
            if (chapter == null) return false;

            if (reference.VerseStart.HasValue)
            {
                // Check if the verse exists in this chapter
                var verse = await _bibleRepository.GetVerseAsync(chapter.Id, reference.VerseStart.Value, ct);
                if (verse == null) return false;
            }

            if (reference.VerseEnd.HasValue)
            {
                var verse = await _bibleRepository.GetVerseAsync(chapter.Id, reference.VerseEnd.Value, ct);
                if (verse == null) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
