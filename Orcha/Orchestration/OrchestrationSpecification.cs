
namespace Orcha.Orchestration;

public class OrchestrationSpecification
{
    public required string SchemaVersion { get; set; }

    public required string InstanceIdPrefix { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public Dictionary<string, string>? Meta { get; set; }
    public required List<Stage> Stages { get; set; }
}
