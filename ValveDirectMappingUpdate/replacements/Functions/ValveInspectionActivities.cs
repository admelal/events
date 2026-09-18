using System.Text.Json;
using GaValveInspectionGisMaximo.Contracts;
using GaValveInspectionGisMaximo.Serialization;
using GaValveInspectionGisMaximo.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
namespace GaValveInspectionGisMaximo.Functions;

public sealed class ValveInspectionActivities
{
    private readonly IGisToMaximoMapper _gisToMaximoMapper;
    private readonly IMaximoToGisMapper _maximoToGisMapper;
    private readonly IMaximoClient _maximoClient;
    private readonly IArcGisClient _arcGisClient;
    private readonly ILogger<ValveInspectionActivities> _logger;
    public ValveInspectionActivities(
        IGisToMaximoMapper gisToMaximoMapper,
        IMaximoToGisMapper maximoToGisMapper,
        IMaximoClient maximoClient,
        IArcGisClient arcGisClient,
        ILogger<ValveInspectionActivities> logger)
    {
        ArgumentNullException.ThrowIfNull(gisToMaximoMapper);
        ArgumentNullException.ThrowIfNull(maximoToGisMapper);
        ArgumentNullException.ThrowIfNull(maximoClient);
        ArgumentNullException.ThrowIfNull(arcGisClient);
        ArgumentNullException.ThrowIfNull(logger);
        _gisToMaximoMapper = gisToMaximoMapper;
        _maximoToGisMapper = maximoToGisMapper;
        _maximoClient = maximoClient;
        _arcGisClient = arcGisClient;
        _logger = logger;
    }
    [Function(FunctionNames.TransformGisToMaximo)]
    public MaximoSendCommand TransformGisToMaximo(
        [ActivityTrigger] ValveInspectionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Payload);
        var action = MapperValue.ForLog(message.Payload.Action);
        try
        {
            _logger.LogInformation(
                "Starting GIS-to-Maximo payload transformation. " +
                "MessageType={MessageType}, Action={Action}, " +
                "CorrelationId={CorrelationId}, InputPayload={InputPayload}",
                message.MessageType,
                action,
                message.CorrelationId,
                SerializePayload(message.Payload));
            var transformedPayload =
                _gisToMaximoMapper.Map(message.Payload);
            _logger.LogInformation(
                "Completed GIS-to-Maximo payload transformation. " +
                "Action={Action}, CorrelationId={CorrelationId}, " +
                "OutputPayload={OutputPayload}",
                action,
                message.CorrelationId,
                SerializePayload(transformedPayload));
            return new MaximoSendCommand(
                CorrelationId: message.CorrelationId,
                Action: action,
                Payload: transformedPayload);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "GIS-to-Maximo payload transformation failed. " +
                "MessageType={MessageType}, Action={Action}, " +
                "CorrelationId={CorrelationId}",
                message.MessageType,
                action,
                message.CorrelationId);
            throw;
        }
    }
    [Function(FunctionNames.TransformMaximoToGis)]
    public GisSendCommand TransformMaximoToGis(
        [ActivityTrigger] ValveInspectionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Payload);
        var action = MapperValue.ForLog(message.Payload.Action);
        try
        {
            _logger.LogInformation(
                "Starting Maximo-to-GIS payload transformation. " +
                "MessageType={MessageType}, Action={Action}, " +
                "CorrelationId={CorrelationId}, InputPayload={InputPayload}",
                message.MessageType,
                action,
                message.CorrelationId,
                SerializePayload(message.Payload));
            var transformedPayload =
                _maximoToGisMapper.Map(message.Payload);
            _logger.LogInformation(
                "Completed Maximo-to-GIS payload transformation. " +
                "Action={Action}, CorrelationId={CorrelationId}, " +
                "OutputPayload={OutputPayload}",
                action,
                message.CorrelationId,
                SerializePayload(transformedPayload));
            return new GisSendCommand(
                CorrelationId: message.CorrelationId,
                Action: action,
                Payload: transformedPayload);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Maximo-to-GIS payload transformation failed. " +
                "MessageType={MessageType}, Action={Action}, " +
                "CorrelationId={CorrelationId}",
                message.MessageType,
                action,
                message.CorrelationId);
            throw;
        }
    }
    [Function(FunctionNames.SendToMaximo)]
    public async Task<IntegrationResult> SendToMaximoAsync(
        [ActivityTrigger] MaximoSendCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Payload);
        try
        {
            _logger.LogInformation(
                "Sending payload to Maximo. Action={Action}, " +
                "CorrelationId={CorrelationId}, Payload={Payload}",
                command.Action,
                command.CorrelationId,
                SerializePayload(command.Payload));
            var statusCode = await _maximoClient
                .SendAsync(command, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Sent payload to Maximo. Action={Action}, " +
                "CorrelationId={CorrelationId}, " +
                "HttpStatusCode={HttpStatusCode}",
                command.Action,
                command.CorrelationId,
                statusCode);
            return new IntegrationResult(
                Status: "Completed",
                MessageType: MessageTypes.GisToMaximo,
                CorrelationId: command.CorrelationId,
                Action: command.Action,
                Target: "Maximo",
                HttpStatusCode: statusCode,
                CompletedAtUtc: DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Maximo operation was cancelled. Action={Action}, " +
                "CorrelationId={CorrelationId}",
                command.Action,
                command.CorrelationId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Sending payload to Maximo failed. Action={Action}, " +
                "CorrelationId={CorrelationId}",
                command.Action,
                command.CorrelationId);
            throw;
        }
    }
    [Function(FunctionNames.SendToGis)]
    public async Task<IntegrationResult> SendToGisAsync(
        [ActivityTrigger] GisSendCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Payload);
        try
        {
            _logger.LogInformation(
                "Sending payload to ArcGIS. Action={Action}, " +
                "CorrelationId={CorrelationId}, Payload={Payload}",
                command.Action,
                command.CorrelationId,
                SerializePayload(command.Payload));
            var statusCode = await _arcGisClient
                .SendAsync(command, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Sent payload to ArcGIS. Action={Action}, " +
                "CorrelationId={CorrelationId}, " +
                "HttpStatusCode={HttpStatusCode}",
                command.Action,
                command.CorrelationId,
                statusCode);
            return new IntegrationResult(
                Status: "Completed",
                MessageType: MessageTypes.MaximoToGis,
                CorrelationId: command.CorrelationId,
                Action: command.Action,
                Target: "ArcGIS",
                HttpStatusCode: statusCode,
                CompletedAtUtc: DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "ArcGIS operation was cancelled. Action={Action}, " +
                "CorrelationId={CorrelationId}",
                command.Action,
                command.CorrelationId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Sending payload to ArcGIS failed. Action={Action}, " +
                "CorrelationId={CorrelationId}",
                command.Action,
                command.CorrelationId);
            throw;
        }
    }
    private static string SerializePayload<T>(T payload)
    {
        return JsonSerializer.Serialize(
            payload,
            JsonDefaults.Options);
    }
}
