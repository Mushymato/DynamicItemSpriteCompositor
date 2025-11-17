using Netcode;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

#pragma warning disable AvoidNetField // Avoid Netcode types when possible
internal sealed class ItemSpriteIndexWatcher : IDisposable
{
    private Item? item;
    private readonly int priorSpriteIndex;
    internal int SpriteIndex
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                if (item != null)
                {
                    item.parentSheetIndex.fieldChangeEvent -= SuppressParentSheetIndexChanges;
                    item.parentSheetIndex.Value = value;
                    item.parentSheetIndex.fieldChangeEvent += SuppressParentSheetIndexChanges;
                }
            }
        }
    }

    internal static ItemSpriteIndexWatcher Make(Item item) => new(item);

    public ItemSpriteIndexWatcher(Item item)
    {
        this.item = item;
        priorSpriteIndex = item.ParentSheetIndex;
        item.parentSheetIndex.fieldChangeEvent += SuppressParentSheetIndexChanges;
    }

    private void SuppressParentSheetIndexChanges(NetInt field, int oldValue, int newValue)
    {
        if (newValue != SpriteIndex && item != null)
        {
            field.fieldChangeEvent -= SuppressParentSheetIndexChanges;
            field.Value = SpriteIndex;
            field.fieldChangeEvent += SuppressParentSheetIndexChanges;
        }
    }

    ~ItemSpriteIndexWatcher() => Dispose();

    public void Dispose()
    {
        if (item != null)
        {
            item.parentSheetIndex.fieldChangeEvent -= SuppressParentSheetIndexChanges;
            item.ParentSheetIndex = priorSpriteIndex;
            item = null!;
        }
        GC.SuppressFinalize(this);
    }
}
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
