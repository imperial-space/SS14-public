namespace Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;

/// <summary>
/// Результат однозначного разбора одной физической строки тетради.
/// </summary>
public readonly record struct DeathNoteParsedLine(
    string TargetName,
    string ExecutionTime,
    string CauseText);
