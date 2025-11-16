using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DynamicItemSpriteCompositor.Models;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

public sealed class ItemSpriteComp(IGameContentHelper content)
{
    private const int MAX_WIDTH = 4096;
    private ItemMetadata? metadata = null;
    private IReadOnlyList<ItemSpriteRuleAtlas>? spriteRuleAtlasList = null;

    private Point textureSize;
    private Point spriteSize = new(16, 16);
    private int spritePerIndex = 1;
    private IAssetName? assetName = null;
    internal IAssetName? AssetName => assetName;
    private IAssetName? baseTextureAsset = null;

    internal bool IsItemDataValid => metadata != null;
    internal bool IsSpriteRuleAtlasValid => spriteRuleAtlasList != null;
    private bool loggedInvalid = false;

    public void UpdateItemMetadataAndSpriteAtlas(
        ItemMetadata metadata,
        IReadOnlyList<ItemSpriteRuleAtlas> spriteRuleAtlasList
    )
    {
        if (metadata.GetParsedData() is not ParsedItemData parsedItemData)
            return;

        this.metadata = metadata;
        this.baseTextureAsset = content.ParseAssetName(parsedItemData.TextureName);
        this.assetName = content.ParseAssetName(
            string.Concat(ItemSpriteManager.TxPrefix, '/', metadata.QualifiedItemId)
        );

        this.spritePerIndex = 1;
        switch (this.metadata.TypeIdentifier)
        {
            case "(BC)":
                this.spriteSize = new(16, 32);
                break;
            case "(O)":
                if (Game1.objectData.TryGetValue(metadata.LocalItemId, out ObjectData? objectData))
                {
                    this.spritePerIndex = objectData.ColorOverlayFromNextIndex ? 2 : 1;
                }
                goto default;
            default:
                this.spriteSize = new(16, 16);
                break;
        }
        UpdateSpriteAtlas(spriteRuleAtlasList);
        OverrideParsedItemDataTexture(parsedItemData);
    }

