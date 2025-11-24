using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ItemSpriteIndexHolder()
{
    internal ItemSpriteComp? Comp { get; private set; } = null;
    internal bool NeedReapplyNextDraw = false;
    private int spriteIndexPicked = 0;
    private int spriteIndexReal = -1;

    internal static ItemSpriteIndexHolder Make(Item item) => new();

    internal void Apply(ItemSpriteComp comp, int pickedIndex)
    {
        Comp = comp;
        spriteIndexPicked = pickedIndex;
    }

    internal void SetDrawParsedItemData(Item item)
    {
        if (spriteIndexReal != -1 || Comp == null)
            return;
        spriteIndexReal = item.ParentSheetIndex;
        int drawIndex = spriteIndexPicked + spriteIndexReal - Comp.baseSpriteIndex;
        Comp.SetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId), drawIndex);
        item.ParentSheetIndex = drawIndex;
        return;
    }

    internal bool UnsetDrawParsedItemData(Item item)
    {
        if (spriteIndexReal == -1 || Comp == null)
            return false;
        Comp.UnsetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId));
        item.ParentSheetIndex = spriteIndexReal;
        spriteIndexReal = -1;
        return true;
    }

    internal bool ShouldSetDrawParsedItemData()
    {
        return spriteIndexReal == -1 && Comp != null && Comp.CanApplySpriteIndexFromRules;
    }

    internal bool NeedReapply(SObject obj)
    {
        if (NeedReapplyNextDraw || DynamicMethods.Item_get_contextTagsDirty(obj))
        {
            NeedReapplyNextDraw = false;
            return true;
        }
        return false;
    }
}
