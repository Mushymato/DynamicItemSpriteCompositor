using System.Diagnostics.CodeAnalysis;
using DynamicItemSpriteCompositor.Integration;
using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ModProidedDataHolder(IAssetName AssetName, IManifest Mod)
{
    internal bool IsValid { get; set; } = false;
    internal Dictionary<string, ItemSpriteRuleAtlas> Data
    {
        get => field;
        set
        {
            field = value;
            IsValid = value != null;
        }
    } = [];

    internal Func<Dictionary<string, ItemSpriteRuleAtlas>> loadFrom = () => [];

    internal bool TryGetModRuleAtlas(
        IGameContentHelper content,
        [NotNullWhen(true)] out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
    )
    {
        modRuleAtlas = null;
        if (IsValid)
        {
            modRuleAtlas = this.Data;
        }
        else
        {
            if (!content.DoesAssetExist<Dictionary<string, ItemSpriteRuleAtlas>>(AssetName))
            {
                return false;
            }
            // csharpier-ignore
            modRuleAtlas = content.Load<Dictionary<string, ItemSpriteRuleAtlas>>(AssetName);
            HashSet<string> previousQIds = Data.Select(kv => kv.Value.QualifiedItemId).ToHashSet();

            Dictionary<string, Dictionary<string, TextureOption>> cpto = ModEntry.config.Data.ContentPackTextureOptions;
            cpto.TryGetValue(Mod.UniqueID, out Dictionary<string, TextureOption>? contentPackOptions);
            bool shouldWriteConfig = false;

            List<string> invalidKeys = [];
            foreach ((string key, ItemSpriteRuleAtlas spriteAtlas) in modRuleAtlas)
            {
                if (
                    string.IsNullOrEmpty(spriteAtlas.TypeIdentifier)
                    || string.IsNullOrWhiteSpace(spriteAtlas.LocalItemId)
                )
                    continue;
                spriteAtlas.Rules.RemoveWhere(rule => rule.SpriteIndexList.Count == 0);
                if (spriteAtlas.Rules.Count == 0)
                {
                    ModEntry.Log($"Atlas '{key}' from '{AssetName}' has no valid rules, skipping.", LogLevel.Warn);
                    invalidKeys.Add(key);
                    continue;
                }
                if (spriteAtlas.SourceSpritePerIndex is int srcSpritePerIdx)
                {
                    if (srcSpritePerIdx < 1)
                    {
                        ModEntry.Log(
                            $"Atlas '{key}' has negative SourceSpritePerIndex={srcSpritePerIdx}.",
                            LogLevel.Warn
                        );
                        invalidKeys.Add(key);
                        continue;
                    }
                    List<int> allIndexes = [];
                    foreach (var rule in spriteAtlas.Rules)
                    {
                        allIndexes.AddRange(rule.SpriteIndexList);
                    }
                    allIndexes.Sort();
                    for (int i = 1; i < allIndexes.Count; i++)
                    {
                        if (allIndexes[i] - allIndexes[i - 1] < srcSpritePerIdx)
                        {
                            ModEntry.Log(
                                $"Atlas '{key}' has SourceSpritePerIndex={srcSpritePerIdx} but contains index {allIndexes[i - 1]} and {allIndexes[i]} with less difference.",
                                LogLevel.Warn
                            );
                            invalidKeys.Add(key);
                            continue;
                        }
                    }
                }

                spriteAtlas.SourceTextureOptions.Clear();
                foreach (string texture in spriteAtlas.SourceTextures)
                {
                    if (string.IsNullOrEmpty(texture))
                    {
                        continue;
                    }
                    if (!Game1.content.DoesAssetExist<Texture2D>(texture))
                    {
                        continue;
                    }
                    spriteAtlas.SourceTextureOptions.Add(new(texture, content.ParseAssetName(texture)));
                }
                if (!spriteAtlas.SourceTextureOptions.Any())
                {
                    ModEntry.Log($"Atlas '{key}' from '{AssetName}' has no source textures, skipping.", LogLevel.Warn);
                    invalidKeys.Add(key);
                    continue;
                }
                spriteAtlas.Key = key;
                spriteAtlas.SourceModAsset = AssetName;

                if (contentPackOptions?.TryGetValue(key, out TextureOption? option) ?? false)
                {
                    spriteAtlas.Enabled = option.Enabled;
                    spriteAtlas.ChosenIdx = spriteAtlas.SourceTextures.IndexOf(option.Texture);
                    if (spriteAtlas.ChosenIdx < 0)
                    {
                        spriteAtlas.ChosenIdx = 0;
                        shouldWriteConfig = true;
                    }
                }

                string thisQId = spriteAtlas.QualifiedItemId;
                if (
                    Context.IsWorldReady
                    && !previousQIds.Contains(thisQId)
                    && ModEntry.manager.qIdToComp.TryGetValue(thisQId, out ItemSpriteComp? comp)
                )
                {
                    comp.ForceInvalidate();
                }
            }
            modRuleAtlas.RemoveWhere(kv => invalidKeys.Contains(kv.Key));
            this.Data = modRuleAtlas;

            if (shouldWriteConfig)
            {
                ModEntry.config.WriteConfig();
            }
        }
        return true;
    }

    internal bool HasDisplayData(IGameContentHelper content)
    {
        return TryGetModRuleAtlas(content, out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas)
            && modRuleAtlas.Any();
    }
}
