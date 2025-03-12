namespace Orcha.Orchestration;

public class JobSpecification
{
    public JobSpecification(Job job, Dictionary<string, string>? meta, string correlationId)
    {
        this.Job = job;
        this.Meta = meta;
        this.CorrelationId = correlationId;
    }

    public Dictionary<string, string>? Meta { get; private set; }
    public Job Job { get; private set; }

    public string CorrelationId { get; private set; }
}