for ValveSpatialRegistration

{
    "eventTimestamp": "2026-07-29T15:39:00Z",
    "action": "ValveSpatialRegistration",
    "globalId": "{8D7B8F91-7A4A-4EAB-BB0A-4A8E9E79E7CB}",
    "lifeCycleStatus": "2",
    "physicalValveId": "NYC-VLV-100234",
    "serviceTerritoryName": "MA Boston West I&R",
    "installationdate": "2026-07-29",
    "classification": "System",
    "regulatoryType": "Distribution",
    "isCriticalValve": "Yes",
    "isSectionalizingValve": "No",
    "isEGOMPValve": "No",
    "isInterconnectValve": "Yes",
    "isAutomaticShutoffValve": "No",
    "isRemoteOperatedValve": "No",
    "isRuptureMitigationValve": "No",
    "isLowPressureValve": "Yes",
    "crossingType": "River",
    "accessType": "Vault",
    "normalPosition": "Normally Open",
    "subSystemName": "Yes",
    "subSystemPressure": "Yes",
    "serviceAddress": [
        {
            "address": "123 Main St, Boston, MA 02134",
            "city": "Boston",
            "state": "MA",
            "zipCode": "02134"
        }
    ]
}
==========================
for ValveFeatureRetired
{	
	"eventTimestamp": "2026-07-29T15:39:00Z",
	"action": "ValveFeatureRetired",
	"globalId": "{8D7B8F91-7A4A-4EAB-BB0A-4A8E9E79E7CB}",
	"maximoAssetNumber": "10124657",
	"physicalValveId": "NYC-VLV-100234",
	"lifeCycleStatus": "5",
	"lifeCycleEndDate": "2026-07-29"
}	

=======================
for ValveFeatureChanged
{
  "eventTimestamp": "2026-07-29T15:39:00Z",
  "action": "ValveFeatureChanged",
  "globalId": "{8D7B8F91-7A4A-4EAB-BB0A-4A8E9E79E7CB}",
  "maximoAssetNumber": "10124657",
  "physicalValveId": "NYC-VLV-100234",
  "lifeCycleStatus": "3",
  "serviceTerritoryName": "MA Boston West I&R",
  "installationDate": "2026-07-29",
  "classification": "System",
  "regulatoryType": "Distribution",
  "isCriticalValve": "Yes",
  "isSectionalizingValve": "No",
  "isEGOMPValve": "No",
  "isInterconnectValve": "Yes",
  "isAutomaticShutoffValve": "No",
  "isRemoteOperatedValve": "No",
  "isRuptureMitigationValve": "No",
  "isLowPressureValve": "Yes",
  "crossingType": "Railroad",
  "accessType": "Yes",
  "normalPosition": "Yes",
  "subSystemName": "WALTHAM_MP_ZONE_A",
  "subSystemPressure": "MEDIUM",
  "serviceAddress": [
    {
      "streetAddress": "123 Main St",
      "city": "Boston",
      "state": "MA",
      "zipCode": "02134"
    }
  ]
}

