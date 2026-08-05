using Versecue.Domain.Enums;

namespace Versecue.Domain.ValueObjects;

/// <summary>
/// Value object representing a confidence score with its detection source.
/// </summary>
public readonly record struct ConfidenceScore
{
    public double Value { get; init; } // 0.0 - 1.0
    public DetectionSource Source { get; init; }

    public ConfidenceScore(double value, DetectionSource source)
    {
        if (value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence must be between 0.0 and 1.0");
        Value = value;
        Source = source;
    }

    public bool IsHighConfidence(double threshold = 0.8) => Value >= threshold;
    public bool IsLowConfidence(double threshold = 0.5) => Value < threshold;

    public override string ToString() => $"{Value:P0} ({Source})";
}