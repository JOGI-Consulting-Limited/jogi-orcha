namespace Orcha.Orchestration;

public class Job
{
    public required string Name { get; set; }
    public required string Function { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public List<Job>? Jobs { get; set; }
    public int MaxRetryCount { get; set; } = 1;
    public int RetryTimeoutSeconds { get; set; } = 10;
}