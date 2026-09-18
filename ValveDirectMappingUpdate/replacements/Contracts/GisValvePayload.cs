using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaValveInspectionGisMaximo.Contracts;

public sealed class GisValvePayload
{
    [JsonPropertyName("eventTimestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? EventTimestamp { get; init; }

    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Action { get; init; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Status { get; init; }

    [JsonPropertyName("globalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? GlobalId { get; init; }

    [JsonPropertyName("assetId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? AssetId { get; init; }

    [JsonPropertyName("lifeCycleStatus")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? LifeCycleStatus { get; init; }

    [JsonPropertyName("physicalValveId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? PhysicalValveId { get; init; }

    [JsonPropertyName("serviceTerritoryName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ServiceTerritoryName { get; init; }

    [JsonPropertyName("installationDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? InstallationDate { get; init; }

    [JsonPropertyName("classification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Classification { get; init; }

    [JsonPropertyName("regulatoryType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? RegulatoryType { get; init; }

    [JsonPropertyName("isCriticalValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsCriticalValve { get; init; }

    [JsonPropertyName("isSectionalizingValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsSectionalizingValve { get; init; }

    [JsonPropertyName("isEGOMPValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsEgompValve { get; init; }

    [JsonPropertyName("isInterconnectValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsInterconnectValve { get; init; }

    [JsonPropertyName("isAutomaticShutoffValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsAutomaticShutoffValve { get; init; }

    [JsonPropertyName("isRemoteOperatedValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsRemoteOperatedValve { get; init; }

    [JsonPropertyName("isRuptureMitigationValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsRuptureMitigationValve { get; init; }

    [JsonPropertyName("isLowPressureValve")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? IsLowPressureValve { get; init; }

    [JsonPropertyName("crossingType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? CrossingType { get; init; }

    [JsonPropertyName("accessType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? AccessType { get; init; }

    [JsonPropertyName("normalPosition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? NormalPosition { get; init; }

    [JsonPropertyName("subSystemName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? SubSystemName { get; init; }

    [JsonPropertyName("subSystemPressure")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? SubSystemPressure { get; init; }

    [JsonPropertyName("lifecycleEndDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? LifecycleEndDate { get; init; }

    [JsonPropertyName("serviceAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ValveServiceAddress[]? ServiceAddress { get; init; }
}
