namespace ExamShield.Domain.ValueObjects;

public sealed record ReviewRequestId : GuidId
{
    public ReviewRequestId(Guid value) : base(value) { }
    public static ReviewRequestId New() => new(Guid.NewGuid());
}
