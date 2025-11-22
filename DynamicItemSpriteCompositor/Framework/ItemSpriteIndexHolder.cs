using Netcode;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

#pragma warning disable AvoidNetField // Avoid Netcode types when possible
internal sealed record ItemSpriteIndexHolder()
#if !MP_DESYNC
    : IDisposable
#endif
{
    private int spriteIndexPicked = 0;
    private int spriteIndexBase = 0;
    private int spriteIndexChanged = 0;
    internal int SpriteIndex => spriteIndexPicked + spriteIndexChanged - spriteIndexBase;

#if MP_DESYNC
    internal static ItemSpriteIndexHolder Make(Item item) => new();

    internal void Change(Item item, int newSpriteIndex)
    {
        int diff = newSpriteIndex - SpriteIndex;
        spriteIndexChanged += diff;
        item.parentSheetIndex.Value += diff;
    }
#else
    private Item? watchedItem;

    internal static ItemSpriteIndexHolder Make(Item item)
    {
        ItemSpriteIndexHolder holder = new() { watchedItem = item };
        holder.watchedItem.parentSheetIndex.fieldChangeVisibleEvent += holder.OnParentSheetIndexChanged;
        return holder;
    }

    public void Dispose()
    {
        if (watchedItem != null)
        {
            watchedItem.parentSheetIndex.fieldChangeVisibleEvent -= OnParentSheetIndexChanged;
            watchedItem.parentSheetIndex.Value = spriteIndexChanged;
            watchedItem = null!;
        }
    }

    private void OnParentSheetIndexChanged(NetInt field, int oldValue, int newValue)
    {
        int diff = newValue - oldValue;
        spriteIndexChanged += diff;
        SetParentSheetIndex(SpriteIndex);
    }

    private void SetParentSheetIndex(int spriteIndex)
    {
        if (watchedItem == null)
            return;
        watchedItem.parentSheetIndex.fieldChangeVisibleEvent -= OnParentSheetIndexChanged;
        watchedItem.parentSheetIndex.Value = spriteIndex;
        watchedItem.parentSheetIndex.fieldChangeVisibleEvent += OnParentSheetIndexChanged;
    }

    public void Unapply()
    {
        SetParentSheetIndex(spriteIndexChanged);
    }

    public void Reapply()
    {
        SetParentSheetIndex(SpriteIndex);
    }
#endif

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
#if MP_DESYNC
                item.parentSheetIndex.Value = baseIndex;
#endif
            }
        }
        spriteIndexPicked = pickedIndex;
#if !MP_DESYNC
        SetParentSheetIndex(SpriteIndex);
#endif
    }
}
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
