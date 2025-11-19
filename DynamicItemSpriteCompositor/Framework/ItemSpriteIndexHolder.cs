using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

#pragma warning disable AvoidNetField // Avoid Netcode types when possible
internal sealed record ItemSpriteIndexHolder()
{
    private int spriteIndexPicked = 0;
    private int spriteIndexBase = 0;
    private int spriteIndexChanged = 0;
    internal int SpriteIndex => spriteIndexPicked + spriteIndexChanged - spriteIndexBase;

    internal static ItemSpriteIndexHolder Make(Item item) => new();

    internal void Apply(Item item, int pickedIndex, int baseIndex, bool resetSpriteIndex, bool isSaveLoaded)
    {
        if (resetSpriteIndex || isSaveLoaded)
        {
            spriteIndexBase = baseIndex;
            if (isSaveLoaded)
            {
                spriteIndexChanged = item.parentSheetIndex.Value;
            }
            else
            {
                spriteIndexChanged = baseIndex;
                item.parentSheetIndex.Value = baseIndex;
            }
        }
        spriteIndexPicked = pickedIndex;
    }

    internal void Change(Item item, int newSpriteIndex)
    {
        int diff = newSpriteIndex - SpriteIndex;
        spriteIndexChanged += diff;
        item.parentSheetIndex.Value += diff;
    }
}
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
