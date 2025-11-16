using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed class ItemSpriteIndexWatcher(Item item) : IDisposable
{
    private int spriteIndex = 0;
    internal int SpriteIndex
    {
        get => spriteIndex;
        set
        {
            spriteIndex = value;
            item.ParentSheetIndex = value;
        }
    }

    internal static ItemSpriteIndexWatcher Make(Item item) => new(item);

    ~ItemSpriteIndexWatcher() => Dispose();

    public void Dispose()
    {
        if (item != null)
        {
            item = null!;
        }
        GC.SuppressFinalize(this);
    }
}
