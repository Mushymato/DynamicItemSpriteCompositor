using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

public sealed class ContextTagSetConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<string>);
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
            _ => token.ToObject<List<string>>()?.Select(part => part.ToLowerInvariant()).ToHashSet().ToList(),
        };
    }

    private static List<string>? FromString(string? strValue)
    {
        if (string.IsNullOrEmpty(strValue))
            return null;
        string[] parts = strValue.Split(',');
        if (parts.Length <= 0)
            return null;
        HashSet<string> ctags = parts.Select(part => part.ToLowerInvariant().Trim()).ToHashSet();
        ctags.Remove(string.Empty);
        return ctags.ToList();
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public sealed class SourceTextureOptionListConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<SourceTextureOption>);
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
            JTokenType.Array => token.ToObject<List<SourceTextureOption>>()?.ToList(),
            JTokenType.String => token.ToObject<string>() is string texture ? [new() { Texture = texture }] : null,
            _ => token.ToObject<SourceTextureOption>() is SourceTextureOption opt ? [opt] : null,
        };
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
