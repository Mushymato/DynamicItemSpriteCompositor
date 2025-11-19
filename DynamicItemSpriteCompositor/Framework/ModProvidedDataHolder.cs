using System.Diagnostics.CodeAnalysis;
using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Extensions;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ModProidedDataHolder(IManifest Mod)
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

    internal bool TryGetModRuleAtlas(
        IGameContentHelper content,
        IAssetName assetName,
        [NotNullWhen(true)] out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
    )
    {
        modRuleAtlas = null;
        if (!IsValid)
        {
            if (!content.DoesAssetExist<Dictionary<string, ItemSpriteRuleAtlas>>(assetName))
            {
                return false;
            }
            // csharpier-ignore
            modRuleAtlas = content.Load<Dictionary<string, ItemSpriteRuleAtlas>>(assetName);
            HashSet<string> previousQIds = Data.Select(kv => kv.Value.QualifiedItemId).ToHashSet();

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
                    ModEntry.Log($"Atlas '{key}' from '{assetName}' has no valid rules, skipping.", LogLevel.Warn);
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
                spriteAtlas.SourceTextureList.RemoveWhere(st =>
                    string.IsNullOrEmpty(st.Texture) || !content.DoesAssetExist<Texture2D>(st.GetAssetName(content))
                );
                if (!spriteAtlas.SourceTextureList.Any())
                {
                    ModEntry.Log($"Atlas '{key}' from '{assetName}' has no source textures, skipping.", LogLevel.Warn);
                    invalidKeys.Add(key);
                    continue;
                }
                spriteAtlas.SourceModAsset = assetName;

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
        }
        else
        {
            modRuleAtlas = this.Data;
        }
        return true;
    }
}
