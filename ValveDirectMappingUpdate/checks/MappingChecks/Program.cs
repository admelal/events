using System.Text.Json;
using GaValveInspectionGisMaximo.Configuration;
using GaValveInspectionGisMaximo.Contracts;
using GaValveInspectionGisMaximo.Serialization;
using GaValveInspectionGisMaximo.Services;

var executed = 0;

ValveInspectionEvent Read(string json) =>
    JsonSerializer.Deserialize<ValveInspectionEvent>(json, JsonDefaults.Options)!;

JsonElement Serialized<T>(T value) =>
    JsonSerializer.SerializeToElement(value, JsonDefaults.Options);

JsonElement ToMaximo(string json) => Serialized(new GisToMaximoMapper().Map(Read(json)));
JsonElement ToGis(string json) => Serialized(new MaximoToGisMapper().Map(Read(json)));

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

void Test(string name, Action body)
{
    body();
    executed++;
    Console.WriteLine($"PASS {name}");
}

Test("Empty payload maps to an empty object in both directions", () =>
{
    Assert(ToMaximo("{}").EnumerateObject().Count() == 0, "Maximo introduced fields.");
    Assert(ToGis("{}").EnumerateObject().Count() == 0, "GIS introduced fields.");
});

Test("Unknown and misspelled event actions do not block mapping", () =>
{
    var output = ToGis("""{"action":"ValveLifecycleInitated","physicalValveId":"V-1"}""");
    Assert(output.GetProperty("action").GetString() == "ValveLifecycleInitated", "Action changed.");
    Assert(output.GetProperty("physicalValveId").GetString() == "V-1", "ID was dropped.");
});

Test("Action is never used to generate status", () =>
{
    var input = """{"action":"ValveFeatureRetired"}""";
    Assert(!ToMaximo(input).TryGetProperty("STATUS", out _), "Derived Maximo status.");
    Assert(!ToGis(input).TryGetProperty("status", out _), "Derived GIS status.");
});

Test("Lifecycle codes are not assumed to be target status codes", () =>
{
    var input = """{"lifeCycleStatus":"5"}""";
    Assert(!ToMaximo(input).TryGetProperty("STATUS", out _), "Inferred Maximo status.");
    Assert(!ToGis(input).TryGetProperty("status", out _), "Inferred GIS status.");
    Assert(ToGis(input).GetProperty("lifeCycleStatus").GetString() == "5", "Lifecycle code lost.");
});

Test("Explicit status is copied without lookup", () =>
{
    Assert(ToMaximo("""{"status":"CUSTOM"}""").GetProperty("STATUS").GetString() == "CUSTOM", "Status changed.");
    Assert(ToGis("""{"STATUS":0}""").GetProperty("status").GetInt32() == 0, "Uppercase numeric status lost.");
});

Test("Missing action is allowed", () =>
{
    var result = ToMaximo("""{"maximoAssetNumber":"10124657"}""");
    Assert(result.GetProperty("ASSETNUM").GetString() == "10124657", "Missing action blocked asset mapping.");
});

Test("Registration no longer discards an asset number", () =>
{
    var result = ToMaximo("""{"action":"ValveSpatialRegistration","maximoAssetNumber":"10124657"}""");
    Assert(result.GetProperty("ASSETNUM").GetString() == "10124657", "Action filtered a field.");
});

Test("Retirement no longer discards other supplied fields", () =>
{
    var result = ToMaximo("""{"action":"ValveFeatureRetired","serviceTerritoryName":"West","classification":"Station","installationDate":"raw-date"}""");
    Assert(result.GetProperty("NG_SERVTERRITORY").GetString() == "West", "Territory dropped.");
    Assert(result.GetProperty("HIERARCHYPATH").GetString() == "Station", "Classification dropped.");
    Assert(result.GetProperty("INSTALLDATE").GetString() == "raw-date", "Date changed.");
});

Test("No default hierarchy or asset specifications", () =>
{
    var result = ToMaximo("""{"globalId":"not-a-guid"}""");
    Assert(result.GetProperty("NG_GUID").GetString() == "not-a-guid", "GUID was validated.");
    Assert(!result.TryGetProperty("HIERARCHYPATH", out _), "Invented hierarchy.");
    Assert(!result.TryGetProperty("ASSETSPEC", out _), "Invented empty specifications.");
});

