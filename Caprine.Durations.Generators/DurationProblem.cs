namespace Caprine.Durations.Generators;

internal readonly struct DurationProblem
{
    internal string Id      { get; }
    internal string Message { get; }

    internal DurationProblem(string id, string message)
    {
        Id      = id;
        Message = message;
    }
}
