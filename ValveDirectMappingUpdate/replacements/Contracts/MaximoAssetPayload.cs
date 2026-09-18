using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaValveInspectionGisMaximo.Contracts;

public sealed class MaximoAssetPayload
{
    [JsonPropertyName("NG_GUID")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? NgGuid { get; init; }

    [JsonPropertyName("STATUS")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Status { get; init; }

    [JsonPropertyName("ASSETNUM")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? AssetNum { get; init; }

    [JsonPropertyName("ASSETTAG")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? AssetTag { get; init; }

    [JsonPropertyName("NG_SERVTERRITORY")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ServiceTerritory { get; init; }

    [JsonPropertyName("HIERARCHYPATH")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? HierarchyPath { get; init; }

    [JsonPropertyName("INSTALLDATE")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? InstallDate { get; init; }

    [JsonPropertyName("SERVICEADDRESS")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaximoServiceAddress[]? ServiceAddress { get; init; }

    [JsonPropertyName("ASSETSPEC")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaximoAssetSpecification[]? AssetSpec { get; init; }
}

public sealed class MaximoServiceAddress
{
    [JsonPropertyName("STREETADDRESS")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? StreetAddress { get; init; }

    [JsonPropertyName("CITY")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? City { get; init; }

    [JsonPropertyName("STATEPROVINCE")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? StateProvince { get; init; }

    [JsonPropertyName("POSTALCODE")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? PostalCode { get; init; }
}

public sealed class MaximoAssetSpecification
{
    [JsonPropertyName("ALNVALUE")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? AlnValue { get; init; }

    [JsonPropertyName("DATEVALUE")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? DateValue { get; init; }

    [JsonPropertyName("ASSETATTRID")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetAttributeId { get; init; }
}
