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

public sealed record AtlasCtx(ItemSpriteRuleAtlas Atlas, Point TextureSize, Point SpriteSize)
{
    private Texture2D? srcTx;
    private Texture2D? compTx;

    internal bool IsCompTxValid { get; set; } = false;

    public Texture2D GetTexture(IGameContentHelper content)
    {
        srcTx ??= content.Load<Texture2D>(Atlas.ChosenSourceTexture.SourceTextureAsset);
        if (TextureSize == Point.Zero)
        {
            return srcTx;
        }
        if (IsCompTxValid && compTx != null)
        {
            return compTx;
        }

        compTx ??= new(Game1.graphics.GraphicsDevice, TextureSize.X, TextureSize.Y);

        Color[] targetData = new Color[compTx.GetElementCount()];
        Array.Fill(targetData, Color.Transparent);
        ModEntry.LogDebug($"Comp: {compTx.Width}x{compTx.Height}");

        Color[] sourceData = new Color[srcTx.GetElementCount()];
        ModEntry.LogDebug($"Atlas: {Atlas.ChosenSourceTexture.SourceTextureAsset}");
        srcTx.GetData(sourceData, 0, srcTx.GetElementCount());

        foreach (SpriteIndexRule spriteIndexRule in Atlas.Rules)
        {
            for (int i = 0; i < spriteIndexRule.SpriteIndexList.Count; i++)
            {
                int sourceIdx = spriteIndexRule.SpriteIndexList[i];
                int targetIdx = spriteIndexRule.ActualSpriteIndexList[i];
                for (int j = 0; j < Atlas.SourceSpritePerIndex; j++)
                {
                    CopySourceSpriteToTarget(
                        ref sourceData,
                        srcTx.Width,
                        sourceIdx + j,
                        ref targetData,
                        compTx.Width,
                        targetIdx + j
                    );
                }
            }
        }

        compTx.SetData(targetData);
        compTx.Name = Atlas.ChosenSourceTexture.Texture;
        IsCompTxValid = true;

        return compTx;
    }

    private Rectangle GetSourceRectForIndex(int width, int index) =>
        ItemSpriteComp.GetSourceRectForIndex(width, index, SpriteSize);

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
        for (int r = 0; r < SpriteSize.Y; r++)
        {
            int sourceArrayStart = sourceRect.X + (sourceRect.Y + r) * sourceTxWidth;
            int targetArrayStart = targetRect.X + (targetRect.Y + r) * targetTxWidth;
            if (sourceArrayStart + SpriteSize.X > sourceData.Length)
            {
                Array.Fill(targetData, Color.Transparent, targetArrayStart, SpriteSize.X);
            }
            else
            {
                Array.Copy(sourceData, sourceArrayStart, targetData, targetArrayStart, SpriteSize.X);
            }
        }
    }
}

public sealed class ItemSpriteComp(IGameContentHelper content)
{
    internal const string CustomFields_SpritePerIndex = $"{ModEntry.ModId}/SpritePerIndex";
    private const int MAX_WIDTH = 4096;
    private ItemMetadata? metadata = null;
    private IReadOnlyList<AtlasCtx>? atlasCtxList = null;

    private Point spriteSize = new(16, 16);
    private int spritePerIndex = 1;
    internal int baseSpriteIndex = 0;

    internal bool IsDataValid { get; private set; } = false;

