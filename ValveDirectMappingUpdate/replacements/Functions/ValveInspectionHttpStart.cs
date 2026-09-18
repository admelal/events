using System.Net;
using System.Text.Json;
using GaValveInspectionGisMaximo.Configuration;
using GaValveInspectionGisMaximo.Contracts;
using GaValveInspectionGisMaximo.Serialization;
using GaValveInspectionGisMaximo.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaValveInspectionGisMaximo.Functions;

public sealed class ValveInspectionHttpStart
{
    private const string MessageTypeHeader = "messageType";
    private const string SourceHeader = "ng_source";
    private const string TargetHeader = "ng_target";
    private const string CorrelationHeader = "x-correlation-id";
    private readonly DownstreamOptions _options;
    private readonly ILogger<ValveInspectionHttpStart> _logger;

    public ValveInspectionHttpStart(
        IOptions<DownstreamOptions> options,
        ILogger<ValveInspectionHttpStart> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    [Function(FunctionNames.GisToMaximoHttpStart)]
    public Task<HttpResponseData> StartGisToMaximoAsync(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "valve-inspections/gis-to-maximo")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return StartAsync(
            request,
            durableClient,
            MessageTypes.GisToMaximo,
            expectedSource: "GIS",
            expectedTarget: "Maximo",
            FunctionNames.GisToMaximoOrchestrator,
            cancellationToken);
    }

    [Function(FunctionNames.MaximoToGisHttpStart)]
    public Task<HttpResponseData> StartMaximoToGisAsync(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "valve-inspections/maximo-to-gis")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        return StartAsync(
            request,
            durableClient,
            MessageTypes.MaximoToGis,
            expectedSource: "Maximo",
            expectedTarget: "GIS",
            FunctionNames.MaximoToGisOrchestrator,
            cancellationToken);
    }

    private async Task<HttpResponseData> StartAsync(
        HttpRequestData request,
        DurableTaskClient durableClient,
        string expectedMessageType,
        string expectedSource,
        string expectedTarget,
        string orchestratorName,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(request);

        if (!TryGetHeader(
                request,
                MessageTypeHeader,
                out var messageType) ||
            !TryGetHeader(
                request,
                SourceHeader,
                out var source) ||
            !TryGetHeader(
                request,
                TargetHeader,
                out var target))
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "MISSING_ROUTING_HEADERS",
                "messageType, ng_source and ng_target are required.",
                correlationId,
                cancellationToken);
        }

        if (!string.Equals(
                messageType,
                expectedMessageType,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                source,
                expectedSource,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                target,
                expectedTarget,
                StringComparison.OrdinalIgnoreCase))
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "INVALID_ROUTING_HEADERS",
                "The routing headers do not match the requested direction.",
                correlationId,
                cancellationToken);
        }

        ValveInspectionEvent? payload;

        try
        {
            payload =
                await JsonSerializer.DeserializeAsync<ValveInspectionEvent>(
                    request.Body,
                    JsonDefaults.Options,
                    cancellationToken);
        }
        catch (JsonException)
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "INVALID_JSON",
                "The request body is not valid JSON.",
                correlationId,
                cancellationToken);
        }

        if (payload is null)
        {
            return await CreateErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "EMPTY_PAYLOAD",
                "A valve inspection payload is required.",
                correlationId,
                cancellationToken);
        }

        var orchestrationMessage = new ValveInspectionMessage(
            MessageType: expectedMessageType,
            CorrelationId: correlationId,
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            Payload: payload,
            RetryPolicy: CreateRetryPolicy());

        var instanceId =
            await durableClient
                .ScheduleNewOrchestrationInstanceAsync(
                    orchestratorName,
                    orchestrationMessage,
                    cancellationToken);

        _logger.LogInformation(
            "Started HTTP valve orchestration. " +
            "InstanceId={InstanceId}, MessageType={MessageType}, " +
            "Source={Source}, Target={Target}, " +
            "CorrelationId={CorrelationId}",
            instanceId,
            expectedMessageType,
            expectedSource,
            expectedTarget,
            correlationId);

        var response =
            request.CreateResponse(HttpStatusCode.Accepted);

        response.Headers.Add(
            "Content-Type",
            "application/json; charset=utf-8");

        response.Headers.Add(
            CorrelationHeader,
            correlationId);

        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                new
                {
                    status = "Accepted",
                    instanceId,
                    messageType = expectedMessageType,
                    source = expectedSource,
                    target = expectedTarget,
                    correlationId
                },
                JsonDefaults.Options),
            cancellationToken);

        return response;
    }

    private WorkflowRetryPolicy CreateRetryPolicy()
    {
        return new WorkflowRetryPolicy(
            MaxAttempts: _options.MaxAttempts,
            FirstDelaySeconds: _options.FirstDelaySeconds,
            BackoffCoefficient: _options.BackoffCoefficient,
            MaxDelaySeconds: _options.MaxDelaySeconds,
            TimeoutSeconds: _options.RetryTimeoutSeconds);
    }

    private static bool TryGetHeader(
        HttpRequestData request,
        string headerName,
        out string value)
    {
        value = string.Empty;

        if (!request.Headers.TryGetValues(
                headerName,
                out var values))
        {
            return false;
        }

        var configuredValue =
            values.FirstOrDefault()?.Trim();

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return false;
        }

        value = configuredValue;
        return true;
    }

    private static string GetCorrelationId(
        HttpRequestData request)
    {
        if (TryGetHeader(
                request,
                CorrelationHeader,
                out var correlationId) &&
            correlationId.Length <= 128 &&
            correlationId.All(character =>
                char.IsLetterOrDigit(character) ||
                character is '-' or '_' or '.' or ':'))
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString("D");
    }

    private static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string code,
        string message,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);

        response.Headers.Add(
            "Content-Type",
            "application/json; charset=utf-8");

        response.Headers.Add(
            CorrelationHeader,
            correlationId);

        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                new
                {
                    code,
                    message,
                    correlationId
                },
                JsonDefaults.Options),
            cancellationToken);

        return response;
    }
}
