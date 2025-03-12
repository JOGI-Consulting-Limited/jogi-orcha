namespace Orcha.Orchestration;

public class Stage
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string State { get; set; }
    public bool ContinueOnError { get; set; }
    public required List<Job> Jobs { get; set; }
    public int TimeoutMinutes { get; set; } = 15;
    public WaitForEvent? WaitForEvent { get; set; }
}
