using System.Globalization;
using System.Text.Json.Nodes;

namespace Transformer.Services;

public static class TypeConverter
{
    public record ConversionResult(JsonNode? Value, bool Succeeded, string? ErrorMessage);

    public static ConversionResult Convert(JsonNode? input, string targetType, string? dateFormat)
    {
        if (input is null)
            return new(null, true, null);

        return targetType.ToLowerInvariant() switch
        {
            "string"   => ConvertToString(input),
            "decimal"  => ConvertToDecimal(input),
            "integer"  => ConvertToInteger(input),
            "boolean"  => ConvertToBoolean(input),
            "datetime" => ConvertToDatetime(input, dateFormat),
            _          => new(input, true, null)
        };
    }

    private static ConversionResult ConvertToString(JsonNode input)
    {
        var str = input is JsonValue val && val.TryGetValue<string>(out var s)
            ? s
            : input.ToJsonString();
        return new(JsonValue.Create(str), true, null);
    }

    private static ConversionResult ConvertToDecimal(JsonNode input)
    {
        if (input is JsonValue val)
        {
            if (val.TryGetValue<decimal>(out var dec)) return new(JsonValue.Create(dec), true, null);
            if (val.TryGetValue<double>(out var dbl)) return new(JsonValue.Create((decimal)dbl), true, null);
            if (val.TryGetValue<float>(out var flt)) return new(JsonValue.Create((decimal)flt), true, null);
            if (val.TryGetValue<long>(out var lng)) return new(JsonValue.Create((decimal)lng), true, null);
            if (val.TryGetValue<int>(out var i)) return new(JsonValue.Create((decimal)i), true, null);
            if (val.TryGetValue<bool>(out var b)) return new(JsonValue.Create(b ? 1m : 0m), true, null);
            if (val.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return new(JsonValue.Create(parsed), true, null);
        }
        return new(null, false, $"Cannot convert '{input.ToJsonString()}' to decimal.");
    }

    private static ConversionResult ConvertToInteger(JsonNode input)
    {
        if (input is JsonValue val)
        {
            if (val.TryGetValue<long>(out var lng)) return new(JsonValue.Create(lng), true, null);
            if (val.TryGetValue<int>(out var i)) return new(JsonValue.Create((long)i), true, null);
            if (val.TryGetValue<double>(out var dbl)) return new(JsonValue.Create((long)dbl), true, null);
            if (val.TryGetValue<decimal>(out var dec)) return new(JsonValue.Create((long)dec), true, null);
            if (val.TryGetValue<bool>(out var b)) return new(JsonValue.Create(b ? 1L : 0L), true, null);
            if (val.TryGetValue<string>(out var s) && long.TryParse(s, out var parsed))
                return new(JsonValue.Create(parsed), true, null);
        }
        return new(null, false, $"Cannot convert '{input.ToJsonString()}' to integer.");
    }

    private static ConversionResult ConvertToBoolean(JsonNode input)
    {
        if (input is JsonValue val)
        {
            if (val.TryGetValue<bool>(out var b)) return new(JsonValue.Create(b), true, null);
            if (val.TryGetValue<string>(out var s))
            {
                if (bool.TryParse(s, out var parsed)) return new(JsonValue.Create(parsed), true, null);
                if (s == "1") return new(JsonValue.Create(true), true, null);
                if (s == "0") return new(JsonValue.Create(false), true, null);
            }
            if (val.TryGetValue<double>(out var dbl)) return new(JsonValue.Create(dbl != 0d), true, null);
            if (val.TryGetValue<long>(out var lng)) return new(JsonValue.Create(lng != 0L), true, null);
            if (val.TryGetValue<int>(out var i)) return new(JsonValue.Create(i != 0), true, null);
        }
        return new(null, false, $"Cannot convert '{input.ToJsonString()}' to boolean.");
    }

    private static ConversionResult ConvertToDatetime(JsonNode input, string? dateFormat)
    {
        if (input is JsonValue val && val.TryGetValue<string>(out var s))
        {
            DateTime dt;
            bool parsed = !string.IsNullOrEmpty(dateFormat)
                ? DateTime.TryParseExact(s, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt)
                : DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dt);

            if (parsed)
            {
                var formatted = string.IsNullOrEmpty(dateFormat)
                    ? dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    : dt.ToString(dateFormat, CultureInfo.InvariantCulture);
                return new(JsonValue.Create(formatted), true, null);
            }
        }
        return new(null, false, $"Cannot convert '{input.ToJsonString()}' to datetime.");
    }
}