Test("Null and blank business values are omitted", () =>
{
    var input = """{"globalId":null,"action":"  ","status":"","serviceTerritoryName":"\t","isCriticalValve":null}""";
    Assert(ToMaximo(input).EnumerateObject().Count() == 0, "Maximo emitted absent values.");
    Assert(ToGis(input).EnumerateObject().Count() == 0, "GIS emitted absent values.");
});

Test("Nonblank string content is preserved without trimming", () =>
{
    var result = ToGis("""{"action":"  Custom Action  ","physicalValveId":"  V-1  "}""");
    Assert(result.GetProperty("action").GetString() == "  Custom Action  ", "Action trimmed.");
    Assert(result.GetProperty("physicalValveId").GetString() == "  V-1  ", "ID trimmed.");
});

Test("False and zero retain their JSON types", () =>
{
    var result = ToGis("""{"isCriticalValve":false,"lifeCycleStatus":0}""");
    Assert(result.GetProperty("isCriticalValve").ValueKind == JsonValueKind.False, "False lost.");
    Assert(result.GetProperty("lifeCycleStatus").GetInt32() == 0, "Zero lost.");
});

Test("Specification values are not converted to strings", () =>
{
    var result = ToMaximo("""{"isCriticalValve":false,"subSystemPressure":0}""");
    var specs = result.GetProperty("ASSETSPEC").EnumerateArray().ToArray();
    Assert(specs.Length == 2, "Specification count differs.");
    Assert(specs[0].GetProperty("ALNVALUE").ValueKind == JsonValueKind.False, "False changed.");
    Assert(specs[1].GetProperty("ALNVALUE").GetInt32() == 0, "Zero changed.");
});

Test("Dates and timestamps are copied as supplied", () =>
{
    var result = ToGis("""{"eventTimestamp":"not-parsed","installationDate":"31/12/2026","lifecycleEndDate":"unknown"}""");
    Assert(result.GetProperty("eventTimestamp").GetString() == "not-parsed", "Timestamp parsed.");
    Assert(result.GetProperty("installationDate").GetString() == "31/12/2026", "Date normalized.");
    Assert(result.GetProperty("lifecycleEndDate").GetString() == "unknown", "Retirement date validated.");
});

Test("Retirement specification preserves source date text", () =>
{
    var result = ToMaximo("""{"lifecycleEndDate":"2026-09-18T09:30:00-04:00"}""");
    var spec = result.GetProperty("ASSETSPEC")[0];
    Assert(spec.GetProperty("ASSETATTRID").GetString() == "NG_DATEOFRETIREMENT", "Wrong destination field.");
    Assert(spec.GetProperty("DATEVALUE").GetString() == "2026-09-18T09:30:00-04:00", "Date truncated.");
});

Test("Business enum-like values are passed through", () =>
{
    var result = ToGis("""{"classification":"SomethingNew","crossingType":"Other","isCriticalValve":"Maybe"}""");
    Assert(result.GetProperty("classification").GetString() == "SomethingNew", "Classification validated.");
    Assert(result.GetProperty("crossingType").GetString() == "Other", "Crossing type validated.");
    Assert(result.GetProperty("isCriticalValve").GetString() == "Maybe", "Yes/no value validated.");
});

Test("Known Maximo field aliases still map to GIS", () =>
{
    var result = ToGis("""{"ASSETNUM":"M-1","NG_GUID":"raw-guid","ASSETTAG":"V-1"}""");
    Assert(result.GetProperty("assetId").GetString() == "M-1", "ASSETNUM not mapped.");
    Assert(result.GetProperty("globalId").GetString() == "raw-guid", "NG_GUID not mapped.");
    Assert(result.GetProperty("physicalValveId").GetString() == "V-1", "ASSETTAG not mapped.");
});

Test("Existing canonical field priority is preserved", () =>
{
    var result = ToGis("""{"globalId":"canonical","NG_GUID":"alias","assetId":"canonical-id","ASSETNUM":"alias-id"}""");
    Assert(result.GetProperty("globalId").GetString() == "canonical", "Alias overrode canonical field.");
    Assert(result.GetProperty("assetId").GetString() == "canonical-id", "Alias overrode canonical ID.");
});

Test("Blank canonical fields fall back only to supplied aliases", () =>
{
    var result = ToGis("""{"globalId":" ","NG_GUID":"supplied","physicalValveId":null,"ASSETTAG":"V-2"}""");
    Assert(result.GetProperty("globalId").GetString() == "supplied", "Alias fallback failed.");
    Assert(result.GetProperty("physicalValveId").GetString() == "V-2", "Tag fallback failed.");
});

