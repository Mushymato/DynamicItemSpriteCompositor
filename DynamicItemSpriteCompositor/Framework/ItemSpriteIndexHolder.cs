using Microsoft.Xna.Framework;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ItemSpriteIndexHolder()
{
    internal ItemSpriteComp? Comp { get; private set; } = null;
    internal bool NeedReapplyNextDraw = false;
    private int realIndex = -1;

    private int pickedIndex = -1;
    private readonly WeakReference<AtlasCtx?> pickedAtlasRef = new(null);

    internal static ItemSpriteIndexHolder Make(Item item) => new();

    internal void Apply(ItemSpriteComp comp, int pickedIndex, AtlasCtx? pickedAtlas)
    {
        Comp = comp;
        this.pickedIndex = pickedIndex;
        this.pickedAtlasRef.SetTarget(pickedAtlas);
    }

    internal void SetDrawParsedItemData(Item item)
    {
        if (
            realIndex != -1
            || pickedIndex == -1
            || Comp == null
            || !pickedAtlasRef.TryGetTarget(out AtlasCtx? pickedAtlas)
            || pickedAtlas == null
        )
            return;
        realIndex = item.ParentSheetIndex;
        int drawIndex = pickedIndex + realIndex - Comp.baseSpriteIndex;
        Comp.SetDrawParsedItemData(ItemRegistry.GetData(item.QualifiedItemId), drawIndex, pickedAtlas.GetTexture());
        item.ParentSheetIndex = drawIndex;
        return;
    }

    internal bool UnsetDrawParsedItemData(Item item)
    {
        if (
            realIndex == -1
            || pickedIndex == -1
            || Comp == null
            || !pickedAtlasRef.TryGetTarget(out AtlasCtx? pickedAtlas)
            || pickedAtlas == null
        )
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
        if (DynamicMethods.Item_get_contextTagsDirty(obj))
        {
            NeedReapplyNextDraw = false;
            // make sure context tags become not dirty
            obj.GetContextTags();
            return true;
        }
        if (NeedReapplyNextDraw)
        {
            NeedReapplyNextDraw = false;
            return true;
        }
        return false;
    }

    internal bool TryGetSubIconDraw(out float scale, out Vector2 offset)
    {
        scale = 0f;
        offset = Vector2.Zero;
        if (!pickedAtlasRef.TryGetTarget(out AtlasCtx? pickedAtlas) || pickedAtlas == null)
        {
            return false;
        }
        scale = pickedAtlas.Atlas.SubIconScale;
        offset = pickedAtlas.Atlas.SubIconOffset;
        return scale > 0f;
    }
}
