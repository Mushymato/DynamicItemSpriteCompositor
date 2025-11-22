using StardewValley;

namespace DynamicItemSpriteCompositor.Framework;

#pragma warning disable AvoidNetField // Avoid Netcode types when possible
internal sealed record ItemSpriteIndexHolder
{
    private const string ModData_RandSeed = $"{ModEntry.ModId}/RandSeed";
    private int spriteIndexPicked = 0;
    private int spriteIndexBase = 0;
    private int spriteIndexChanged = 0;
    internal Random Rand { get; private set; }

    internal int SpriteIndex => spriteIndexPicked + spriteIndexChanged - spriteIndexBase;

    internal static ItemSpriteIndexHolder Make(Item item) => new(item);

    internal ItemSpriteIndexHolder(Item item)
    {
        if (
            item.modData.TryGetValue(ModData_RandSeed, out string randSeedStr)
            && int.TryParse(randSeedStr, out int randSeed)
        )
        {
            Rand = new Random(randSeed);
        }
        else
        {
            randSeed = Random.Shared.Next();
            Rand = new Random(randSeed);
            item.modData[ModData_RandSeed] = randSeed.ToString();
        }
    }

    internal void Change(Item item, int newSpriteIndex)
    {
        int diff = newSpriteIndex - SpriteIndex;
        spriteIndexChanged += diff;
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
                item.parentSheetIndex.Value = baseIndex;
            }
        }
        spriteIndexPicked = pickedIndex;
    }
}
#pragma warning restore AvoidNetField // Avoid Netcode types when possible