Test("Street address maps to Maximo target fields", () =>
{
    var result = ToMaximo("""{"serviceAddress":[{"streetAddress":"123 Main St","city":"Boston","state":"MA","zipCode":"02134"}]}""");
    var address = result.GetProperty("SERVICEADDRESS")[0];
    Assert(address.GetProperty("STREETADDRESS").GetString() == "123 Main St", "Street missing.");
    Assert(address.GetProperty("STATEPROVINCE").GetString() == "MA", "State missing.");
    Assert(address.GetProperty("POSTALCODE").GetString() == "02134", "Leading zero lost.");
});

Test("GIS streetAddress array preserves all populated entries", () =>
{
    var result = ToGis("""{"serviceAddress":[{"streetAddress":"One"},{"streetAddress":"Two","city":"Boston"}]}""");
    var addresses = result.GetProperty("serviceAddress");
    Assert(addresses.GetArrayLength() == 2, "Address was discarded.");
    Assert(addresses[1].GetProperty("streetAddress").GetString() == "Two", "Street changed.");
    Assert(!result.TryGetProperty("address", out _), "Legacy flat address emitted.");
});

Test("Null and empty address entries are skipped", () =>
{
    var input = """{"serviceAddress":[null,{},{"streetAddress":" "},{"city":"Boston"}]}""";
    var mx = ToMaximo(input).GetProperty("SERVICEADDRESS");
    var gis = ToGis(input).GetProperty("serviceAddress");
    Assert(mx.GetArrayLength() == 1 && gis.GetArrayLength() == 1, "Empty addresses retained.");
    Assert(!mx[0].TryGetProperty("STREETADDRESS", out _), "Missing street fabricated.");
});

Test("All-empty address arrays are omitted", () =>
{
    var input = """{"serviceAddress":[null,{},{"streetAddress":""}]}""";
    Assert(!ToMaximo(input).TryGetProperty("SERVICEADDRESS", out _), "Empty Maximo address emitted.");
    Assert(!ToGis(input).TryGetProperty("serviceAddress", out _), "Empty GIS address emitted.");
});

Test("Old input address alias is not used", () =>
{
    var input = """{"serviceAddress":[{"address":"old field","city":"Boston"}]}""";
    Assert(!ToMaximo(input).GetProperty("SERVICEADDRESS")[0].TryGetProperty("STREETADDRESS", out _), "Old alias used.");
    Assert(!ToGis(input).GetProperty("serviceAddress")[0].TryGetProperty("streetAddress", out _), "Old alias used.");
});

Test("Unmapped fields are ignored", () =>
{
    var input = """{"newUnknownField":"ignored","physicalValveId":"V-3"}""";
    var result = ToGis(input);
    Assert(!result.TryGetProperty("newUnknownField", out _), "Unknown destination invented.");
    Assert(result.GetProperty("physicalValveId").GetString() == "V-3", "Known value lost.");
});

Test("Envelope and command round trips preserve optional payload values", () =>
{
    var input = new ValveInspectionMessage("MaximoToGis", "test-only", DateTimeOffset.UtcNow,
        Read("""{"eventTimestamp":"unparsed","isCriticalValve":false}"""));
    var json = JsonSerializer.Serialize(input, JsonDefaults.Options);
    var restored = JsonSerializer.Deserialize<ValveInspectionMessage>(json, JsonDefaults.Options)!;
    var payload = new MaximoToGisMapper().Map(restored.Payload);
    var command = new GisSendCommand("test-only", MapperValue.ForLog(restored.Payload.Action), payload);
    var serialized = Serialized(command);
    Assert(!serialized.TryGetProperty("action", out _), "Missing action made required.");
    Assert(serialized.GetProperty("payload").GetProperty("isCriticalValve").ValueKind == JsonValueKind.False, "Round trip changed false.");
});

Test("Malformed JSON is still an error", () =>
{
    try { Read("{"); }
    catch (JsonException) { return; }
    throw new InvalidOperationException("Malformed JSON was accepted.");
});

Test("Infrastructure defaults remain available", () =>
{
    var options = new DownstreamOptions();
    Assert(options.GisTokenMaxAttempts == 3, "Token retries changed.");
    Assert(options.GisTokenExpirationMinutes == 60, "Token TTL changed.");
    Assert(options.GisTokenRefreshSkewMinutes == 5, "Refresh skew changed.");
    Assert(options.MaxAttempts == 4, "Durable retry default changed.");
});

Console.WriteLine($"All {executed} mapping checks passed.");
