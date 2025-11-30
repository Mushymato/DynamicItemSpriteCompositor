using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

internal static class SampleObjectCache
{
    private static readonly Dictionary<string, SObject?> cache = [];

    internal static SObject? GetObject(string itemId)
    {
        if (itemId == null)
            return null;
        string qId = ItemRegistry.QualifyItemId(itemId) ?? itemId;
        if (cache.TryGetValue(qId, out SObject? sobj))
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
        cache[qId] = sobj;
        return sobj;
    }

    internal static void Invalidate()
    {
        cache.Clear();
    }
}
