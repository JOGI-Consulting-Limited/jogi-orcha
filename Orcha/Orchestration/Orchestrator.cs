using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Orcha.Orchestration;

public static class Orchestrator
{
    [Function(nameof(Orchestrator))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Get model specification
        var specification = context.GetInput<OrchestrationSpecification>();

        if (specification == null)
        {
            throw new InvalidOperationException(nameof(specification));
        }

        // Create replay safe logger
        ILogger log = context.CreateReplaySafeLogger(nameof(Orchestrator));

        log.LogInformation("{InstanceId}: Orchestrator handling request for: '{SpecificationName}'.", context.InstanceId, specification.Name);
        log.LogInformation("{InstanceId}: Specification Stage count: '{StageCount}'.", context.InstanceId, specification.Stages.Count);

        var isManuallyCancelled = false;

        // Run stages sequentially
        foreach (var stage in specification.Stages)
        {
            // Set-up waiting for an event if needed
            if (stage.WaitForEvent != null)
            {
                using (var waitCts = new CancellationTokenSource())
                {
                    // Set up the timer
                    var waitTime = context.CurrentUtcDateTime.AddHours(stage.WaitForEvent.TimeoutHours);
                    var waitExpired = context.CreateTimer(waitTime, waitCts.Token);

                    log.LogInformation("{InstanceId}: Waiting for event: {EventName}.", context.InstanceId, stage.WaitForEvent.EventName);

                    // Wait for the expected event
                    var waitEvent = context.WaitForExternalEvent<EventResponse>(stage.WaitForEvent.EventName);
                    context.SetCustomStatus($"Waiting for event: {stage.WaitForEvent.EventName}");

                    // Did we get the event, or did the timer fire
                    if (waitEvent == await Task.WhenAny(waitEvent, waitExpired))
                    {
                        log.LogInformation("{InstanceId}: Received event: {EventName}. Continuing...", context.InstanceId, stage.WaitForEvent.EventName);

                        // Cancel the timer
                        await waitCts.CancelAsync();

                        // What did the event instruct us to do
                        if (waitEvent.Result == EventResponse.Continue)
                        {
                            // We got the continue event, let the orchestration continue
                            await BuildStage(context, log, specification, stage);
                        }
                        else
                        {
                            // We have a cancel...
                            log.LogWarning("{InstanceId}: Cancelling orchestration, event: {EventName} set to 'Cancel'.", context.InstanceId, stage.WaitForEvent.EventName);
                            context.SetCustomStatus($"Cancelled by user issuing event: {stage.WaitForEvent.EventName} with 'Cancel'");
                            isManuallyCancelled = true;
                            break;
                        }
                    }
                    else
                    {
                        log.LogWarning("{InstanceId}: Time expired waiting for event: {EventName}.", context.InstanceId, stage.WaitForEvent.EventName);

                        // Wait expired - fail or continue based on user's mandate
                        if (stage.WaitForEvent.TimeoutAction == WaitForEventAction.ContinueOrchestration)
                        {
                            log.LogWarning("{InstanceId}: Continuing with orchestration, 'WaitForEvent.TimeoutAction' set to 'ContinueOrchestration'.", context.InstanceId);

                            // let the orchestration continue
                            await BuildStage(context, log, specification, stage);
                        }
                        else
                        {
                            // Terminate it
                            throw new TimeoutException($"{context.InstanceId}: Time expired waiting for event: {stage.WaitForEvent.EventName}. Terminating orchestration.");
                        }
                    }
                }
            }
            else
            {
                await BuildStage(context, log, specification, stage);
            }
        }

        if (!isManuallyCancelled)
        {
            context.SetCustomStatus("complete");
        }

        return $"{context.InstanceId}: Completed run of: {specification.Name}";
    }

    private static async Task BuildStage(TaskOrchestrationContext context, ILogger log, OrchestrationSpecification specification, Stage stage)
    {
        using var cts = new CancellationTokenSource();

        // Configure Stage timeout
        var timeoutAt = context.CurrentUtcDateTime.AddMinutes(stage.TimeoutMinutes);
        var stageTimeoutTask = context.CreateTimer(timeoutAt, cts.Token);

        // Run individual jobs concurrently
        var jobs = new List<Task>();

        try
        {
            stage.Jobs.ForEach((j) =>
            {
                // Build retry options
                var retryPolicy = new RetryPolicy(
                    firstRetryInterval: TimeSpan.FromSeconds(j.RetryTimeoutSeconds),
                    maxNumberOfAttempts: j.MaxRetryCount);

                var taskRetryOptions = new TaskRetryOptions(retryPolicy);

                var taskOptions = new TaskOptions(taskRetryOptions);

                // Create the job's spec (cascade the top-level metadata down to the individual jobs)
                var jobSpec = new JobSpecification(j, specification.Meta, context.InstanceId);

                if (j.Jobs?.Count > 0)
                {
                    // Sub-orchestration
                    log.LogInformation("{InstanceId}: Adding SubOrchestration for job: '{JobName}'.", context.InstanceId, j.Name);
                    var instanceId = $"{context.InstanceId}-{context.NewGuid()}";
                    jobs.Add(context.CallSubOrchestratorAsync<string>("SubOrchestrator", instanceId, taskOptions));
                }
                else
                {
                    // Create job and add to list
                    log.LogInformation("{InstanceId}: Adding job: '{JobName}'.", context.InstanceId, j.Name);
                    jobs.Add(context.CallActivityAsync(j.Function, jobSpec, taskOptions));
                }
            });

            // Set orchestration state
            context.SetCustomStatus(stage.State);

            // Wait for all to finish or stage to timeout
            var stageTask = Task.WhenAll(jobs);
            var winner = await Task.WhenAny(stageTask, stageTimeoutTask);

            // Check if a timeout occurred
            if (winner == stageTask)
            {
                log.LogInformation("{InstanceId}: Stage complete: '{StageName}'.", context.InstanceId, stage.Name);
                await cts.CancelAsync();
            }
            else
            {
                // Stage has timed out
                log.LogWarning("{InstanceId}: TIMEOUT for stage: {StageName}", context.InstanceId, stage.Name);
                throw new TimeoutException($"{context.InstanceId}: TIMEOUT for stage: {stage.Name}");
            }
        }
        catch (Exception ex)
        {
            if (stage.ContinueOnError)
            {
                log.LogWarning(ex, "{InstanceId}: ContinuingOnError for stage: {StageName}. Continuing orchestration. Stage non-fatal error, downgrading to warning: {Exception}",
                    context.InstanceId, stage.Name, ex.Message);
            }
            else
            {
                log.LogError(ex, "{InstanceId}: Fatal Error for stage: {StageName}. Terminating orchestration. Stage fatal error: {Exception}",
                    context.InstanceId, stage.Name, ex.Message);

                context.SetCustomStatus("failed");
                throw;
            }
        }
    }
}