    public void UpdateData(ItemMetadata metadata, IReadOnlyList<ItemSpriteRuleAtlas> spriteRuleAtlasList)
    {
        if (metadata.GetParsedData() is not ParsedItemData parsedItemData)
            return;

        this.metadata = metadata;

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

        List<AtlasCtx> newAtlasCtxList = [];

        foreach (ItemSpriteRuleAtlas spriteAtlas in spriteRuleAtlasList)
        {
            if (spriteAtlas.SourceSpritePerIndex == null || spriteAtlas.SourceSpritePerIndex == spritePerIndex)
            {
                foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
                {
                    spriteIndexRule.SpriteIndexList = spriteIndexRule.ActualSpriteIndexList;
                }
                newAtlasCtxList.Add(new(spriteAtlas, Point.Zero, spriteSize));
                continue;
            }

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
            int paddedIdx = spritePerIndex - spriteAtlas.SourceSpritePerIndex.Value;
            for (int i = 0; i < actualIdxKeys.Count; i++)
            {
                remappedIdx[actualIdxKeys[i]] += paddedIdx * i;
            }
            int minIdx = remappedIdx.Values.Min();
            foreach (SpriteIndexRule spriteIndexRule in spriteAtlas.Rules)
            {
                spriteIndexRule.ActualSpriteIndexList.Clear();
                foreach (int idx in spriteIndexRule.SpriteIndexList)
                {
                    spriteIndexRule.ActualSpriteIndexList.Add(remappedIdx[idx] - minIdx);
                }
            }
            int maxIdx = remappedIdx.Values.Max() - minIdx + spriteAtlas.SourceSpritePerIndex.Value + 1;
            newAtlasCtxList.Add(
                new(
                    spriteAtlas,
                    new(Math.Min(maxIdx * spriteSize.X, MAX_WIDTH), ((maxIdx / MAX_WIDTH) + 1) * spriteSize.Y),
                    spriteSize
                )
            );
        }

        this.atlasCtxList = newAtlasCtxList;
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

    private Texture2D? currentTexture = null;
    private Rectangle currentDefaultSourceRect = Rectangle.Empty;

    internal void SetDrawParsedItemData(ParsedItemData parsedItemData, int drawIndex, Texture2D pickedTx)
    {
        if (parsedItemData.IsErrorItem)
            return;

        currentTexture = parsedItemData.GetTexture();
        currentDefaultSourceRect = DynamicMethods.ParsedItemData_get_DefaultSourceRect(parsedItemData);

        DynamicMethods.ParsedItemData_set_Texture(parsedItemData, pickedTx);
        DynamicMethods.ParsedItemData_set_DefaultSourceRect(
            parsedItemData,
            GetSourceRectForIndex(pickedTx.Width, drawIndex)
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

        if (atlasCtxList != null)
        {
            foreach (AtlasCtx atlasCtx in atlasCtxList)
            {
                if (atlasCtx.Atlas.SourceModAsset != null && reloadedModAssets.Contains(atlasCtx.Atlas.SourceModAsset))
                {
                    IsDataValid = false;
                    break;
                }
            }
            foreach (AtlasCtx atlasCtx in atlasCtxList)
            {
                if (
                    atlasCtx.Atlas.ChosenSourceTexture.SourceTextureAsset != null
                    && names.Contains(atlasCtx.Atlas.ChosenSourceTexture.SourceTextureAsset)
                )
                {
                    atlasCtx.IsCompTxValid = false;
                    break;
                }
            }
        }
        return !IsDataValid;
    }

    internal bool CanApplySpriteIndexFromRules => atlasCtxList?.Count > 0;

    internal void DoApplySpriteIndexFromRules(Item item, ItemSpriteIndexHolder holder)
    {
        List<(SpriteIndexRule, AtlasCtx)> validRulesCtx = [];
        foreach (AtlasCtx atlasCtx in this.atlasCtxList!)
        {
            if (atlasCtx.Atlas.SourceModAsset == null || !atlasCtx.Atlas.Enabled)
            {
                continue;
            }
            foreach (SpriteIndexRule spriteIndexRule in atlasCtx.Atlas.Rules)
            {
                if (spriteIndexRule.ValidForItem(item))
                {
                    validRulesCtx.Add((spriteIndexRule, atlasCtx));
                }
            }
        }

        int spriteIndex = -1;
        Texture2D? pickedTx = null;
        if (validRulesCtx.Count > 0)
        {
            int minPrecedence = int.MaxValue;
            (SpriteIndexRule rule, AtlasCtx atlasCtx)? minPrecedencePair = null;
            foreach ((SpriteIndexRule rule, AtlasCtx ctx) ruleCtx in validRulesCtx)
            {
                if (ruleCtx.rule.Precedence < minPrecedence)
                {
                    minPrecedence = ruleCtx.rule.Precedence;
                    minPrecedencePair = ruleCtx;
                }
            }
            if (minPrecedencePair is (SpriteIndexRule rule, AtlasCtx atlasCtx))
            {
                int randIdx = Random.Shared.Next(
                    rule.IncludeDefaultSpriteIndex ? -1 : 0,
                    rule.ActualSpriteIndexList.Count
                );
                if (randIdx >= 0)
                {
                    spriteIndex = rule.ActualSpriteIndexList[randIdx];
                    pickedTx = atlasCtx.GetTexture(content);
                }
            }
        }

        holder.Apply(this, spriteIndex, pickedTx);
    }

    internal void UpdateCompTx(ItemSpriteRuleAtlas ruleAtlas)
    {
        if (atlasCtxList == null)
            return;
        foreach (AtlasCtx atlasCtx in atlasCtxList)
        {
            if (atlasCtx.Atlas == ruleAtlas)
            {
                atlasCtx.IsCompTxValid = false;
            }
        }
    }

    /// Based on https://github.com/Pathoschild/StardewMods/blob/95d695b205199de4bad86770d69a30806d1721a2/ContentPatcher/Framework/Commands/Commands/ExportCommand.cs
    /// MIT License
    #region PATCH_EXPORT
    internal void Export(string exportDir)
    {
        if (metadata != null && atlasCtxList != null)
        {
            string subDir = Path.Combine(exportDir, SanitizePath(metadata.QualifiedItemId));
            Directory.CreateDirectory(subDir);
            File.WriteAllText(
                Path.Combine(subDir, "data.json"),
                JsonConvert.SerializeObject(atlasCtxList, Formatting.Indented)
            );
            ModEntry.Log($"- {subDir}/data.json", LogLevel.Info);
            foreach (AtlasCtx atlasCtx in atlasCtxList)
            {
                string pngName =
                    $"{SanitizePath(string.Concat(Path.GetFileName(atlasCtx.Atlas.SourceModAsset.Name), '-', atlasCtx.Atlas.Key))}.png";
                using Texture2D exported = UnPremultiplyTransparency(atlasCtx.GetTexture(content));
                using Stream stream = File.Create(Path.Combine(exportDir, subDir, pngName));
                exported.SaveAsPng(stream, exported.Width, exported.Height);
                ModEntry.Log($"- {subDir}/{pngName}", LogLevel.Info);
            }
        }
    }

    private static string SanitizePath(string path)
    {
        return string.Join('_', path.Split(Path.GetInvalidFileNameChars()));
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
