namespace GaValveInspectionGisMaximo.Configuration;

public sealed class ValveIntegrationOptions
{
    public const string SectionName = "ValveInspection";

    public ServiceBusOptions ServiceBus { get; init; } = new();
    public RequestOptions Request { get; init; } = new();
}

public sealed class ServiceBusOptions
{
    public string FullyQualifiedNamespace { get; init; } = string.Empty;
    public string TopicName { get; init; } = string.Empty;
    public string TransportType { get; init; } = "AmqpWebSockets";
    public int MessageTimeToLiveMinutes { get; init; } = 1_440;
}

public sealed class RequestOptions
{
    public int MaxBodyBytes { get; init; } = 262_144;
}
