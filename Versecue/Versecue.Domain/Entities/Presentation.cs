using Versecue.Domain.ValueObjects;

namespace Versecue.Domain.Entities;

/// <summary>
/// Current presentation state - what's on the projector.
/// MVP: single current-state row per session.
/// </summary>
public class Presentation
{
    public Guid Id { get; private set; }
    public Guid SermonSessionId { get; private set; }
    public Guid? DetectedReferenceId { get; private set; } // Currently displayed reference
    public string DisplayDeviceId { get; private set; }
    public bool IsVisible { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation
    public SermonSession? SermonSession { get; private set; }
    public DetectedReference? DetectedReference { get; private set; }

    private Presentation() { } // EF Core

    public Presentation(Guid sermonSessionId, string displayDeviceId)
    {
        if (string.IsNullOrWhiteSpace(displayDeviceId))
            throw new ArgumentException("DisplayDeviceId required", nameof(displayDeviceId));

        Id = Guid.NewGuid();
        SermonSessionId = sermonSessionId;
        DisplayDeviceId = displayDeviceId;
        IsVisible = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Show(Guid detectedReferenceId)
    {
        DetectedReferenceId = detectedReferenceId;
        IsVisible = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Hide()
    {
        IsVisible = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDisplay(string displayDeviceId)
    {
        DisplayDeviceId = displayDeviceId;
        UpdatedAt = DateTime.UtcNow;
    }
}