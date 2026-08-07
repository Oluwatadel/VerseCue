using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;

namespace Versecue.Domain.Entities;

/// <summary>
/// A detected Bible reference from a transcript segment.
/// Goes through lifecycle: Detected -> Processing -> Resolved -> Validated -> PendingApproval -> Approved/Rejected -> Displayed
/// </summary>
public class DetectedReference
{
    public Guid Id { get; private set; }
    public Guid SermonSessionId { get; private set; }
    public Guid? TranscriptSegmentId { get; private set; } // Null for manually-initiated references
    public string RawText { get; private set; } // As detected, pre-normalization
    public int? BookId { get; private set; } // Populated once resolved
    public int? ChapterNumber { get; private set; }
    public int? VerseStart { get; private set; }
    public int? VerseEnd { get; private set; }
    public double ConfidenceScore { get; private set; } // 0.0 - 1.0
    public DetectionSource DetectionSource { get; private set; }
    public DetectedReferenceState State { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation
    public SermonSession? SermonSession { get; private set; }
    public TranscriptSegment? TranscriptSegment { get; private set; }

    private DetectedReference() { } // EF Core

    public DetectedReference(
        Guid sermonSessionId,
        string rawText,
        DetectionSource source,
        double confidenceScore,
        Guid? transcriptSegmentId = null)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new ArgumentException("RawText cannot be empty", nameof(rawText));
        if (confidenceScore < 0.0 || confidenceScore > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence must be between 0.0 and 1.0");

        Id = Guid.NewGuid();
        SermonSessionId = sermonSessionId;
        TranscriptSegmentId = transcriptSegmentId;
        RawText = rawText.Trim();
        DetectionSource = source;
        ConfidenceScore = confidenceScore;
        State = DetectedReferenceState.Detected;
        DetectedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public BibleReference? ResolvedReference =>
        BookId.HasValue && ChapterNumber.HasValue
            ? new BibleReference(BookId.Value, ChapterNumber.Value, VerseStart, VerseEnd)
            : null;

    public void Resolve(int bookId, int chapterNumber, int? verseStart = null, int? verseEnd = null)
    {
        if (State != DetectedReferenceState.Detected && State != DetectedReferenceState.Processing)
            throw new InvalidOperationException($"Cannot resolve reference in state {State}");

        BookId = bookId;
        ChapterNumber = chapterNumber;
        VerseStart = verseStart;
        VerseEnd = verseEnd;
        State = DetectedReferenceState.Resolved;
        ResolvedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Validate()
    {
        if (State != DetectedReferenceState.Resolved)
            throw new InvalidOperationException($"Cannot validate reference in state {State}");
        if (!ResolvedReference.HasValue)
            throw new InvalidOperationException("Cannot validate unresolved reference");

        State = DetectedReferenceState.Validated;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SubmitForApproval()
    {
        if (State != DetectedReferenceState.Validated)
            throw new InvalidOperationException($"Cannot submit for approval in state {State}");

        State = DetectedReferenceState.PendingApproval;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        if (State != DetectedReferenceState.PendingApproval)
            throw new InvalidOperationException($"Cannot approve reference in state {State}");

        State = DetectedReferenceState.Approved;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (State != DetectedReferenceState.PendingApproval)
            throw new InvalidOperationException($"Cannot reject reference in state {State}");

        State = DetectedReferenceState.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDisplayed()
    {
        if (State != DetectedReferenceState.Approved)
            throw new InvalidOperationException($"Cannot mark displayed in state {State}");

        State = DetectedReferenceState.Displayed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateConfidence(double newConfidence)
    {
        if (newConfidence < 0.0 || newConfidence > 1.0)
            throw new ArgumentOutOfRangeException(nameof(newConfidence));
        ConfidenceScore = newConfidence;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsResolved => ResolvedReference.HasValue;
    public bool IsPendingApproval => State == DetectedReferenceState.PendingApproval;
    public bool IsApproved => State == DetectedReferenceState.Approved;
    public bool IsDisplayed => State == DetectedReferenceState.Displayed;
}