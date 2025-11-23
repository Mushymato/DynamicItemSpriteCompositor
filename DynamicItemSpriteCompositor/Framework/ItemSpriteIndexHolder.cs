using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ItemSpriteIndexHolder()
{
    private ItemSpriteComp? Comp { get; set; } = null;
    private int spriteIndexPicked = 0;
    private int spriteIndexReal = 0;

    internal static ItemSpriteIndexHolder Make(Item item) => new();

    internal void Apply(ItemSpriteComp comp, int pickedIndex)
    {
        Comp = comp;
        spriteIndexPicked = pickedIndex;
    }

    internal void SetDrawParsedItemData(Item item)
    {
        if (Comp == null)
            return;
        spriteIndexReal = item.ParentSheetIndex;
        int drawIndex = spriteIndexPicked + spriteIndexReal - Comp.baseSpriteIndex;
        Comp.SetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId), drawIndex);
        item.ParentSheetIndex = drawIndex;
    }

    internal void UnsetDrawParsedItemData(Item item)
    {
        if (Comp == null)
            return;
        Comp.UnsetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId));
        item.ParentSheetIndex = spriteIndexReal;
        spriteIndexReal = -1;
    }
}
