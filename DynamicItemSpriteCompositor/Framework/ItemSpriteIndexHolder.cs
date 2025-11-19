using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ItemSpriteIndexHolder(WeakReference<Item> ItemRef) : IDisposable
{
    private int spriteIndexPicked = 0;
    private int spriteIndexBase = 0;
    private int spriteIndexChanged = 0;
    internal int SpriteIndex => spriteIndexPicked + spriteIndexChanged - spriteIndexBase;

    internal static ItemSpriteIndexHolder Make(Item item) => new(new WeakReference<Item>(item));

    internal void Apply(int pickedIndex, int baseIndex, bool resetSpriteIndex)
    {
        if (resetSpriteIndex)
        {
            spriteIndexBase = baseIndex;
            spriteIndexChanged = baseIndex;
        }
        spriteIndexPicked = pickedIndex;
    }

    internal void Change(int newSpriteIndex)
    {
        spriteIndexChanged += newSpriteIndex - SpriteIndex;
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
