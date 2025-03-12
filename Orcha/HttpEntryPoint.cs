using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask.Client;
using Orcha.Orchestration;
using System.Net;
using System.Text.Json;
using Microsoft.DurableTask;

namespace Orcha;

public static class HttpEntryPoint
{
    [Function(nameof(HttpEntryPoint))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "POST")] HttpRequestData req,
        [DurableClient] DurableTaskClient starter,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(HttpEntryPoint));

        // Read the request body
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var specification = JsonSerializer.Deserialize<OrchestrationSpecification>(requestBody);

        if (specification == null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid request payload.");
            return badResponse;
        }

        logger.LogInformation("Received request to run = '{SpecificationName}'. Forwarding request to Orchestrator.", specification.Name);

        // Construct a human readable id (for the UI)
        var instanceId = $"{specification.InstanceIdPrefix}-{DateTime.UtcNow.Ticks}";

        // Start the orchestration
        instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(Orchestrator), specification, new StartOrchestrationOptions(instanceId));

        logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        // It's all running now, this HTTP request is async - below provides a list of
        // URLs for monitoring the status of the model execution
        return await starter.CreateCheckStatusResponseAsync(req, instanceId);
    }
}