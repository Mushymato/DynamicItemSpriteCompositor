using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Models;

public sealed class SpriteIndexRule
{
    [JsonConverter(typeof(ContextTagSetConverter))]
    public List<string>? RequiredContextTags { get; set; } = null;

    [JsonConverter(typeof(StringColorConverter))]
    public Color? RequiredColor { get; set; } = null;
    public string? RequiredCondition { get; set; } = null;

    [JsonConverter(typeof(StringIntListConverter))]
    public List<int> SpriteIndexList { get; set; } = [];
    public bool IncludeDefaultSpriteIndex { get; set; } = false;

    private int? precedence = null;
    public int Precedence
    {
        get
        {
            if (precedence == null)
            {
                precedence = 100;
                if (RequiredColor != null)
                {
                    precedence = -50;
                }
                if (RequiredContextTags != null)
                {
                    precedence = -100;
                }
                if (RequiredCondition != null)
                {
                    precedence = -25;
                }
            }
            return precedence.Value;
        }
        set => precedence = value;
    }

    internal bool ValidForItem(Item item)
    {
        if (RequiredColor != null && (item is not ColoredObject cObj || RequiredColor != cObj.color.Value))
        {
            return false;
        }
        if (RequiredContextTags != null && !RequiredContextTags.All(item.HasContextTag))
        {
            return false;
        }
        if (RequiredCondition != null && GameStateQuery.CheckConditions(RequiredCondition, targetItem: item))
        {
            return false;
        }
        return true;
    }
}
