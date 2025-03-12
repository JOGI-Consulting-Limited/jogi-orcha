using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orcha.Orchestration;

namespace Orcha.Function.Core;

public static class Skip
{
    /// <summary>
    /// Does nothing.  Useful when initially building model spec structure when you don't 
    /// want to actually do any work.
    /// </summary>
    /// <param name="js">JobSpecification</param>
    /// <param name="log">ILogger</param>
    /// <returns>Task</returns>
    [Function(nameof(Skip))]
    public static void Run([ActivityTrigger] JobSpecification js, FunctionContext executionContext)
    {
        ILogger log = executionContext.GetLogger(nameof(Skip));

        log.LogInformation("Running: {JobName}.", js.Job.Name);
    }
}
