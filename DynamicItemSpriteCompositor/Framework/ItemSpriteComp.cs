using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DynamicItemSpriteCompositor.Models;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
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
    private IAssetName? baseTextureAsset = null;
    private int baseSpriteIndex = 0;

    private const string compTxPrefix = $"{ModEntry.ModId}@TX";
    private readonly Texture2D compTx = new(Game1.graphics.GraphicsDevice, 16, 16)
    {
        Name = $"{compTxPrefix}/PLACEHOLDER",
    };

    internal bool IsCompTxValid { get; private set; } = false;
    internal bool IsDataValid { get; private set; } = false;
    internal bool IsValid => IsDataValid && IsCompTxValid;

    public void UpdateData(ItemMetadata metadata, IReadOnlyList<ItemSpriteRuleAtlas> spriteRuleAtlasList)
    {
        if (metadata.GetParsedData() is not ParsedItemData parsedItemData)
            return;

        this.metadata = metadata;
        this.baseTextureAsset = content.ParseAssetName(parsedItemData.TextureName);

        this.spritePerIndex = 1;
        switch (this.metadata.TypeIdentifier)
        {
            case "(BC)":
                if (Game1.bigCraftableData.TryGetValue(metadata.LocalItemId, out BigCraftableData? bcData))
                {
                    this.baseSpriteIndex = bcData.SpriteIndex;
                }
                this.spriteSize = new(16, 32);
                break;
            case "(O)":
                if (Game1.objectData.TryGetValue(metadata.LocalItemId, out ObjectData? objectData))
                {
                    this.spritePerIndex = objectData.ColorOverlayFromNextIndex ? 2 : 1;
                    this.baseSpriteIndex = objectData.SpriteIndex;
                }
                goto default;
            default:
                this.spriteSize = new(16, 16);
                break;
        }

        int maxIdx = spritePerIndex;
        int extraIdx = spritePerIndex - 1;
        // recomp: comp tx marked invalid/did not have spriteRuleAtlasList before/got different counts
        bool needTextureRecomp =
            !IsCompTxValid
            || this.spriteRuleAtlasList == null
            || this.spriteRuleAtlasList.Count != spriteRuleAtlasList.Count;
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
        Point newTextureSize = new(
            Math.Min(maxIdx * spriteSize.X, MAX_WIDTH),
            ((maxIdx / MAX_WIDTH) + 1) * spriteSize.Y
        );

        // recomp: different texture size
        needTextureRecomp = needTextureRecomp || newTextureSize.X != textureSize.X || newTextureSize.Y != textureSize.Y;
        textureSize = newTextureSize;

        if (!needTextureRecomp)
        {
            // recomp: index reordered/changed
            for (int i = 0; i < this.spriteRuleAtlasList!.Count; i++)
            {
                ItemSpriteRuleAtlas oldISRA = this.spriteRuleAtlasList[i];
                ItemSpriteRuleAtlas newISRA = spriteRuleAtlasList[i];
                if (
                    oldISRA.SourceTextureAsset != newISRA.SourceTextureAsset
                    || oldISRA.BaseIndex != newISRA.BaseIndex
                    || oldISRA.LocalMinIndex != newISRA.LocalMinIndex
                    || oldISRA.LocalMaxIndex != newISRA.LocalMaxIndex
                )
                {
                    needTextureRecomp = true;
                    break;
                }
            }
        }
        this.spriteRuleAtlasList = spriteRuleAtlasList;

        // rebuild the composite texture
        if (needTextureRecomp)
            UpdateCompTx();

        OverrideParsedItemDataTexture(parsedItemData);
        IsDataValid = true;
    }

    private Rectangle GetSourceRectForIndex(int width, int index) =>
        new(index * spriteSize.X % width, index * spriteSize.X / width * spriteSize.Y, spriteSize.X, spriteSize.Y);

    internal void UpdateCompTx()
    {
        if (
            baseTextureAsset == null
            || metadata == null
            || spriteRuleAtlasList == null
            || spriteRuleAtlasList.Count == 0
        )
            return;

        if (compTx.Width < textureSize.X || compTx.Height < textureSize.Y)
        {
            Texture2D tmpTx = new(
                Game1.graphics.GraphicsDevice,
                Math.Max(compTx.Width, textureSize.X),
                Math.Max(compTx.Height, textureSize.Y)
            );
            compTx.CopyFromTexture(tmpTx);
        }
        Color[] targetData = new Color[compTx.GetElementCount()];

        List<Texture2D> sourceTextures = [];
        sourceTextures.Add(content.Load<Texture2D>(baseTextureAsset));
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            sourceTextures.Add(content.Load<Texture2D>(spriteAtlas.SourceTextureAsset!));
        }
        Color[] sourceData = new Color[sourceTextures.Max(tx => tx.GetElementCount())];
        Texture2D sourceTx = sourceTextures[0];

        // vanilla sprite
        ModEntry.LogDebug($"Base: {baseTextureAsset}");
        sourceTx.GetData(sourceData, 0, sourceTx.GetElementCount());
        for (int i = 0; i < spritePerIndex; i++)
        {
            CopySourceSpriteToTarget(
                ref sourceData,
                sourceTx.Width,
                this.baseSpriteIndex + i,
                ref targetData,
                compTx.Width,
                i
            );
        }

        // mod sprites
        int txIdx = 1;
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            ModEntry.LogDebug($"Atlas: {spriteAtlas.SourceTextureAsset}");
            sourceTx = sourceTextures[txIdx];
            txIdx++;
            sourceTx.GetData(sourceData, 0, sourceTx.GetElementCount());
            for (int i = spriteAtlas.LocalMinIndex; i < spriteAtlas.LocalMaxIndex + spritePerIndex; i++)
            {
                CopySourceSpriteToTarget(
                    ref sourceData,
                    sourceTx.Width,
                    i,
                    ref targetData,
                    compTx.Width,
                    spriteAtlas.BaseIndex + i
                );
            }
        }

        compTx.SetData(targetData);
        compTx.Name = $"{compTxPrefix}/{metadata.QualifiedItemId}";
        IsCompTxValid = true;
    }

    private void CopySourceSpriteToTarget(
        ref Color[] sourceData,
        int sourceTxWidth,
        int sourceIdx,
        ref Color[] targetData,
        int targetTxWidth,
        int targetIdx
    )
    {
        Rectangle sourceRect = GetSourceRectForIndex(sourceTxWidth, sourceIdx);
        Rectangle targetRect = GetSourceRectForIndex(targetTxWidth, targetIdx);

        ModEntry.LogDebug($"Copy src[{sourceIdx}]{sourceRect} to comp{targetRect}");

        // r is row, aka y
        // copy the array row by row from source to target
        for (int r = 0; r < spriteSize.Y; r++)
        {
            int sourceArrayStart = sourceRect.X + (sourceRect.Y + r) * sourceTxWidth;
            int targetArrayStart = targetRect.X + (targetRect.Y + r) * targetTxWidth;
            if (sourceArrayStart + spriteSize.X > sourceData.Length)
            {
                Array.Fill(targetData, Color.Transparent, targetArrayStart, spriteSize.X);
            }
            else
            {
                Array.Copy(sourceData, sourceArrayStart, targetData, targetArrayStart, spriteSize.X);
            }
        }
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
    private static readonly FieldInfo ParsedItemData_Texture = AccessTools.DeclaredField(
        typeof(ParsedItemData),
        "Texture"
    );
    private static readonly FieldInfo ParsedItemData_DefaultSourceRect = AccessTools.DeclaredField(
        typeof(ParsedItemData),
        "DefaultSourceRect"
    );

    private void OverrideParsedItemDataTexture(ParsedItemData parsedItemData)
    {
        ParsedItemData_Texture?.SetValue(parsedItemData, compTx);
        ParsedItemData_LoadedTexture?.SetValue(parsedItemData, true);
        ParsedItemData_DefaultSourceRect?.SetValue(parsedItemData, new Rectangle(0, 0, spriteSize.X, spriteSize.Y));
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
            IsDataValid = false;
            metadata = null;
        }

        if (spriteRuleAtlasList != null)
        {
            foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
            {
                if (spriteAtlas.SourceModAsset != null && reloadedModAssets.Contains(spriteAtlas.SourceModAsset))
                {
                    IsDataValid = false;
                    break;
                }
            }
            foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
            {
                if (spriteAtlas.SourceTextureAsset != null && names.Contains(spriteAtlas.SourceTextureAsset))
                {
                    IsCompTxValid = false;
                    break;
                }
            }
        }
        if (baseTextureAsset != null && names.Contains(baseTextureAsset))
        {
            IsCompTxValid = false;
        }
        return !IsValid;
    }

    internal bool TryApplySpriteIndexFromRules(Item item, [NotNullWhen(true)] out int? spriteIndex)
    {
        spriteIndex = null;
        if (spriteRuleAtlasList == null)
        {
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
    internal void Export(string exportDir)
    {
        if (metadata != null && spriteRuleAtlasList?.Count > 0)
        {
            string fileName = string.Join('_', metadata.QualifiedItemId.Split(Path.GetInvalidFileNameChars()));
            using Texture2D exported = UnPremultiplyTransparency(compTx);
            using Stream stream = File.Create(Path.Combine(exportDir, $"{fileName}.png"));
            exported.SaveAsPng(stream, exported.Width, exported.Height);
            string jsonStr = JsonConvert.SerializeObject(spriteRuleAtlasList);
            File.WriteAllText(Path.Combine(exportDir, $"{fileName}.json"), jsonStr);
            ModEntry.Log($"- {fileName}.(png|json)", LogLevel.Info);
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

        Texture2D result = new(texture.GraphicsDevice ?? Game1.graphics.GraphicsDevice, texture.Width, texture.Height);
        result.SetData(data);
        return result;
    }
    #endregion
}
