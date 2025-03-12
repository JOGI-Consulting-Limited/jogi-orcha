using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orcha.Orchestration;

namespace Orcha.Function.Core;

public static class EchoJobName
{
    /// <summary>
    /// Simple job which just echos the job name.
    /// </summary>
    /// <param name="js">JobSpecification</param>
    /// <param name="log">ILogger</param>
    /// <returns>Task</returns>
    [Function(nameof(EchoJobName))]
    public static async Task<string> Run([ActivityTrigger] JobSpecification js, FunctionContext executionContext)
    {
        ILogger log = executionContext.GetLogger(nameof(EchoJobName));
        log.LogInformation("Saying hello from: {JobName}.", js.Job.Name);
        await Task.Delay(100);
        return $"Hello {js.Job.Name}!";
    }
}
