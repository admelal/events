# Valve direct-mapping update

This is a replacement-source package based on Pasted text(8).txt, not a complete Function App project. It changes business-payload handling in both directions. Program.cs, the project file, host.json, APIM policy, and your deployed settings were not included and are not replaced.

## Behavior

- No business-field validators, mandatory action, action allowlists, business enums, event-action status lookup, or business default values.
- No action-specific registration, change, or retirement branches. A supplied asset number or address is not discarded because of an action name.
- Only known source-to-target field names are mapped. Unknown input property names are ignored, not guessed or forwarded as arbitrary downstream properties.
- Missing, null, empty-string, and whitespace-only mapped fields are omitted.
- Nonblank values are preserved without trimming, date/GUID parsing, boolean conversion, status translation, or numeric conversion. JSON number 0 and boolean false remain present.
- Business fields use nullable JsonElement to retain the incoming JSON types. This is a contract type change from string/DateTimeOffset. Update any other C# code that directly initializes these properties.
- No event name is corrected or translated. GIS receives action unchanged. Maximo's supplied target contract has no ACTION field; the action is retained in command/log metadata, not invented as a new Maximo field.
- STATUS/status is copied only from an explicit status input field (case-insensitive, so STATUS also works). It is omitted when absent. lifeCycleStatus is not assumed to have the same meaning as Maximo STATUS.
- SERVICEADDRESS and ASSETSPEC are omitted when no populated entries remain.
- Routing metadata, structural JSON/envelope handling, authentication, HTTP/ArcGIS error handling, correlation handling, retries and token caching remain.

The receiver may still reject a partial payload. Removing local validation does not waive Maximo or ArcGIS server requirements, and cannot fix an Invalid URL or Invalid Token error.

## Address contract — review before deployment

Both flows read serviceAddress arrays containing streetAddress, city, state and zipCode. The old input address alias and the legacy top-level input address/city/state/zipCode fields are removed.

The GIS output in this package uses the same serviceAddress array instead of the old flat address/city/state/zipCode properties. This is a destination-contract change, not merely an input rename. Confirm that the receiving GIS task expects this array before deploying. Multiple populated addresses are preserved; none is silently selected or discarded to fit a single-address output.

Input example:

```json
{
  "serviceAddress": [
    {
      "streetAddress": "123 Main St",
      "city": "Boston",
      "state": "MA",
      "zipCode": "02134"
    }
  ]
}
```

Maximo destination names remain SERVICEADDRESS[].STREETADDRESS, CITY, STATEPROVINCE and POSTALCODE.

## Replace these 11 files together

Copy the contents of replacements/ into the directory containing your existing Function App .csproj, preserving the relative paths.

| Replacement | Change |
| --- | --- |
| Configuration/DownstreamOptions.cs | Removes ValveEventType only; preserves connection, retry, timeout and token settings/defaults. |
| Configuration/ValveIntegrationOptions.cs | Removes Mapping, MappingOptions and MappingDefaults; keeps ServiceBus and Request settings. |
| Contracts/ValveInspectionEvent.cs | Optional raw business values; explicit status; streetAddress input; preserves existing uppercase Maximo ID aliases. |
| Contracts/GisValvePayload.cs | Optional raw values and nested serviceAddress array. |
| Contracts/MaximoAssetPayload.cs | Removes business required properties; nullable values and optional collections. |
| Contracts/IntegrationMessages.cs | Action command/result metadata is nullable; operational envelope remains intact. |
| Serialization/JsonDefaults.cs | Ignores unknown input fields and suppresses absent/blank mapped properties. |
| Services/Mapping.cs | Direct mappings only, no configuration dependency, lookup or action branching. |
| Functions/CommonValveInspectionSubscriber.cs | Removes payload validator dependency and validation dead-letter branch; preserves routing/envelope checks. |
| Functions/ValveInspectionHttpStart.cs | Removes payload validation; preserves HTTP routes, routing headers and structural error responses. |
| Functions/ValveInspectionActivities.cs | Removes validator dependency and RequireAction; preserves transformation/send logging and error handling. |

