using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Orcha.Orchestration;

public static class SubOrchestrator
{
    [Function(nameof(SubOrchestrator))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Create replay safe logger
        ILogger log = context.CreateReplaySafeLogger(nameof(Orchestrator));

        // Get job specification
        var spec = context.GetInput<JobSpecification>() ?? throw new InvalidOperationException("JobSpecification cannot be null");
        var job = spec.Job;

        log.LogInformation("{CorrelationId}: SubOrchestrator handling request for: '{JobName}'.", spec.CorrelationId, job.Name);
        log.LogInformation("{CorrelationId}: Job sub job count: '{JobCount}'.", spec.CorrelationId, job.Jobs?.Count ?? 0);

        // Run individual jobs concurrently (children)
        var subJobs = new List<Task>();

        job.Jobs?.ForEach((j) =>
            {
                // Create the job's spec
                var jobSpec = new JobSpecification(j, spec.Meta, spec.CorrelationId);

                // Build retry options
                var retryPolicy = new RetryPolicy(
                    firstRetryInterval: TimeSpan.FromSeconds(j.RetryTimeoutSeconds),
                    maxNumberOfAttempts: j.MaxRetryCount);

                var taskRetryOptions = new TaskRetryOptions(retryPolicy);

                var taskOptions = new TaskOptions(taskRetryOptions);

                if (j.Jobs?.Count > 0)
                {
                    // Nested Sub-orchestrations
                    log.LogInformation("{CorrelationId}: Adding SubOrchestration for job: '{JobName}'.", spec.CorrelationId, j.Name);
                    var instanceId = $"{context.InstanceId}-{context.NewGuid()}";
                    subJobs.Add(context.CallSubOrchestratorAsync<string>("SubOrchestrator", instanceId, taskOptions));
                }
                else
                {
                    subJobs.Add(context.CallActivityAsync(j.Function, jobSpec, taskOptions));
                }
            });

        context.SetCustomStatus($"{spec.CorrelationId}: Waiting for {subJobs.Count} jobs to complete.");

        // Wait for all to finish
        await Task.WhenAll(subJobs);

        context.SetCustomStatus($"{spec.CorrelationId}: All jobs complete.");

        // Now call the function associated with the parent job
        var jobSpec = new JobSpecification(job, spec.Meta, spec.CorrelationId);
        await context.CallActivityAsync<string>(job.Function, jobSpec);

        return $"{spec.CorrelationId}: Sub-orchestration Complete";
    }
}