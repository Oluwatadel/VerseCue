namespace Versecue.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a Bible conical range is out of bounds.
/// </summary>
public class CononicalOutOfRangeException : DomainException
{
    public CononicalOutOfRangeException(string message) : base(message) { }
    public CononicalOutOfRangeException(string message, Exception inner) : base(message, inner) { }
}


public class NegativeBibleChapterException : DomainException
{
    public NegativeBibleChapterException(string message) : base(message) { }
    public NegativeBibleChapterException(string message, Exception inner) : base(message, inner) { }
}

public class BibleTranslationArgumentException : DomainException
{
    public BibleTranslationArgumentException(string message) : base(message) { }
    public BibleTranslationArgumentException(string message, Exception inner) : base(message, inner) { }
}

public class BibleVerseArgumentException : DomainException
{
    public BibleVerseArgumentException(string message) : base(message) { }
    public BibleVerseArgumentException(string message, Exception inner) : base(message, inner) { }
}

public class BibleBookArgumentException : DomainException
{
    public BibleBookArgumentException(string message) : base(message) { }
    public BibleBookArgumentException(string message, Exception inner) : base(message, inner) { }
}


/// <summary>
/// Thrown when a Bible reference cannot be parsed or normalized.
/// </summary>
public class BibleReferenceParseException : DomainException
{
    public string RawInput { get; }

    public BibleReferenceParseException(string rawInput, string message)
        : base($"Failed to parse Bible reference '{rawInput}': {message}")
    {
        RawInput = rawInput;
    }
}

/// <summary>
/// Thrown when a Bible reference fails validation against the database.
/// </summary>
public class BibleReferenceValidationException : DomainException
{
    public BibleReferenceParseException? ParseException { get; }

    public BibleReferenceValidationException(string message) : base(message) { }
    public BibleReferenceValidationException(string message, BibleReferenceParseException parseEx)
        : base(message, parseEx)
    {
        ParseException = parseEx;
    }
}

/// <summary>
/// Thrown when an entity state transition is invalid.
/// </summary>
public class InvalidStateTransitionException : DomainException
{
    public string EntityType { get; }
    public string CurrentState { get; }
    public string AttemptedState { get; }

    public InvalidStateTransitionException(string entityType, string currentState, string attemptedState)
        : base($"{entityType}: Cannot transition from {currentState} to {attemptedState}")
    {
        EntityType = entityType;
        CurrentState = currentState;
        AttemptedState = attemptedState;
    }
}

/// <summary>
/// Thrown when a required Bible translation is not available.
/// </summary>
public class TranslationNotFoundException : DomainException
{
    public int TranslationId { get; }
    public string? TranslationCode { get; }

    public TranslationNotFoundException(int translationId)
        : base($"Bible translation with ID {translationId} not found")
    {
        TranslationId = translationId;
    }

    public TranslationNotFoundException(string code)
        : base($"Bible translation with code '{code}' not found")
    {
        TranslationCode = code;
    }
}

/// <summary>
/// Thrown when audio device operations fail.
/// </summary>
public class AudioDeviceException : DomainException
{
    public string DeviceId { get; }

    public AudioDeviceException(string deviceId, string message)
        : base($"Audio device '{deviceId}': {message}")
    {
        DeviceId = deviceId;
    }
}

/// <summary>
/// Thrown when STT operations fail.
/// </summary>
public class SttException : DomainException
{
    public SttException(string message) : base(message) { }
    public SttException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when LLM operations fail.
/// </summary>
public class LlmException : DomainException
{
    public LlmException(string message) : base(message) { }
    public LlmException(string message, Exception inner) : base(message, inner) { }
}