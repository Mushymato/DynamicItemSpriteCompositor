using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Models;

public class SpriteIndexRequirement
{
    [JsonConverter(typeof(ContextTagSetConverter))]
    public List<string>? RequiredContextTags { get; set; } = null;

    [JsonConverter(typeof(StringColorConverter))]
    public Color? RequiredColor { get; set; } = null;
    public string? RequiredCondition { get; set; } = null;

    internal bool BaseValidForItem(Item item)
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

public sealed class SpriteIndexRule : SpriteIndexRequirement
{
    public SpriteIndexRequirement? HeldObject;

    [JsonConverter(typeof(StringIntListConverter))]
    public List<int> SpriteIndexList { get; set; } = [];
    public bool IncludeDefaultSpriteIndex { get; set; } = false;

    internal bool ValidForItem(Item item)
    {
        if (!BaseValidForItem(item))
        {
            return false;
        }
        if (HeldObject != null && item is SObject obj && obj.heldObject.Value is SObject heldObj)
        {
            if (!HeldObject.BaseValidForItem(heldObj))
            {
                return false;
            }
        }
        return true;
    }
}
