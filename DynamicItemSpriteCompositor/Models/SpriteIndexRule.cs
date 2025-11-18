using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Models;

[Flags]
internal enum ValidForResult
{
    None = 0b000,
    Item = 0b001,
    HeldObj = 0b010,
    Preserve = 0b100,
}

public class SpriteIndexRequirements
{
    [JsonConverter(typeof(ContextTagSetConverter))]
    public List<string>? RequiredContextTags { get; set; } = null;

    [JsonConverter(typeof(StringColorConverter))]
    public Color? RequiredColor { get; set; } = null;
    public string? RequiredCondition { get; set; } = null;
    internal int PrecedenceMod
    {
        get
        {
            if (RequiredContextTags != null)
            {
                return -100;
            }
            if (RequiredColor != null)
            {
                return -50;
            }
            if (RequiredCondition != null)
            {
                return -20;
            }
            return 0;
        }
    }
}

public sealed class SpriteIndexRule : SpriteIndexRequirements
{
    [JsonConverter(typeof(StringIntListConverter))]
    public List<int> SpriteIndexList { get; set; } = [];
    internal List<int> ActualSpriteIndexList { get; set; } = [];
    public bool IncludeDefaultSpriteIndex { get; set; } = false;

    public SpriteIndexRequirements? HeldObject;
    public SpriteIndexRequirements? Preserve;

    private int? precedence = null;
    public int Precedence
    {
        get
        {
            if (precedence == null)
            {
                precedence =
                    100 + PrecedenceMod + ((HeldObject?.PrecedenceMod ?? 0) / 10) + (Preserve?.PrecedenceMod ?? 0) / 5;
            }
            return precedence.Value;
        }
        set => precedence = value;
    }

    internal static bool CheckReqValidForItem(SpriteIndexRequirements req, Item item)
    {
        if (req.RequiredColor != null && (item is not ColoredObject cObj || req.RequiredColor != cObj.color.Value))
        {
            return false;
        }
        if (req.RequiredContextTags != null && !req.RequiredContextTags.All(item.HasContextTag))
        {
            return false;
        }
        if (req.RequiredCondition != null && GameStateQuery.CheckConditions(req.RequiredCondition, targetItem: item))
        {
            return false;
        }
        return true;
    }

    internal ValidForResult ValidForItem(Item item)
    {
        if (!CheckReqValidForItem(this, item))
        {
            return ValidForResult.None;
        }
        ValidForResult result = ValidForResult.Item;

        SObject? preserveObj = null;
        if (HeldObject != null)
        {
            if (item is not SObject obj || obj.heldObject.Value is not SObject heldObj)
            {
                return ValidForResult.None;
            }
            if (!CheckReqValidForItem(HeldObject, heldObj))
            {
                return ValidForResult.None;
            }
            result |= ValidForResult.HeldObj;
            preserveObj = obj;
        }

        if (Preserve != null)
        {
            if (
                (preserveObj ?? item) is not SObject obj
                || obj.preservedParentSheetIndex.Value is not string preserveQId
            )
            {
                return ValidForResult.None;
            }
            if (!CheckReqValidForItem(Preserve, ItemRegistry.Create(preserveQId)))
            {
                return ValidForResult.None;
            }
            result |= ValidForResult.Preserve;
        }

        return result;
    }
}
