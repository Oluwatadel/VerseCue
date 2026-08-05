namespace Versecue.Domain.Enums;

/// <summary>
/// State of a sermon session.
/// </summary>
public enum SermonSessionState
{
    Idle,
    Listening,
    Transcribing,
    Detecting,
    AwaitingApproval,
    Displaying,
    Paused,
    Completed,
    Error
}

/// <summary>
/// State of a detected reference through its lifecycle.
/// </summary>
public enum DetectedReferenceState
{
    Detected,
    Processing,
    Resolved,
    Validated,
    PendingApproval,
    Approved,
    Rejected,
    Displayed
}

/// <summary>
/// Source of the detection/resolution.
/// </summary>
public enum DetectionSource
{
    Deterministic,
    AIAssisted
}

/// <summary>
/// Operator approval action.
/// </summary>
public enum ApprovalAction
{
    Display,
    Ignore,
    Edit
}

/// <summary>
/// Testament (Old or New).
/// </summary>
public enum Testament
{
    Old,
    New
}