Based on the above payload, modify the following code. 
using System.Globalization;
using GaValveInspectionGisMaximo.Configuration;
using GaValveInspectionGisMaximo.Contracts;
using Microsoft.Extensions.Options;

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
    private const string DefaultHierarchyPath = "System";

    private static class AssetAttributeIds
    {
        public const string AccessType = "NG_ACCTYPE";
        public const string AutomaticShutoff = "NG_AUTOSHTOFF";
        public const string Critical = "NG_CRITICAL";
        public const string CrossingType = "NG_CROSSTYPE";
        public const string DateOfRetirement = "NG_DATEOFRETIREMENT";
        public const string Egomp = "NG_EGOMP";
        public const string Interconnect = "NG_INTERCONCT";
        public const string LowPressure = "NG_LOWPRS";
        public const string NormalPosition = "NG_NMPOSITN";
        public const string RegulatoryType = "NG_REGULTYPE";
        public const string RemoteOperated = "NG_REMOPERTD";
        public const string RuptureMitigation = "NG_RUPTUREMIT";
        public const string Sectionalizing = "NG_SECTNLIZING";
        public const string SubSystemName = "NG_SUBSYSNM";
        public const string SubSystemPressure = "NG_SUBSYSPRS";
    }

    private readonly IReadOnlyDictionary<string, string> _valveEventTypes;

    public GisToMaximoMapper(
        IOptions<DownstreamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);

        _valveEventTypes = EventTypeLookup.Create(
            options.Value.ValveEventType);
    }

    public MaximoAssetPayload Map(
        ValveInspectionEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var action = MapperValue.Required(
            source.Action,
            nameof(source.Action));

        return new MaximoAssetPayload
        {
            NgGuid = MapperValue.Required(
                source.GlobalId,
                nameof(source.GlobalId)),

            Status = EventTypeLookup.GetRequired(
                _valveEventTypes,
                action),
            AssetNum = MapperValue.Required(
                source.MaximoAssetNumber,
                nameof(source.MaximoAssetNumber)),
                
            AssetTag = MapperValue.Required(
                source.PhysicalValveId,
                nameof(source.PhysicalValveId)),

            ServiceTerritory = MapperValue.Required(
                source.ServiceTerritoryName,
                nameof(source.ServiceTerritoryName)),

            HierarchyPath = DefaultHierarchyPath,

            InstallDate = MapperValue.NormalizeOptionalDate(
                source.InstallationDate,
                nameof(source.InstallationDate)),

            ServiceAddress = BuildServiceAddress(source),

            AssetSpec = BuildAssetSpecifications(source)
        };
    }

    private static MaximoAssetSpecification[]
        BuildAssetSpecifications(
            ValveInspectionEvent source)
    {
        var specifications =
            new List<MaximoAssetSpecification>(15);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.AccessType,
            source.AccessType);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.AutomaticShutoff,
            source.IsAutomaticShutoffValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.Critical,
            source.IsCriticalValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.CrossingType,
            source.CrossingType);

        AddDateSpecification(
            specifications,
            AssetAttributeIds.DateOfRetirement,
            source.LifecycleEndDate);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.Egomp,
            source.IsEgompValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.Interconnect,
            source.IsInterconnectValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.LowPressure,
            source.IsLowPressureValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.NormalPosition,
            source.NormalPosition);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.RegulatoryType,
            source.RegulatoryType);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.RemoteOperated,
            source.IsRemoteOperatedValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.RuptureMitigation,
            source.IsRuptureMitigationValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.Sectionalizing,
            source.IsSectionalizingValve);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.SubSystemName,
            source.SubSystemName);

        AddAlnSpecification(
            specifications,
            AssetAttributeIds.SubSystemPressure,
            source.SubSystemPressure);

        return specifications.ToArray();
    }

    private static MaximoServiceAddress[]?
        BuildServiceAddress(
            ValveInspectionEvent source)
    {
        if (source.ServiceAddress is { Length: > 0 })
        {
            var addresses =
                new List<MaximoServiceAddress>(
                    source.ServiceAddress.Length);

            foreach (var sourceAddress in source.ServiceAddress)
            {
                if (sourceAddress is null)
                {
                    continue;
                }

                var streetAddress =
                    MapperValue.Clean(sourceAddress.StreetAddress);

                var city =
                    MapperValue.Clean(sourceAddress.City);

                var stateProvince =
                    MapperValue.Clean(sourceAddress.State);

                var postalCode =
                    MapperValue.Clean(sourceAddress.ZipCode);

                if (streetAddress is null &&
                    city is null &&
                    stateProvince is null &&
                    postalCode is null)
                {
                    continue;
                }

                addresses.Add(
                    new MaximoServiceAddress
                    {
                        StreetAddress = streetAddress,
                        City = city,
                        StateProvince = stateProvince,
                        PostalCode = postalCode
                    });
            }

            if (addresses.Count > 0)
            {
                return addresses.ToArray();
            }
        }

        // Backward compatibility for messages that use flat address fields.
        return BuildLegacyServiceAddress(source);
    }

    private static MaximoServiceAddress[]?
        BuildLegacyServiceAddress(
            ValveInspectionEvent source)
    {
        var streetAddress =
            MapperValue.Clean(source.Address);

        var city =
            MapperValue.Clean(source.City);

        var stateProvince =
            MapperValue.Clean(source.State);

        var postalCode =
            MapperValue.Clean(source.ZipCode);

        if (streetAddress is null &&
            city is null &&
            stateProvince is null &&
            postalCode is null)
        {
            return null;
        }

        return
        [
            new MaximoServiceAddress
           {
               StreetAddress = streetAddress,
               City = city,
               StateProvince = stateProvince,
               PostalCode = postalCode
           }
        ];
    }

    private static void AddAlnSpecification(
        ICollection<MaximoAssetSpecification> specifications,
        string assetAttributeId,
        string? value)
    {
        ArgumentNullException.ThrowIfNull(specifications);

        var cleanedValue = MapperValue.Clean(value);

        if (cleanedValue is null)
        {
            return;
        }

        specifications.Add(
            new MaximoAssetSpecification
            {
                AssetAttributeId = assetAttributeId,
                AlnValue = cleanedValue
            });
    }

    private static void AddDateSpecification(
        ICollection<MaximoAssetSpecification> specifications,
        string assetAttributeId,
        string? value)
    {
        ArgumentNullException.ThrowIfNull(specifications);

        var normalizedDate = MapperValue.NormalizeOptionalDate(
            value,
            assetAttributeId);

        if (normalizedDate is null)
        {
            return;
        }

        specifications.Add(
            new MaximoAssetSpecification
            {
                AssetAttributeId = assetAttributeId,
                DateValue = normalizedDate
            });
    }
}

