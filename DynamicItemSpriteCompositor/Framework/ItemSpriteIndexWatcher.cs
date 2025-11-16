using Netcode;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

#pragma warning disable AvoidNetField // Avoid Netcode types when possible
internal sealed class ItemSpriteIndexWatcher : IDisposable
{
    private WeakReference<Item>? itemRef;
    private readonly int priorSpriteIndex;
    private int spriteIndex = 0;
    internal int SpriteIndex
    {
        get => spriteIndex;
        set
        {
            spriteIndex = value;
            if (itemRef?.TryGetTarget(out Item? item) ?? false)
            {
                item.ParentSheetIndex = spriteIndex;
            }
            else
            {
                Dispose();
            }
        }
    }

    internal static ItemSpriteIndexWatcher Make(Item item) => new(item);

    public ItemSpriteIndexWatcher(Item item)
    {
        this.itemRef = new WeakReference<Item>(item);
        priorSpriteIndex = item.ParentSheetIndex;
        item.parentSheetIndex.fieldChangeEvent += SuppressParentSheetIndexChanges;
    }

    private void SuppressParentSheetIndexChanges(NetInt field, int oldValue, int newValue)
    {
        if (newValue != spriteIndex && (itemRef?.TryGetTarget(out Item? item) ?? false))
        {
            field.fieldChangeEvent -= SuppressParentSheetIndexChanges;
            field.Value = spriteIndex;
            field.fieldChangeEvent += SuppressParentSheetIndexChanges;
        }
    }

    ~ItemSpriteIndexWatcher() => Dispose();

    public void Dispose()
    {
        if (itemRef != null)
        {
            if (itemRef.TryGetTarget(out Item? item))
            {
                ModEntry.Log($"Stop watching {item.QualifiedItemId} {GetHashCode()}");
                item.parentSheetIndex.fieldChangeEvent -= SuppressParentSheetIndexChanges;
                item.ParentSheetIndex = priorSpriteIndex;
            }
            itemRef = null!;
        }
        GC.SuppressFinalize(this);
    }
}
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
