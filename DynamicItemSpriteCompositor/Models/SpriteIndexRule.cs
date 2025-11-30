using DynamicItemSpriteCompositor.Framework;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewValley;
using StardewValley.Objects;

namespace DynamicItemSpriteCompositor.Models;

public class SpriteIndexReqs
{
    [JsonConverter(typeof(ContextTagsConverter))]
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

public sealed class SpriteIndexRule : SpriteIndexReqs
{
    private string IdImpl = "";
    public string Id
    {
        get => IdImpl ??= string.Join('|', SpriteIndexList.OrderBy(idx => idx).Select(idx => idx.ToString()));
        set => IdImpl = value;
    }

    [JsonConverter(typeof(StringIntListConverter))]
    public List<int> SpriteIndexList { get; set; } = [];
    public bool IncludeDefaultSpriteIndex { get; set; } = false;

    public SpriteIndexReqs? HeldObject;
    public SpriteIndexReqs? Preserve;

    public List<int> SpriteIndexListAdjusted { get; internal set; } = [];
    private int? precedence = null;
    public int Precedence
    {
        get
        {
            precedence ??=
                100 + PrecedenceMod + ((HeldObject?.PrecedenceMod ?? 0) / 10) + (Preserve?.PrecedenceMod ?? 0) / 5;
            return precedence.Value;
        }
        set => precedence = value;
    }

    internal static bool CheckReqValidForItem(SpriteIndexReqs req, Item item)
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

    internal bool ValidForItem(Item item)
    {
        if (!CheckReqValidForItem(this, item))
        {
            return false;
        }

        SObject? preserveObj = null;
        if (HeldObject != null)
        {
            if (item is not SObject obj || obj.heldObject.Value is not SObject heldObj)
            {
                return false;
            }
            if (!CheckReqValidForItem(HeldObject, heldObj))
            {
                return false;
            }
            preserveObj = obj;
        }

        if (Preserve != null)
        {
            if (
                (preserveObj ?? item) is not SObject obj
                || obj.preservedParentSheetIndex.Value is not string preserveQId
                || SampleObjectCache.GetObject(preserveQId) is not SObject preserveItem
            )
            {
                return false;
            }
            if (!CheckReqValidForItem(Preserve, preserveItem))
            {
                return false;
            }
        }

        return true;
    }
}
