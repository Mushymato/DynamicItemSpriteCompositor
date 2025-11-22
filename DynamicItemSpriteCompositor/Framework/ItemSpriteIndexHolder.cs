using StardewModdingAPI;
using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

#pragma warning disable AvoidNetField // Avoid Netcode types when possible
internal sealed record ItemSpriteIndexHolder : IDisposable
{
    private const int SpecialParentSheetIndex = -395870000;
    internal static bool IsSaving = false;

    private int spriteIndexPicked = 0;
    private int spriteIndexBase = 0;
    private int spriteIndexChanged = 0;

    internal int SpriteIndex => spriteIndexPicked + spriteIndexChanged - spriteIndexBase;
    private WeakReference<Item> itemRef;

    internal static ItemSpriteIndexHolder Make(Item item) => new(item);

    internal ItemSpriteIndexHolder(Item item)
    {
        this.itemRef = new(item);
        item.parentSheetIndex.fieldChangeVisibleEvent += OnParentSheetIndexChanged;
    }

    private void OnParentSheetIndexChanged(Netcode.NetInt field, int oldValue, int newValue)
    {
        if (IsSaving)
            return;
        if (newValue == SpecialParentSheetIndex)
        {
            spriteIndexChanged = spriteIndexBase;
            SetParentSheetIndexToChanged();
        }
        else if (oldValue != SpecialParentSheetIndex && oldValue != newValue)
        {
            spriteIndexChanged += newValue - SpriteIndex;
        }
    }

    internal bool SetParentSheetIndexToChanged()
    {
        if (itemRef.TryGetTarget(out Item? item))
        {
            item.parentSheetIndex.fieldChangeVisibleEvent -= OnParentSheetIndexChanged;
            item.parentSheetIndex.Value = spriteIndexChanged;
            item.parentSheetIndex.fieldChangeVisibleEvent += OnParentSheetIndexChanged;
            return true;
        }
        return false;
    }

    internal void Change(Item item, int newSpriteIndex)
    {
        int diff = newSpriteIndex - SpriteIndex;
        item.parentSheetIndex.Value += diff;
    }

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
                item.parentSheetIndex.Value = SpecialParentSheetIndex;
            }
        }
        spriteIndexPicked = pickedIndex;
    }

    ~ItemSpriteIndexHolder() => Dispose();

    public void Dispose()
    {
        if (itemRef.TryGetTarget(out Item? item))
        {
            item.parentSheetIndex.fieldChangeVisibleEvent -= OnParentSheetIndexChanged;
            itemRef = null!;
        }
    }
}
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
