using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ItemSpriteIndexHolder()
{
    internal ItemSpriteComp? Comp { get; private set; } = null;
    internal bool NeedReapplyNextDraw = false;
    private int realIndex = -1;

    private int pickedIndex = -1;
    private Func<Texture2D>? pickedTxGetter = null;

    internal static ItemSpriteIndexHolder Make(Item item) => new();

    internal void Apply(ItemSpriteComp comp, int pickedIndex, Func<Texture2D>? pickedTxGetter)
    {
        Comp = comp;
        this.pickedIndex = pickedIndex;
        this.pickedTxGetter = pickedTxGetter;
    }

    internal void SetDrawParsedItemData(Item item)
    {
        if (realIndex != -1 || pickedIndex == -1 || Comp == null || pickedTxGetter == null)
            return;
        realIndex = item.ParentSheetIndex;
        int drawIndex = pickedIndex + realIndex - Comp.baseSpriteIndex;
        Comp.SetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId), drawIndex, pickedTxGetter());
        item.ParentSheetIndex = drawIndex;
        return;
    }

    internal bool UnsetDrawParsedItemData(Item item)
    {
        if (realIndex == -1 || pickedIndex == -1 || Comp == null || pickedTxGetter == null)
            return false;
        Comp.UnsetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId));
        item.ParentSheetIndex = realIndex;
        realIndex = -1;
        return true;
    }

    internal bool ShouldSetDrawParsedItemData()
    {
        return realIndex == -1 && Comp != null && Comp.CanApplySpriteIndexFromRules;
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
