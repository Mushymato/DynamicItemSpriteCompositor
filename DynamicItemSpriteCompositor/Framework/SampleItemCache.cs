using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal static class SampleObjectCache
{
    private static readonly Dictionary<string, SObject?> cache = [];

    internal static SObject? GetObject(string itemId)
    {
        if (itemId == null)
            return null;
        if (cache.TryGetValue(ItemRegistry.QualifyItemId(itemId) ?? itemId, out SObject? sobj))
        {
            return sobj;
        }
        try
        {
            sobj = ItemRegistry.Create<SObject>(itemId, allowNull: true);
        }
        catch (InvalidCastException)
        {
            sobj = null;
        }
        cache[itemId] = sobj;
        return sobj;
    }

    internal static void Invalidate()
    {
        cache.Clear();
    }
}
