using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orcha.Orchestration;

namespace Orcha.Function.Core;

public static class Delay
{
    /// <summary>
    /// Simple job which just adds a delay.  The delay value is in ms and provided by the job's param dictionary.
    /// </summary>
    /// <param name="js">JobSpecification</param>
    /// <param name="log">ILogger</param>
    /// <returns>Task</returns>
    [Function(nameof(Delay))]
    public static async Task Run([ActivityTrigger] JobSpecification js, FunctionContext executionContext)
    {
        ILogger log = executionContext.GetLogger(nameof(Delay));

        // Get the delay
        var delay = 0;

        if (js.Job != null && js.Job.Parameters != null && js.Job.Parameters.TryGetValue("DelayMilliseconds", out string? value))
        {
            delay = Int32.Parse(value);

            log.LogInformation("[JOB: {JobName}] - Adding delay of: {Delay} ms.", js.Job.Name, delay);

        }
        else
        {
            log.LogWarning("[JOB: {JobName}] - No delay parameter found in Job.Parameters, please specify using `DelayMilliseconds` as key.", js.Job?.Name);
        }

        // Add the delay
        await Task.Delay(delay);

    }
}
