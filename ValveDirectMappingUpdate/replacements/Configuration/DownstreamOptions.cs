namespace GaValveInspectionGisMaximo.Configuration;

public sealed class DownstreamOptions
{
    public string MaximoHost { get; init; } = string.Empty;
    public string MaximoValveInspectionPath { get; init; } = string.Empty;
    public string MaximoBasicAuth { get; init; } = string.Empty;
    public int MaximoTimeoutSeconds { get; init; } = 60;

    public string GisHost { get; init; } = string.Empty;
    public string GisValveInspectionPath { get; init; } = string.Empty;
    public string GisTokenPath { get; init; } = string.Empty;
    public string GisUsername { get; init; } = string.Empty;
    public string GisPassword { get; init; } = string.Empty;
    public string GisReferer { get; init; } = string.Empty;
    public string GisTokenClient { get; init; } = "requestip";
    public int GisTokenExpirationMinutes { get; init; } = 60;
    public int GisTokenRefreshSkewMinutes { get; init; } = 5;
    public int GisTimeoutSeconds { get; init; } = 60;
    public string GisPayloadParameterName { get; init; } = "eventPayload";
    public int GisTokenMaxAttempts { get; init; } = 3;
    public int GisTokenFirstDelaySeconds { get; init; } = 5;
    public double GisTokenBackoffCoefficient { get; init; } = 2.0;
    public int GisTokenMaxDelaySeconds { get; init; } = 30;
    public int GisTokenRetryTimeoutSeconds { get; init; } = 120;

    public int MaxAttempts { get; init; } = 4;
    public int FirstDelaySeconds { get; init; } = 2;
    public double BackoffCoefficient { get; init; } = 2;
    public int MaxDelaySeconds { get; init; } = 30;
    public int RetryTimeoutSeconds { get; init; } = 180;
}
