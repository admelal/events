namespace GaValveInspectionGisMaximo.Contracts;

public sealed record WorkflowRetryPolicy(
    int MaxAttempts,
    int FirstDelaySeconds,
    double BackoffCoefficient,
    int MaxDelaySeconds,
    int TimeoutSeconds);

public sealed record ValveInspectionMessage(
    string MessageType,
    string CorrelationId,
    DateTimeOffset ReceivedAtUtc,
    ValveInspectionEvent Payload,
    WorkflowRetryPolicy? RetryPolicy = null);

public sealed record MaximoSendCommand(
    string CorrelationId,
    string? Action,
    MaximoAssetPayload Payload);

public sealed record GisSendCommand(
    string CorrelationId,
    string? Action,
    GisValvePayload Payload);

public sealed record IntegrationResult(
    string Status,
    string MessageType,
    string CorrelationId,
    string? Action,
    string Target,
    int HttpStatusCode,
    DateTimeOffset CompletedAtUtc);
