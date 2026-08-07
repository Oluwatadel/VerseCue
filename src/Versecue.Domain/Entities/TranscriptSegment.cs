using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;

namespace Versecue.Domain.Entities;

/// <summary>
/// A timestamped segment of transcribed text from the STT engine.
/// </summary>
public class TranscriptSegment
{
    public Guid Id { get; private set; }
    public Guid SermonSessionId { get; private set; }
    public string Text { get; private set; }
    public long StartOffsetMs { get; private set; }
    public long EndOffsetMs { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public SermonSession? SermonSession { get; private set; }

    private TranscriptSegment() { } // EF Core

    public TranscriptSegment(Guid sermonSessionId, string text, long startOffsetMs, long endOffsetMs)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Transcript text cannot be empty", nameof(text));
        if (startOffsetMs < 0) throw new ArgumentException("StartOffsetMs cannot be negative", nameof(startOffsetMs));
        if (endOffsetMs < startOffsetMs) throw new ArgumentException("EndOffsetMs must be >= StartOffsetMs", nameof(endOffsetMs));

        Id = Guid.NewGuid();
        SermonSessionId = sermonSessionId;
        Text = text.Trim();
        StartOffsetMs = startOffsetMs;
        EndOffsetMs = endOffsetMs;
        CreatedAt = DateTime.UtcNow;
    }

    public TimeSpan Duration => TimeSpan.FromMilliseconds(EndOffsetMs - StartOffsetMs);
    public TimeSpan StartOffset => TimeSpan.FromMilliseconds(StartOffsetMs);
    public TimeSpan EndOffset => TimeSpan.FromMilliseconds(EndOffsetMs);
}