public sealed class MaximoToGisMapper : IMaximoToGisMapper
{
    private readonly IReadOnlyDictionary<string, string> _valveEventTypes;

    public MaximoToGisMapper(
        IOptions<DownstreamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);

        _valveEventTypes = EventTypeLookup.Create(
            options.Value.ValveEventType);
    }

    public GisValvePayload Map(
        ValveInspectionEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var action = MapperValue.Required(
            source.Action,
            nameof(source.Action));

        var serviceAddress =
            GetPrimaryServiceAddress(source.ServiceAddress);

        return new GisValvePayload
        {
            EventTimestamp = source.EventTimestamp,

            Action = action,

            Status = EventTypeLookup.GetRequired(
                _valveEventTypes,
                action),

            GlobalId =
                MapperValue.Clean(source.GlobalId),

            AssetId =
                MapperValue.Clean(source.AssetId) ??
                MapperValue.Clean(source.MaximoAssetNumber),

            LifeCycleStatus =
                MapperValue.Clean(source.LifeCycleStatus),

            PhysicalValveId =
                MapperValue.Clean(source.PhysicalValveId),

            ServiceTerritoryName =
                MapperValue.Clean(source.ServiceTerritoryName),

            InstallationDate =
                MapperValue.Clean(source.InstallationDate),

            Classification =
                MapperValue.Clean(source.Classification),

            RegulatoryType =
                MapperValue.Clean(source.RegulatoryType),

            IsCriticalValve =
                MapperValue.Clean(source.IsCriticalValve),

            IsSectionalizingValve =
                MapperValue.Clean(source.IsSectionalizingValve),

            IsEgompValve =
                MapperValue.Clean(source.IsEgompValve),

            IsInterconnectValve =
                MapperValue.Clean(source.IsInterconnectValve),

            IsAutomaticShutoffValve =
                MapperValue.Clean(source.IsAutomaticShutoffValve),

            IsRemoteOperatedValve =
                MapperValue.Clean(source.IsRemoteOperatedValve),

            IsRuptureMitigationValve =
                MapperValue.Clean(source.IsRuptureMitigationValve),

            IsLowPressureValve =
                MapperValue.Clean(source.IsLowPressureValve),

            CrossingType =
                MapperValue.Clean(source.CrossingType),

            AccessType =
                MapperValue.Clean(source.AccessType),

            NormalPosition =
                MapperValue.Clean(source.NormalPosition),

            SubSystemName =
                MapperValue.Clean(source.SubSystemName),

            SubSystemPressure =
                MapperValue.Clean(source.SubSystemPressure),

            Address =
                MapperValue.Clean(source.Address) ??
                MapperValue.Clean(serviceAddress?.StreetAddress),

            City =
                MapperValue.Clean(source.City) ??
                MapperValue.Clean(serviceAddress?.City),

            State =
                MapperValue.Clean(source.State) ??
                MapperValue.Clean(serviceAddress?.State),

            ZipCode =
                MapperValue.Clean(source.ZipCode) ??
                MapperValue.Clean(serviceAddress?.ZipCode),

            LifecycleEndDate =
                MapperValue.Clean(source.LifecycleEndDate)
        };
    }

    private static ValveServiceAddress?
        GetPrimaryServiceAddress(
            ValveServiceAddress[]? addresses)
    {
        if (addresses is null ||
            addresses.Length == 0)
        {
            return null;
        }

        foreach (var address in addresses)
        {
            if (address is null)
            {
                continue;
            }

            if (MapperValue.Clean(address.StreetAddress) is not null ||
                MapperValue.Clean(address.City) is not null ||
                MapperValue.Clean(address.State) is not null ||
                MapperValue.Clean(address.ZipCode) is not null)
            {
                return address;
            }
        }

        return null;
    }
}

