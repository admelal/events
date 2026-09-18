using System.Text.Json;
using GaValveInspectionGisMaximo.Contracts;

namespace GaValveInspectionGisMaximo.Services;

public interface IGisToMaximoMapper
{
    MaximoAssetPayload Map(ValveInspectionEvent source);
}

public interface IMaximoToGisMapper
{
    GisValvePayload Map(ValveInspectionEvent source);
}

public sealed class GisToMaximoMapper : IGisToMaximoMapper
{
    public MaximoAssetPayload Map(ValveInspectionEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new MaximoAssetPayload
        {
            NgGuid = MapperValue.Optional(source.GlobalId),
            Status = MapperValue.Optional(source.Status),
            AssetNum = MapperValue.Optional(source.MaximoAssetNumber),
            AssetTag = MapperValue.Optional(source.PhysicalValveId),
            ServiceTerritory = MapperValue.Optional(source.ServiceTerritoryName),
            HierarchyPath = MapperValue.Optional(source.Classification),
            InstallDate = MapperValue.Optional(source.InstallationDate),
            ServiceAddress = BuildServiceAddress(source.ServiceAddress),
            AssetSpec = BuildAssetSpecifications(source)
        };
    }

    private static MaximoServiceAddress[]? BuildServiceAddress(
        ValveServiceAddress?[]? source)
    {
        var addresses = MapperValue.Addresses(source);
        if (addresses is null)
        {
            return null;
        }

        return addresses.Select(address => new MaximoServiceAddress
        {
            StreetAddress = address.StreetAddress,
            City = address.City,
            StateProvince = address.State,
            PostalCode = address.ZipCode
        }).ToArray();
    }

    private static MaximoAssetSpecification[]? BuildAssetSpecifications(
        ValveInspectionEvent source)
    {
        var specifications = new List<MaximoAssetSpecification>(15);

        // These are target field identifiers, not event/value lookups.
        Add(specifications, "NG_ACCTYPE", source.AccessType);
        Add(specifications, "NG_AUTOSHTOFF", source.IsAutomaticShutoffValve);
        Add(specifications, "NG_CRITICAL", source.IsCriticalValve);
        Add(specifications, "NG_CROSSTYPE", source.CrossingType);
        Add(specifications, "NG_DATEOFRETIREMENT", source.LifecycleEndDate, isDate: true);
        Add(specifications, "NG_EGOMP", source.IsEgompValve);
        Add(specifications, "NG_INTERCONCT", source.IsInterconnectValve);
        Add(specifications, "NG_LOWPRS", source.IsLowPressureValve);
        Add(specifications, "NG_NMPOSITN", source.NormalPosition);
        Add(specifications, "NG_REGULTYPE", source.RegulatoryType);
        Add(specifications, "NG_REMOPERTD", source.IsRemoteOperatedValve);
        Add(specifications, "NG_RUPTUREMIT", source.IsRuptureMitigationValve);
        Add(specifications, "NG_SECTNLIZING", source.IsSectionalizingValve);
        Add(specifications, "NG_SUBSYSNM", source.SubSystemName);
        Add(specifications, "NG_SUBSYSPRS", source.SubSystemPressure);

        return specifications.Count == 0 ? null : specifications.ToArray();
    }

    private static void Add(
        ICollection<MaximoAssetSpecification> specifications,
        string attributeId,
        JsonElement? value,
        bool isDate = false)
    {
        var suppliedValue = MapperValue.Optional(value);
        if (suppliedValue is null)
        {
            return;
        }

        specifications.Add(new MaximoAssetSpecification
        {
            AssetAttributeId = attributeId,
            AlnValue = isDate ? null : suppliedValue,
            DateValue = isDate ? suppliedValue : null
        });
    }
}

public sealed class MaximoToGisMapper : IMaximoToGisMapper
{
    public GisValvePayload Map(ValveInspectionEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new GisValvePayload
        {
            EventTimestamp = MapperValue.Optional(source.EventTimestamp),
            Action = MapperValue.Optional(source.Action),
            Status = MapperValue.Optional(source.Status),
            GlobalId = MapperValue.Optional(source.GlobalId)
                ?? MapperValue.Optional(source.GlobalIdFromMaximo),
            AssetId = MapperValue.Optional(source.AssetId)
                ?? MapperValue.Optional(source.MaximoAssetNumber)
                ?? MapperValue.Optional(source.MaximoAssetNumberFromMaximo),
            LifeCycleStatus = MapperValue.Optional(source.LifeCycleStatus),
            PhysicalValveId = MapperValue.Optional(source.PhysicalValveId)
                ?? MapperValue.Optional(source.AssetTagFromMaximo),
            ServiceTerritoryName = MapperValue.Optional(source.ServiceTerritoryName),
            InstallationDate = MapperValue.Optional(source.InstallationDate),
            Classification = MapperValue.Optional(source.Classification),
            RegulatoryType = MapperValue.Optional(source.RegulatoryType),
            IsCriticalValve = MapperValue.Optional(source.IsCriticalValve),
            IsSectionalizingValve = MapperValue.Optional(source.IsSectionalizingValve),
            IsEgompValve = MapperValue.Optional(source.IsEgompValve),
            IsInterconnectValve = MapperValue.Optional(source.IsInterconnectValve),
            IsAutomaticShutoffValve = MapperValue.Optional(source.IsAutomaticShutoffValve),
            IsRemoteOperatedValve = MapperValue.Optional(source.IsRemoteOperatedValve),
            IsRuptureMitigationValve = MapperValue.Optional(source.IsRuptureMitigationValve),
            IsLowPressureValve = MapperValue.Optional(source.IsLowPressureValve),
            CrossingType = MapperValue.Optional(source.CrossingType),
            AccessType = MapperValue.Optional(source.AccessType),
            NormalPosition = MapperValue.Optional(source.NormalPosition),
            SubSystemName = MapperValue.Optional(source.SubSystemName),
            SubSystemPressure = MapperValue.Optional(source.SubSystemPressure),
            LifecycleEndDate = MapperValue.Optional(source.LifecycleEndDate),
            ServiceAddress = MapperValue.Addresses(source.ServiceAddress)
        };
    }
}

internal static class MapperValue
{
    public static JsonElement? Optional(JsonElement? value)
    {
        if (value is not { } element ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String &&
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            return null;
        }

        // Preserve type, date text, whitespace within nonblank strings, 0 and false.
        return element.Clone();
    }

    public static ValveServiceAddress[]? Addresses(ValveServiceAddress?[]? source)
    {
        if (source is not { Length: > 0 })
        {
            return null;
        }

        var addresses = new List<ValveServiceAddress>(source.Length);
        foreach (var input in source)
        {
            if (input is null)
            {
                continue;
            }

            var address = new ValveServiceAddress
            {
                StreetAddress = Optional(input.StreetAddress),
                City = Optional(input.City),
                State = Optional(input.State),
                ZipCode = Optional(input.ZipCode)
            };

            if (address.StreetAddress is null && address.City is null &&
                address.State is null && address.ZipCode is null)
            {
                continue;
            }

            addresses.Add(address);
        }

        return addresses.Count == 0 ? null : addresses.ToArray();
    }

    // For logging/command metadata only; this does not change the mapped payload.
    public static string? ForLog(JsonElement? value)
    {
        var suppliedValue = Optional(value);
        return suppliedValue is not { } element
            ? null
            : element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
    }
}
