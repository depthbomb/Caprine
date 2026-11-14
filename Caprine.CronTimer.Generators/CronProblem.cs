namespace Caprine.CronTimer.Generators;

internal readonly struct CronProblem
{
    internal string Id      { get; }
    internal string Message { get; }

    internal CronProblem(string id, string message)
    {
        Id      = id;
        Message = message;
    }
}
