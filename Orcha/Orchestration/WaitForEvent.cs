namespace Orcha.Orchestration;

public partial class WaitForEvent
{
    public required string EventName { get; set; }
    public int TimeoutHours { get; set; }
    public WaitForEventAction TimeoutAction { get; set; }
}