using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewValley;

namespace DynamicItemSpriteCompositor.Models;

public sealed class StringIntListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<int>);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        return token.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => FromString(token.ToObject<string>()),
            JTokenType.Array => token.ToObject<List<int>>(),
            _ => [token.ToObject<int>()!],
        };
    }

    private static List<int>? FromString(string? strValue)
    {
        if (string.IsNullOrEmpty(strValue))
            return null;
        string[] parts = strValue.Split(',');
        List<int> result = [];
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index))
            {
                result.Add(index);
            }
        }
        return result.Count > 0 ? result : null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public sealed class StringColorConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(Color?);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.String)
        {
            return Utility.StringToColor(token.ToObject<string>());
        }
        return null;
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public abstract class StringSetConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<string>);
    }

    public abstract List<string>? NormalizeStringSet(IEnumerable<string>? values);

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer
    )
    {
        JToken token = JToken.Load(reader);
        return token.Type switch
        {
            JTokenType.Null => null,
            JTokenType.String => FromString(token.ToObject<string>()),
            _ => NormalizeStringSet(token.ToObject<List<string>>()),
        };
    }

    private List<string>? FromString(string? strValue)
    {
        if (string.IsNullOrEmpty(strValue))
            return null;
        string[] parts = strValue.Split(',');
        if (parts.Length <= 0)
            return null;
        return NormalizeStringSet(parts);
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class ContextTagsConverter : StringSetConverter
{
    public override List<string>? NormalizeStringSet(IEnumerable<string>? values)
    {
        if (values == null)
            return null;
        HashSet<string> valueSet = values.Select(ctag => ctag.ToLowerInvariant()).ToHashSet();
        valueSet.Remove(string.Empty);
        if (valueSet.Any())
            return valueSet.ToList();
        return null;
    }
}

public class SourceTexturesConverter : StringSetConverter
{
    public override List<string>? NormalizeStringSet(IEnumerable<string>? values)
    {
        if (values == null)
            return null;
        HashSet<string> valueSet = values.ToHashSet();
        valueSet.Remove(string.Empty);
        valueSet.Remove(null!);
        if (valueSet.Any())
            return valueSet.ToList();
        return null;
    }
}
