using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;

namespace Versecue.Domain.Entities;

/// <summary>
/// Aggregate root for a sermon session.
/// Owns TranscriptSegments and DetectedReferences.
/// </summary>
public class SermonSession
{
    public Guid Id { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public SermonSessionState State { get; private set; }
    public int TranslationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<TranscriptSegment> _transcriptSegments = [];
    private readonly List<DetectedReference> _detectedReferences = [];

    public IReadOnlyCollection<TranscriptSegment> TranscriptSegments => _transcriptSegments.AsReadOnly();
    public IReadOnlyCollection<DetectedReference> DetectedReferences => _detectedReferences.AsReadOnly();

    private SermonSession() { } // EF Core

    public SermonSession(int translationId)
    {
        Id = Guid.NewGuid();
        StartedAt = DateTime.UtcNow;
        State = SermonSessionState.Idle;
        TranslationId = translationId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        if (State != SermonSessionState.Idle)
            throw new InvalidOperationException($"Cannot start session in state {State}");
        State = SermonSessionState.Listening;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Pause()
    {
        if (State != SermonSessionState.Listening && State != SermonSessionState.Transcribing && State != SermonSessionState.Detecting)
            throw new InvalidOperationException($"Cannot pause session in state {State}");
        State = SermonSessionState.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Resume()
    {
        if (State != SermonSessionState.Paused)
            throw new InvalidOperationException($"Cannot resume session in state {State}");
        State = SermonSessionState.Listening;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (State == SermonSessionState.Completed || State == SermonSessionState.Error)
            throw new InvalidOperationException($"Session already in terminal state {State}");
        EndedAt = DateTime.UtcNow;
        State = SermonSessionState.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetError()
    {
        State = SermonSessionState.Error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddTranscriptSegment(TranscriptSegment segment)
    {
        if (State == SermonSessionState.Completed || State == SermonSessionState.Error)
            throw new InvalidOperationException("Cannot add transcript to completed/error session");
        _transcriptSegments.Add(segment);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddDetectedReference(DetectedReference reference)
    {
        if (State == SermonSessionState.Completed || State == SermonSessionState.Error)
            throw new InvalidOperationException("Cannot add detection to completed/error session");
        _detectedReferences.Add(reference);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetState(SermonSessionState newState)
    {
        State = newState;
        UpdatedAt = DateTime.UtcNow;
    }
}