    public void UpdateSpriteAtlas(IReadOnlyList<ItemSpriteRuleAtlas> spriteRuleAtlasList)
    {
        int maxIdx = spritePerIndex;
        int extraIdx = spritePerIndex - 1;
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            int localMinIdx = int.MaxValue;
            int localMaxIdx = 0;
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                if (spriteIndexRule.SpriteIndexList.Count == 0)
                    continue;
                localMinIdx = Math.Min(localMinIdx, spriteIndexRule.SpriteIndexList.Min());
                localMaxIdx = Math.Max(localMaxIdx, spriteIndexRule.SpriteIndexList.Max());
            }
            spriteAtlas.LocalMinIndex = localMinIdx;
            spriteAtlas.LocalMaxIndex = localMaxIdx;
            spriteAtlas.BaseIndex = maxIdx - localMinIdx;
            maxIdx += localMaxIdx - localMinIdx + extraIdx + 1;
        }
        textureSize = new(Math.Min(maxIdx * spriteSize.X, MAX_WIDTH), ((maxIdx / MAX_WIDTH) + 1) * spriteSize.Y);
        this.spriteRuleAtlasList = spriteRuleAtlasList;
    }

    internal void FixAdditionalMetadata(ItemMetadata metadata)
    {
        if (spriteRuleAtlasList?.Count == 0)
            return;
        if (metadata.LocalItemId != metadata.LocalItemId || metadata.TypeIdentifier != metadata.TypeIdentifier)
            return;
        if (metadata.GetParsedData() is not ParsedItemData parsedItemData)
            return;

        OverrideParsedItemDataTexture(parsedItemData);
    }

    private static readonly FieldInfo ParsedItemData_LoadedTexture = AccessTools.DeclaredField(
        typeof(ParsedItemData),
        "LoadedTexture"
    );
    private static readonly FieldInfo ParsedItemData_TextureName = AccessTools.DeclaredField(
        typeof(ParsedItemData),
        nameof(ParsedItemData.TextureName)
    );
    private static readonly FieldInfo ParsedItemData_SpriteIndex = AccessTools.DeclaredField(
        typeof(ParsedItemData),
        nameof(ParsedItemData.SpriteIndex)
    );

    private void OverrideParsedItemDataTexture(ParsedItemData parsedItemData)
    {
        if (assetName == null)
        {
            return;
        }
        ParsedItemData_LoadedTexture?.SetValue(parsedItemData, false);
        ParsedItemData_SpriteIndex?.SetValue(parsedItemData, 0);
        ParsedItemData_TextureName?.SetValue(parsedItemData, assetName.Name);
    }

    private Rectangle GetSourceRectForIndex(int width, int index) =>
        new(index * spriteSize.X % width, index * spriteSize.X / width * spriteSize.Y, spriteSize.X, spriteSize.Y);

    internal Texture2D Load()
    {
        if (assetName == null)
        {
            throw new InvalidDataException("Texture asset name not set");
        }
        ModEntry.LogDebug($"Load: {assetName} {textureSize}");
        return new Texture2D(Game1.graphics.GraphicsDevice, textureSize.X, textureSize.Y) { Name = assetName.Name };
    }

    internal void Edit(IAssetData asset)
    {
        if (metadata == null || spriteRuleAtlasList == null || baseTextureAsset == null)
            return;

        ParsedItemData parsedItemData = metadata.GetParsedData();

        ModEntry.LogDebug($"Edit: {asset.Name} {textureSize}");
        IAssetDataForImage editor = asset.AsImage();

        // vanilla sprite
        ModEntry.LogDebug($"Base: {baseTextureAsset}");
        Texture2D tx = content.Load<Texture2D>(baseTextureAsset);
        int sourceIdx = parsedItemData.SpriteIndex;
        for (int i = 0; i < spritePerIndex; i++)
        {
            Rectangle sourceRect = GetSourceRectForIndex(tx.Width, sourceIdx + i);
            Rectangle targetRect = GetSourceRectForIndex(textureSize.X, i);
            ModEntry.LogDebug($"Copy base[{sourceIdx + i}]{sourceRect} to comp[{i}]{targetRect}");
            editor.PatchImage(tx, sourceRect, targetRect, PatchMode.Replace);
        }

        // mod sprites
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            ModEntry.LogDebug($"Atlas: {spriteAtlas.SourceTextureAsset}");
            tx = content.Load<Texture2D>(spriteAtlas.SourceTextureAsset!);
            for (int i = spriteAtlas.LocalMinIndex; i < spriteAtlas.LocalMaxIndex + spritePerIndex; i++)
            {
                Rectangle sourceRect = GetSourceRectForIndex(tx.Width, i);
                Rectangle targetRect = GetSourceRectForIndex(textureSize.X, spriteAtlas.BaseIndex + i);
                ModEntry.LogDebug($"Copy mod[{i}]{sourceRect} to comp[{spriteAtlas.BaseIndex + i}]{targetRect}");
                editor.PatchImage(tx, sourceRect, targetRect, PatchMode.Replace);
            }
        }
    }

    internal bool CheckInvalidate(
        HashSet<IAssetName> reloadedModAssets,
        bool dataObjectInvalidated,
        bool dataBigCraftablesInvalidated,
        IReadOnlySet<IAssetName> names
    )
    {
        if (
            (dataObjectInvalidated && metadata?.TypeIdentifier == "(O)")
            || (dataBigCraftablesInvalidated && metadata?.TypeIdentifier == "(BC)")
        )
        {
            loggedInvalid = false;
            metadata = null;
            spriteRuleAtlasList = null;
            return true;
        }
        if (spriteRuleAtlasList != null)
        {
            foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
            {
                if (spriteAtlas.SourceModAsset != null && reloadedModAssets.Contains(spriteAtlas.SourceModAsset))
                {
                    spriteRuleAtlasList = null;
                    return true;
                }
            }
            foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
            {
                if (spriteAtlas.SourceTextureAsset != null && names.Contains(spriteAtlas.SourceTextureAsset))
                {
                    return true;
                }
            }
        }
        if (baseTextureAsset != null && names.Contains(baseTextureAsset))
        {
            return true;
        }
        return false;
    }

    internal bool TryApplySpriteIndexFromRules(Item item, [NotNullWhen(true)] out int? spriteIndex)
    {
        spriteIndex = null;
        if (spriteRuleAtlasList == null || assetName == null)
        {
            if (!loggedInvalid)
            {
                ModEntry.Log(
                    $"Tried to get sheet index from invalid ItemSpriteComp for item '{item.QualifiedItemId}'",
                    LogLevel.Error
                );
                loggedInvalid = true;
            }
            return false;
        }
        if (spriteRuleAtlasList.Count == 0)
        {
            return false;
        }

        Dictionary<IAssetName, HashSet<int>> perModPossibleIndicies = [];
        foreach (ItemSpriteRuleAtlas spriteAtlas in this.spriteRuleAtlasList)
        {
            if (spriteAtlas.SourceModAsset == null)
            {
                continue;
            }
            if (!perModPossibleIndicies.TryGetValue(spriteAtlas.SourceModAsset, out HashSet<int>? possibleIndicies))
            {
                possibleIndicies = [];
                perModPossibleIndicies[spriteAtlas.SourceModAsset] = possibleIndicies;
            }
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                if (spriteIndexRule.SpriteIndexList.Count > 0 && spriteIndexRule.ValidForItem(item))
                {
                    foreach (int idx in spriteIndexRule.SpriteIndexList)
                    {
                        possibleIndicies.Add(idx + spriteAtlas.BaseIndex);
                    }
                    if (spriteIndexRule.IncludeDefaultSpriteIndex)
                    {
                        possibleIndicies.Add(0);
                    }
                }
            }
        }

        spriteIndex = 0;
        foreach (IAssetName modAsset in ModEntry.manager.orderedValidModAssets)
        {
            if (
                perModPossibleIndicies.TryGetValue(modAsset, out HashSet<int>? possibleIndicies)
                && possibleIndicies.Count > 0
            )
            {
                spriteIndex = Random.Shared.ChooseFrom(possibleIndicies.ToList());
                break;
            }
        }
        return true;
    }

    /// Based on https://github.com/Pathoschild/StardewMods/blob/95d695b205199de4bad86770d69a30806d1721a2/ContentPatcher/Framework/Commands/Commands/ExportCommand.cs
    /// MIT License
    #region PATCH_EXPORT
    internal void Export(IModHelper helper, string exportDir)
    {
        if (metadata != null && spriteRuleAtlasList?.Count > 0 && AssetName != null)
        {
            string fileName = string.Join('_', metadata.QualifiedItemId.Split(Path.GetInvalidFileNameChars()));
            using Texture2D exported = UnPremultiplyTransparency(helper.GameContent.Load<Texture2D>(AssetName));
            string imagePath = Path.Combine(exportDir, $"{fileName}.png");
            using Stream stream = File.Create(imagePath);
            exported.SaveAsPng(stream, exported.Width, exported.Height);
            helper.Data.WriteJsonFile($"export/{fileName}.json", spriteRuleAtlasList);
            ModEntry.Log($"Exported '{exportDir}/{fileName}.png' and '{exportDir}/{fileName}.json'", LogLevel.Info);
        }
    }

    /// <summary>Reverse premultiplication applied to an image asset by the XNA content pipeline.</summary>
    /// <param name="texture">The texture to adjust.</param>
    private static Texture2D UnPremultiplyTransparency(Texture2D texture)
    {
        Color[] data = new Color[texture.Width * texture.Height];
        texture.GetData(data);

        for (int i = 0; i < data.Length; i++)
        {
            Color pixel = data[i];
            if (pixel.A == 0)
                continue;

            data[i] = new Color(
                (byte)(pixel.R * 255 / pixel.A),
                (byte)(pixel.G * 255 / pixel.A),
                (byte)(pixel.B * 255 / pixel.A),
                pixel.A
            ); // don't use named parameters, which are inconsistent between MonoGame (e.g. 'alpha') and XNA (e.g. 'a')
        }

        Texture2D result = new Texture2D(
            texture.GraphicsDevice ?? Game1.graphics.GraphicsDevice,
            texture.Width,
            texture.Height
        );
        result.SetData(data);
        return result;
    }
    #endregion
}
