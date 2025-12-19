using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;

namespace DynamicItemSpriteCompositor.Integration;

#region BAGI
/// <summary>Provides texture and source ingredient information.</summary>
/// <remarks>License on this class is unclear, hopefully within API exception though.</remarks>
internal sealed class BAGIData
{
    public List<string>? Fruits { get; set; } = null;
    public List<string>? Vegetables { get; set; } = null;
    public List<string>? Flowers { get; set; } = null;
    public List<string>? Mushrooms { get; set; } = null;

    public string? Jelly { get; set; } = null;
    public string? Pickles { get; set; } = null;
    public string? Wine { get; set; } = null;
    public string? Juice { get; set; } = null;
    public string? Honey { get; set; } = null;
    public string? DriedMushrooms { get; set; } = null;
}
#endregion

internal sealed class BAGIPackContext(IModHelper helper, IContentPack pack, BAGIData data)
{
    internal IAssetName? ParseBAGIAssetName(string? textureFile)
    {
        if (textureFile == null)
            return null;
        return helper.GameContent.ParseAssetName(
            Path.Combine(ModEntry.ModId, pack.Manifest.UniqueID, "TX", Path.GetFileName(textureFile))
        );
    }

    internal IAssetName? Jelly => field ??= ParseBAGIAssetName(data.Jelly);
    internal IAssetName? Pickles => field ??= ParseBAGIAssetName(data.Pickles);
    internal IAssetName? Wine => field ??= ParseBAGIAssetName(data.Wine);
    internal IAssetName? Juice => field ??= ParseBAGIAssetName(data.Juice);
    internal IAssetName? Honey => field ??= ParseBAGIAssetName(data.Honey);
    internal IAssetName? DriedMushrooms => field ??= ParseBAGIAssetName(data.DriedMushrooms);

    internal IAssetName DataAsset = helper.GameContent.ParseAssetName(
        string.Concat(ModEntry.ModId, "/Data/", pack.Manifest.UniqueID)
    );

    internal IManifest ModManifest => pack.Manifest;

    internal static BAGIPackContext? Make(IModHelper helper, IContentPack contentPack)
    {
        if (contentPack.ReadJsonFile<BAGIData>("data.json") is not BAGIData data)
        {
            return null;
        }
        return new BAGIPackContext(helper, contentPack, data);
    }

    internal void AssetRequested(AssetRequestedEventArgs e)
    {
        if (LoadBAGITexture(e, Jelly, data.Jelly))
            return;
        if (LoadBAGITexture(e, Pickles, data.Pickles))
            return;
        if (LoadBAGITexture(e, Wine, data.Wine))
            return;
        if (LoadBAGITexture(e, Juice, data.Juice))
            return;
        if (LoadBAGITexture(e, Honey, data.Honey))
            return;
        if (LoadBAGITexture(e, DriedMushrooms, data.DriedMushrooms))
            return;
    }

    private bool LoadBAGITexture(AssetRequestedEventArgs e, IAssetName? textureAsset, string? textureFile)
    {
        if (textureFile != null && e.NameWithoutLocale.IsEquivalentTo(textureAsset))
        {
            e.LoadFrom(() => pack.ModContent.Load<Texture2D>(textureFile), AssetLoadPriority.Exclusive);
            return true;
        }
        return false;
    }

    internal Dictionary<string, ItemSpriteRuleAtlas> LoadFromBAGI()
    {
        Dictionary<string, ItemSpriteRuleAtlas> modRuleAtlas = [];
        Dictionary<string, string> nameToId = [];
        foreach ((string itemId, ObjectData objData) in Game1.objectData)
        {
            nameToId[objData.Name] = itemId;
        }
        AddRules(ref modRuleAtlas, ref nameToId, Jelly, data.Fruits, nameof(Jelly), "344");
        AddRules(ref modRuleAtlas, ref nameToId, Pickles, data.Vegetables, nameof(Pickles), "342");
        AddRules(ref modRuleAtlas, ref nameToId, Wine, data.Fruits, nameof(Wine), "348");
        AddRules(ref modRuleAtlas, ref nameToId, Juice, data.Vegetables, nameof(Juice), "350");
        AddRules(ref modRuleAtlas, ref nameToId, Honey, data.Flowers, nameof(Honey), "340");
        AddRules(
            ref modRuleAtlas,
            ref nameToId,
            DriedMushrooms,
            data.Mushrooms,
            nameof(DriedMushrooms),
            "DriedMushrooms"
        );
        return modRuleAtlas;
    }

    private static void AddRules(
        ref Dictionary<string, ItemSpriteRuleAtlas> modRuleAtlas,
        ref Dictionary<string, string> nameToId,
        IAssetName? textureName,
        List<string>? preserves,
        string key,
        string itemId
    )
    {
        if (textureName == null || preserves == null || preserves.Count == 0)
            return;

        List<SpriteIndexRule> rules = [];
        int? configIndex = null;
        string? configIconItem = null;
        for (int i = 0; i < preserves.Count; i++)
        {
            if (!nameToId.TryGetValue(preserves[i], out string? preserveId))
            {
                continue;
            }
            rules.Add(
                new()
                {
                    SpriteIndexList = [i],
                    RequiredContextTags =
                    [
                        "preserve_sheet_index_" + ItemContextTagManager.SanitizeContextTag(preserveId),
                    ],
                }
            );
            configIndex ??= i;
            configIconItem ??= string.Concat("(O)", preserveId);
        }
        if (rules.Count == 0)
            return;

        ItemSpriteRuleAtlas ruleAtlas = new()
        {
            TypeIdentifier = "(O)",
            LocalItemId = itemId,
            SourceSpritePerIndex = 1,
            SourceTextures = [textureName.BaseName],
            Rules = rules,
            ConfigIconSpriteIndex = configIndex,
            ConfigSubIconItemId = configIconItem,
            SubIconScale = 0.5f,
        };
        modRuleAtlas[key] = ruleAtlas;
    }
}

internal sealed class BetterArtisianGoodsCompat
{
    internal readonly List<BAGIPackContext> packs = [];

    internal BetterArtisianGoodsCompat(IModHelper helper)
    {
        foreach (IContentPack contentPack in helper.ContentPacks.GetOwned())
        {
            if (BAGIPackContext.Make(helper, contentPack) is BAGIPackContext bagiCtx)
            {
                ModEntry.Log(
                    $"BAGI -> DISCO shim applied for {contentPack.Manifest.Name} ({contentPack.Manifest.UniqueID})",
                    LogLevel.Debug
                );
                packs.Add(bagiCtx);
            }
        }
        if (packs.Any())
            helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        foreach (BAGIPackContext bagiCtx in packs)
        {
            bagiCtx.AssetRequested(e);
        }
    }
}
