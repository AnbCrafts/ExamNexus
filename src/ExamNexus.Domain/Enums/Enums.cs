namespace ExamNexus.Domain.Enums;

public enum TenantPlan
{
    Free = 0,
    Professional = 1,
    Enterprise = 2
}

public enum QuestionType
{
    MultipleChoice = 0,
    TrueFalse = 1,
    ShortAnswer = 2
}

public enum ExamSessionStatus
{
    NotStarted = 0,
    InProgress = 1,
    Submitted = 2,
    AutoSubmittedTimeout = 3,
    FlaggedCheating = 4
}

public enum ProctorSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}