Keep only one source file defining GisToMaximoMapper, MaximoToGisMapper and their interfaces. If your old file is named ValvePayloadMappers.cs, replace its contents with Services/Mapping.cs OR remove that old duplicate after adding Mapping.cs. Do not compile both.

## Program.cs changes still required

Remove the IValveEventValidator registration and any dependency-injection factories referencing it:

```csharp
// Remove this old registration:
// builder.Services.AddSingleton<IValveEventValidator, ValveEventValidator>();
```

Keep each mapper registered once:

```csharp
builder.Services.AddSingleton<IGisToMaximoMapper, GisToMaximoMapper>();
builder.Services.AddSingleton<IMaximoToGisMapper, MaximoToGisMapper>();
```

The mapper constructors no longer take IOptions<DownstreamOptions>. Update any explicit mapper factory or test constructor call.

Keep your existing configuration-provider loading and options bindings. Remove checks that require ValveEventType or Mapping dictionaries, but retain startup checks for operational settings (valid HTTPS endpoints, credentials, positive timeout/retry values). Do not replace your entire Program.cs with a generic sample.

Keep existing singleton ArcGIS client/token-provider registrations and named HttpClient registrations. Token TTL, early refresh, retries and in-memory caching are unchanged by this package. Runtime option defaults are deliberately retained; no business-payload defaults are retained.

Ensure the worker serializer and all payload HTTP serialization paths use JsonDefaults.Apply/Options as in your existing setup. JsonElement preserves supplied JSON without relying on business enums or scalar string conversion.

## Safe cleanup after updating references

1. Remove Services/ValveEventValidator.cs (and separate IValveEventValidator/ValveValidationException files if they exist).
2. Remove Contracts/ValveActions.cs once repository-wide references are gone.
3. Remove separate EventTypeLookup or MappingLookups files if present. The replacement Mapping.cs does not define or use them.
4. Remove configuration entries for ValveEventType and ValveInspection:Mapping once no remaining application version needs them. Do not delete timeout/retry/token configuration.
5. Update tests asserting old required-field validation, default values, action-derived status or old flat addresses.
6. Keep DownstreamRequestException and HttpContentExtensions: the runtime clients use them.
7. Do not delete DownstreamEndpointBuilder, options-validator files, HTTP entry points or health endpoints solely because they look unused here. Program.cs and other callers were not supplied; inspect references first.
8. The pasted attachment repeats ArcGisTokenProvider. Keep only one definition in the real project. No ArcGIS runtime files are replaced in this package.

A read-only reference check from the project root:

```bash
rg -n "IValveEventValidator|ValveValidationException|ValveActions|EventTypeLookup|MappingLookups|ValveEventType|MappingOptions|MappingDefaults|RequireAction|NormalizeOptionalDate" --glob "*.cs"
```

Review remaining references before removing their definitions. Preserve existing source-control history/backup when replacing files.

## Run the included mapping checks

Requires the .NET 8 SDK or newer with the .NET 8 target/runtime available. No external test packages are used.

```bash
dotnet run --project checks/MappingChecks/MappingChecks.csproj
```

There are 28 regression scenarios covering empty payloads, absent values, arbitrary/missing actions, no action-derived status, raw numbers/booleans, dates, Maximo ID aliases, nested addresses, omitted empty collections and envelope serialization.

The check project compiles only configuration, contracts, serialization and mapping. Keep checks/ outside the Function App project directory so its top-level Program.cs is not compiled into the Function App.

After updating Program.cs and removing old references, run your actual project build and existing integration tests:

```bash
dotnet build
```

Verification status for this delivery: source/reference and delimiter checks performed; the .NET SDK is unavailable in the authoring environment, so neither this check project nor the full Function App was compiled/executed. No live Service Bus, Maximo, ArcGIS or Azure configuration calls were made.

## Framework references

- [JsonElement preserves arbitrary JSON values](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow).
- [Conditional property serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/custom-contracts).

