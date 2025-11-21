using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Extensions;

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
            string? tokenValue = token.ToObject<string>();
            if (tokenValue == null)
                return null;
            return ContextTagToColor(tokenValue)
                ?? Utility.StringToColor(tokenValue)
                ?? ModEntry.EMC?.GetColorOverride(tokenValue);
        }
        return null;
    }

    /// <summary>
    /// Direct copy of ItemContextTagManager.GetColorFromTags inner loop.
    /// </summary>
    /// <param name="contextTag"></param>
    /// <returns></returns>
    public static Color? ContextTagToColor(string? contextTag)
    {
        if (contextTag == null)
            return null;
        if (contextTag.StartsWithIgnoreCase("color_"))
        {
            switch (contextTag.ToLowerInvariant())
            {
                case "color_black":
                    return new Color(45, 45, 45);
                case "color_gray":
                    return Color.Gray;
                case "color_white":
                    return Color.White;
                case "color_pink":
                    return new Color(255, 163, 186);
                case "color_red":
                    return new Color(220, 0, 0);
                case "color_orange":
                    return new Color(255, 128, 0);
                case "color_yellow":
                    return new Color(255, 230, 0);
                case "color_green":
                    return new Color(10, 143, 0);
                case "color_blue":
                    return new Color(46, 85, 183);
                case "color_purple":
                    return new Color(115, 41, 181);
                case "color_brown":
                    return new Color(130, 73, 37);
                case "color_light_cyan":
                    return new Color(180, 255, 255);
                case "color_cyan":
                    return Color.Cyan;
                case "color_aquamarine":
                    return Color.Aquamarine;
                case "color_sea_green":
                    return Color.SeaGreen;
                case "color_lime":
                    return Color.Lime;
                case "color_yellow_green":
                    return Color.GreenYellow;
                case "color_pale_violet_red":
                    return Color.PaleVioletRed;
                case "color_salmon":
                    return new Color(255, 85, 95);
                case "color_jade":
                    return new Color(130, 158, 93);
                case "color_sand":
                    return Color.NavajoWhite;
                case "color_poppyseed":
                    return new Color(82, 47, 153);
                case "color_dark_red":
                    return Color.DarkRed;
                case "color_dark_orange":
                    return Color.DarkOrange;
                case "color_dark_yellow":
                    return Color.DarkGoldenrod;
                case "color_dark_green":
                    return Color.DarkGreen;
                case "color_dark_blue":
                    return Color.DarkBlue;
                case "color_dark_purple":
                    return Color.DarkViolet;
                case "color_dark_pink":
                    return Color.DeepPink;
                case "color_dark_cyan":
                    return Color.DarkCyan;
                case "color_dark_gray":
                    return Color.DarkGray;
                case "color_dark_brown":
                    return Color.SaddleBrown;
                case "color_gold":
                    return Color.Gold;
                case "color_copper":
                    return new Color(179, 85, 0);
                case "color_iron":
                    return new Color(197, 213, 224);
                case "color_iridium":
                    return new Color(105, 15, 255);
            }
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
