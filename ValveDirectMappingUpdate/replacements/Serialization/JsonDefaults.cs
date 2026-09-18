using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GaValveInspectionGisMaximo.Serialization;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { OmitAbsentValues }
        };
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        Apply(options);
        return options;
    }

    private static void OmitAbsentValues(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType != typeof(string) &&
                property.PropertyType != typeof(JsonElement) &&
                property.PropertyType != typeof(JsonElement?))
            {
                continue;
            }

            var existingCondition = property.ShouldSerialize;
            property.ShouldSerialize = (instance, value) =>
                IsSupplied(value) &&
                (existingCondition?.Invoke(instance, value) ?? true);
        }
    }

    private static bool IsSupplied(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        if (value is JsonElement element)
        {
            return element.ValueKind is not
                       (JsonValueKind.Null or JsonValueKind.Undefined) &&
                   (element.ValueKind != JsonValueKind.String ||
                    !string.IsNullOrWhiteSpace(element.GetString()));
        }

        return true;
    }
}
