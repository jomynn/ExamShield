namespace ExamShield.Domain.ValueObjects;

public sealed class OcrConfidence : IEquatable<OcrConfidence>
{
    public const double LowThreshold = 0.85;
    public double Value { get; }

    public OcrConfidence(double value)
    {
        if (value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence must be between 0.0 and 1.0.");
        Value = value;
    }

    public bool IsLow => Value < LowThreshold;

    public bool Equals(OcrConfidence? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as OcrConfidence);
    public override int GetHashCode() => Value.GetHashCode();
}
