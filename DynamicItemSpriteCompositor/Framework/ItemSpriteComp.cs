using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

public sealed class ItemSpriteComp(IGameContentHelper content)
{
    internal const string CustomFields_SpritePerIndex = $"{ModEntry.ModId}/SpritePerIndex";
    private const int MAX_WIDTH = 4096;
    private ItemMetadata? metadata = null;
    private IReadOnlyList<ItemSpriteRuleAtlas>? spriteRuleAtlasList = null;

    private Point textureSize;
    private Point spriteSize = new(16, 16);
    private int spritePerIndex = 1;
    private IAssetName? baseTextureAsset = null;
    internal int baseSpriteIndex = 0;

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
                    if (
                        (bcData.CustomFields?.TryGetValue(CustomFields_SpritePerIndex, out string? spiStr) ?? false)
                        && int.TryParse(spiStr, out int spi)
                    )
                    {
                        this.spritePerIndex = spi;
                    }
                    else if (bcData.Name?.Contains("Seasonal") ?? false)
                    {
                        // TODO: this actually has more funny % 4 logic to it, not gonna deal with it for now
                        this.spritePerIndex = Math.Max(spritePerIndex, 4);
                    }
                }
                if (
                    DataLoader
                        .Machines(Game1.content)
                        .TryGetValue(metadata.QualifiedItemId, out MachineData? machineData)
                )
                {
                    if (machineData.ShowNextIndexWhileWorking || machineData.ShowNextIndexWhenReady)
                    {
                        this.spritePerIndex = Math.Max(this.spritePerIndex, 2);
                    }
                    CheckMachineEffectsSpritePerIndex(machineData.LoadEffects);
                    CheckMachineEffectsSpritePerIndex(machineData.WorkingEffects);
                    if (machineData.OutputRules != null)
                    {
                        IEnumerable<int> incrementIndex = machineData
                            .OutputRules.SelectMany(outputRule => outputRule.OutputItem)
                            .Select(itemOutput => itemOutput.IncrementMachineParentSheetIndex);
                        if (incrementIndex.Any())
                        {
                            this.spritePerIndex += incrementIndex.Max();
                        }
                    }
                }
                this.spriteSize = new(16, 32);
                break;
            case "(O)":
                if (Game1.objectData.TryGetValue(metadata.LocalItemId, out ObjectData? objectData))
                {
                    this.baseSpriteIndex = objectData.SpriteIndex;
                    if (
                        (objectData.CustomFields?.TryGetValue(CustomFields_SpritePerIndex, out string? spiStr) ?? false)
                        && int.TryParse(spiStr, out int spi)
                    )
                    {
                        this.spritePerIndex = spi;
                    }
                    else
                    {
                        this.spritePerIndex = Math.Max(
                            this.spritePerIndex,
                            objectData.ColorOverlayFromNextIndex ? 2 : 1
                        );
                    }
                }
                goto default;
            default:
                this.spriteSize = new(16, 16);
                break;
        }

        int maxIdx = 0;
        // recomp: comp tx marked invalid/did not have spriteRuleAtlasList before/got different counts
        bool needTextureRecomp =
            !IsCompTxValid
            || this.spriteRuleAtlasList == null
            || this.spriteRuleAtlasList.Count != spriteRuleAtlasList.Count;
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            Dictionary<int, int> remappedIdx = [];
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                foreach (int idx in spriteIndexRule.SpriteIndexList)
                {
                    remappedIdx[idx] = idx;
                }
            }
            List<int> actualIdxKeys = remappedIdx.Keys.ToList();
            actualIdxKeys.Sort();
            // first pass: put in the padding idx
            int paddedIdx = spritePerIndex - (spriteAtlas.SourceSpritePerIndex ?? spritePerIndex);
            for (int i = 0; i < actualIdxKeys.Count; i++)
            {
                remappedIdx[actualIdxKeys[i]] += paddedIdx * i;
            }
            int baseIdx = maxIdx - remappedIdx.Values.Min();
            // second pass: put in the baseIdx
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                spriteIndexRule.ActualSpriteIndexList.Clear();
                foreach (int idx in spriteIndexRule.SpriteIndexList)
                {
                    int finalIdx = baseIdx + remappedIdx[idx];
                    spriteIndexRule.ActualSpriteIndexList.Add(finalIdx);
                    maxIdx = Math.Max(maxIdx, finalIdx + spritePerIndex);
                }
            }
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
                if (oldISRA.ChosenSourceTexture.SourceTextureAsset != newISRA.ChosenSourceTexture.SourceTextureAsset)
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

        IsDataValid = true;
    }

    private void CheckMachineEffectsSpritePerIndex(List<MachineEffects> machineEffects)
    {
        if (machineEffects == null)
        {
            return;
        }
        foreach (MachineEffects effects in machineEffects)
        {
            if (effects.Frames?.Count > 0)
                this.spritePerIndex = Math.Max(this.spritePerIndex, effects.Frames.Max());
        }
    }

    internal Rectangle GetSourceRectForIndex(int width, int index) => GetSourceRectForIndex(width, index, spriteSize);

    internal static Rectangle GetSourceRectForIndex(int width, int index, Point spriteSize) =>
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
        Array.Fill(targetData, Color.Transparent);
        ModEntry.LogDebug($"Comp: {compTx.Width}x{compTx.Height}");

        List<Texture2D> sourceTextures = [];
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            sourceTextures.Add(content.Load<Texture2D>(spriteAtlas.ChosenSourceTexture.SourceTextureAsset));
        }
        Color[] sourceData = new Color[sourceTextures.Max(tx => tx.GetElementCount())];
        Texture2D sourceTx = sourceTextures[0];

        int txIdx = 0;
        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            ModEntry.LogDebug($"Atlas: {spriteAtlas.ChosenSourceTexture.SourceTextureAsset}");
            sourceTx = sourceTextures[txIdx];
            txIdx++;
            sourceTx.GetData(sourceData, 0, sourceTx.GetElementCount());
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                for (int i = 0; i < spriteIndexRule.SpriteIndexList.Count; i++)
                {
                    int sourceIdx = spriteIndexRule.SpriteIndexList[i];
                    int targetIdx = spriteIndexRule.ActualSpriteIndexList[i];
                    for (int j = 0; j < (spriteAtlas.SourceSpritePerIndex ?? spritePerIndex); j++)
                    {
                        CopySourceSpriteToTarget(
                            ref sourceData,
                            sourceTx.Width,
                            sourceIdx + j,
                            ref targetData,
                            compTx.Width,
                            targetIdx + j
                        );
                    }
                }
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

        ModEntry.LogDebug($"Copy src[{sourceIdx}]{sourceRect} to comp[{targetIdx}]{targetRect}");

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

    private Texture2D? currentTexture = null;
    private Rectangle currentDefaultSourceRect = Rectangle.Empty;

    internal void SetDrawParsedItemData(ParsedItemData parsedItemData, int drawIndex)
    {
        if (parsedItemData.IsErrorItem)
            return;

        currentTexture = parsedItemData.GetTexture();
        currentDefaultSourceRect = DynamicMethods.ParsedItemData_get_DefaultSourceRect(parsedItemData);

        DynamicMethods.ParsedItemData_set_Texture(parsedItemData, compTx);
        DynamicMethods.ParsedItemData_set_DefaultSourceRect(
            parsedItemData,
            GetSourceRectForIndex(textureSize.X, drawIndex)
        );
        DynamicMethods.ParsedItemData_set_SpriteIndex(parsedItemData, drawIndex);
    }

    internal void UnsetDrawParsedItemData(ParsedItemData parsedItemData)
    {
        if (parsedItemData.IsErrorItem)
            return;

        DynamicMethods.ParsedItemData_set_Texture(parsedItemData, currentTexture);
        DynamicMethods.ParsedItemData_set_DefaultSourceRect(parsedItemData, currentDefaultSourceRect);
        currentTexture = null;
        currentDefaultSourceRect = Rectangle.Empty;

        DynamicMethods.ParsedItemData_set_SpriteIndex(parsedItemData, baseSpriteIndex);
    }

    internal void ForceInvalidate()
    {
        IsDataValid = false;
        IsCompTxValid = false;
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
                if (
                    spriteAtlas.ChosenSourceTexture.SourceTextureAsset != null
                    && names.Contains(spriteAtlas.ChosenSourceTexture.SourceTextureAsset)
                )
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

    internal bool CanApplySpriteIndexFromRules => spriteRuleAtlasList?.Count > 0;

    internal void DoApplySpriteIndexFromRules(Item item, ItemSpriteIndexHolder holder)
    {
        List<SpriteIndexRule> validRules = [];
        foreach (ItemSpriteRuleAtlas spriteAtlas in this.spriteRuleAtlasList!)
        {
            if (spriteAtlas.SourceModAsset == null || !spriteAtlas.Enabled)
            {
                continue;
            }
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                if (spriteIndexRule.ValidForItem(item))
                {
                    validRules.Add(spriteIndexRule);
                }
            }
        }

        int spriteIndex = -1;
        if (validRules.Count > 0)
        {
            int minPrecedence = int.MaxValue;
            SpriteIndexRule? minPrecedenceRule = null;
            foreach (SpriteIndexRule rule in validRules)
            {
                if (rule.Precedence < minPrecedence)
                {
                    minPrecedence = rule.Precedence;
                    minPrecedenceRule = rule;
                }
            }
            if (minPrecedenceRule != null)
            {
                int randIdx = Random.Shared.Next(
                    minPrecedenceRule.IncludeDefaultSpriteIndex ? -1 : 0,
                    minPrecedenceRule.ActualSpriteIndexList.Count
                );
                if (randIdx >= 0)
                {
                    spriteIndex = minPrecedenceRule.ActualSpriteIndexList[randIdx];
                }
            }
        }

        holder.Apply(this, spriteIndex);
    }

    /// Based on https://github.com/Pathoschild/StardewMods/blob/95d695b205199de4bad86770d69a30806d1721a2/ContentPatcher/Framework/Commands/Commands/ExportCommand.cs
    /// MIT License
    #region PATCH_EXPORT
    internal void Export(string exportDir)
    {
        if (metadata != null)
        {
            string fileName = string.Join('_', metadata.QualifiedItemId.Split(Path.GetInvalidFileNameChars()));
            using Texture2D exported = UnPremultiplyTransparency(compTx);
            using Stream stream = File.Create(Path.Combine(exportDir, $"{fileName}.png"));
            exported.SaveAsPng(stream, exported.Width, exported.Height);
            string jsonStr = JsonConvert.SerializeObject(spriteRuleAtlasList, Formatting.Indented);
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