internal static class EventTypeLookup
{
    public static IReadOnlyDictionary<string, string> Create(
        IReadOnlyDictionary<string, string>? configuredValues)
    {
        if (configuredValues is null ||
            configuredValues.Count == 0)
        {
            throw new InvalidOperationException(
                "ValveEventType configuration must contain at least " +
                "one action-to-status mapping.");
        }

        var validatedValues =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var entry in configuredValues)
        {
            var action = MapperValue.Clean(entry.Key);
            var status = MapperValue.Clean(entry.Value);

            if (action is null || status is null)
            {
                throw new InvalidOperationException(
                    "ValveEventType configuration contains an empty " +
                    "action or status.");
            }

            if (!validatedValues.TryAdd(action, status))
            {
                throw new InvalidOperationException(
                    $"ValveEventType contains duplicate action '{action}'.");
            }
        }

        return validatedValues;
    }

    public static string GetRequired(
        IReadOnlyDictionary<string, string> eventTypes,
        string action)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        if (eventTypes.TryGetValue(
                action,
                out var eventType) &&
            !string.IsNullOrWhiteSpace(eventType))
        {
            return eventType.Trim();
        }

        throw new InvalidOperationException(
            $"No valve event type is configured for action '{action}'.");
    }
}

internal static class MapperValue
{
    public static string Required(
        string? value,
        string propertyName)
    {
        return Clean(value)
            ?? throw new InvalidOperationException(
                $"{propertyName} is required.");
    }

    public static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    public static string? NormalizeOptionalDate(
        string? value,
        string propertyName)
    {
        var cleanedValue = Clean(value);

        if (cleanedValue is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(
                cleanedValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        if (DateTimeOffset.TryParse(
                cleanedValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var timestamp))
        {
            return timestamp.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        throw new InvalidOperationException(
            $"{propertyName} must contain a valid ISO-8601 date. " +
            $"Received '{cleanedValue}'.");
    }
}
