using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed record ItemSpriteIndexHolder(Item Item)
{
    internal int SpriteIndex { get; set; }

    internal static ItemSpriteIndexHolder Make(Item item) => new(item);
}

internal sealed class ItemSpriteManager
{
    private readonly IModHelper helper;

    internal readonly List<IAssetName> orderedValidModAssets = [];
    private readonly Dictionary<IAssetName, Dictionary<string, ItemSpriteRuleAtlas>?> modDataAssets = [];
    private readonly Dictionary<string, ItemSpriteComp> qIdToComp = [];

    private readonly HashSet<string> needItemSpriteCompRecheck = [];
    private readonly List<WeakReference<Item>> needApplyDynamicSpriteIndex = [];
    private readonly ConditionalWeakTable<Item, ItemSpriteIndexHolder> watchedItems = [];
    internal int ParentSheetIndexUsageCount = 0;

    internal readonly Dictionary<string, int>? SpecialSpritesPerIndex;

    internal void AddToNeedApplyDynamicSpriteIndex(Item item)
    {
        if (EnsureItemSpriteCompForQualifiedItemId(item.QualifiedItemId))
        {
            needApplyDynamicSpriteIndex.Add(new(item));
        }
    }

    internal ItemSpriteManager(IModHelper helper)
    {
        this.helper = helper;
        SpecialSpritesPerIndex = this.helper.Data.ReadJsonFile<Dictionary<string, int>>(
            "assets/special_sprite_per_index.json"
        );
        foreach (IModInfo info in helper.ModRegistry.GetAll())
        {
            if (
                info.Manifest.Dependencies.Any(dep => dep.UniqueID.EqualsIgnoreCase(ModEntry.ModId) && dep.IsRequired)
                || (
                    info.Manifest.ExtraFields.TryGetValue(ModEntry.ModId, out object? specialReq)
                    && specialReq is bool specialReqBool
                    && specialReqBool
                )
            )
            {
                IAssetName modAssetName = helper.GameContent.ParseAssetName(
                    string.Concat(ModEntry.ModId, "/Data/", info.Manifest.UniqueID)
                );
                orderedValidModAssets.Add(modAssetName);
                modDataAssets[modAssetName] = null;
                ModEntry.Log($"Tracking '{modAssetName}' asset for '{info.Manifest.UniqueID}'");
            }
        }
        orderedValidModAssets.Reverse();

        if (modDataAssets.Count == 0)
        {
            ModEntry.Log(
                $"No content packs detected, mod is disabled. All sprite indexes will be reset once a save is loaded."
            );
            this.helper.Events.GameLoop.SaveLoaded += OnSaveLoaded_ModDisabled;
            return;
        }

        this.helper.Events.Content.AssetRequested += OnAssetRequested;
        this.helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        this.helper.Events.GameLoop.UpdateTicked += OnUpdatedTicked;
        this.helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        this.helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;

        helper.ConsoleCommands.Add(
            "disco-export",
            "Export the current composite sprites and rule atlas info.",
            ConsoleExport
        );

        Patches.Register();
    }

    private void ConsoleExport(string arg1, string[] arg2)
    {
        string exportDir = Path.Combine(helper.DirectoryPath, "export");
        Directory.CreateDirectory(exportDir);
        ModEntry.Log($"Export to '{exportDir}':", LogLevel.Info);
        foreach (ItemSpriteComp itemSpriteComp in qIdToComp.Values)
        {
            itemSpriteComp.Export(exportDir);
        }
    }

    private static ItemMetadata? SafeResolveMetadata(string qualifiedItemId)
    {
        Patches.ItemMetadata_SetTypeDefinition_Postfix_Enabled = false;
        ItemMetadata metadata = ItemRegistry.ResolveMetadata(qualifiedItemId);
        Patches.ItemMetadata_SetTypeDefinition_Postfix_Enabled = true;
        return metadata;
    }

    internal bool TryGetItemSpriteCompForQualifiedItemId(
        string qualifiedItemId,
        [NotNullWhen(true)] out ItemSpriteComp? itemSpriteComp
    )
    {
        itemSpriteComp = null;
        if (
            SafeResolveMetadata(qualifiedItemId) is not ItemMetadata itemMetadata
            || itemMetadata.QualifiedItemId == null
        )
        {
            return false;
        }
        qualifiedItemId = itemMetadata.QualifiedItemId;

        // TODO support '(O)' and '(BC)' only for now
        if (itemMetadata.TypeIdentifier != "(O)" && itemMetadata.TypeIdentifier != "(BC)")
        {
            return false;
        }

        if (!qIdToComp.TryGetValue(qualifiedItemId, out itemSpriteComp))
        {
            itemSpriteComp = new ItemSpriteComp(helper.GameContent);
            qIdToComp[qualifiedItemId] = itemSpriteComp;
        }

        if (itemSpriteComp.IsValid)
        {
            itemSpriteComp.FixAdditionalMetadata(itemMetadata);
            return true;
        }

        if (itemSpriteComp.IsDataValid && !itemSpriteComp.IsCompTxValid)
        {
            itemSpriteComp.UpdateCompTx();
            itemSpriteComp.FixAdditionalMetadata(itemMetadata);
            return true;
        }

        List<ItemSpriteRuleAtlas> combinedRules = [];
        foreach ((IAssetName assetName, Dictionary<string, ItemSpriteRuleAtlas>? ruleAtlasDict) in modDataAssets)
        {
            Dictionary<string, ItemSpriteRuleAtlas> currentRuleAtlas;
            if (ruleAtlasDict == null)
            {
                if (!helper.GameContent.DoesAssetExist<Dictionary<string, ItemSpriteRuleAtlas>>(assetName))
                {
                    continue;
                }
                // csharpier-ignore
                currentRuleAtlas = helper.GameContent.Load<Dictionary<string, ItemSpriteRuleAtlas>>(assetName);

                List<string> invalidKeys = [];
                foreach ((string key, ItemSpriteRuleAtlas spriteAtlas) in currentRuleAtlas)
                {
                    spriteAtlas.Rules.RemoveWhere(rule => rule.SpriteIndexList.Count == 0);
                    if (spriteAtlas.Rules.Count == 0)
                    {
                        ModEntry.Log($"Atlas '{key}' from '{assetName}' has no valid rules, skipping.", LogLevel.Warn);
                        invalidKeys.Add(key);
                        continue;
                    }
                    if (string.IsNullOrEmpty(spriteAtlas.SourceTexture))
                    {
                        ModEntry.Log(
                            $"Atlas '{key}' from '{assetName}' has no source texture, skipping.",
                            LogLevel.Warn
                        );
                        invalidKeys.Add(key);
                        continue;
                    }
                    spriteAtlas.SourceModAsset = assetName;
                }
                currentRuleAtlas.RemoveWhere(kv => invalidKeys.Contains(kv.Key));
                modDataAssets[assetName] = currentRuleAtlas;
            }
            else
            {
                currentRuleAtlas = ruleAtlasDict;
            }

            foreach (ItemSpriteRuleAtlas ruleAtlas in currentRuleAtlas.Values)
            {
                if (
                    helper.GameContent.DoesAssetExist<Texture2D>(ruleAtlas.GetAssetName(helper.GameContent))
                    && ruleAtlas.TypeIdentifier == itemMetadata.TypeIdentifier
                    && ruleAtlas.LocalItemId == itemMetadata.LocalItemId
                )
                {
                    combinedRules.Add(ruleAtlas);
                }
            }
        }
        itemSpriteComp.UpdateData(itemMetadata, combinedRules);

        return true;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (modDataAssets.ContainsKey(e.NameWithoutLocale))
        {
            ModEntry.LogDebug($"Load '{e.Name}' for content pack");
            e.LoadFrom(() => new Dictionary<string, ItemSpriteRuleAtlas>(), AssetLoadPriority.Exclusive);
        }
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        bool dataObjectInvalidated = false;
        bool dataBigCraftablesInvalidated = false;
        HashSet<IAssetName> reloadedModAssets = [];
        foreach (IAssetName name in e.NamesWithoutLocale)
        {
            if (name.IsEquivalentTo("Data/Objects"))
            {
                dataObjectInvalidated = true;
            }
            if (name.IsEquivalentTo("Data/BigCraftables") || name.IsEquivalentTo("Data/Machines"))
            {
                dataBigCraftablesInvalidated = true;
            }
            else if (modDataAssets.ContainsKey(name))
            {
                modDataAssets[name] = null;
                reloadedModAssets.Add(name);
            }
        }
        foreach ((string key, ItemSpriteComp itemSpriteComp) in qIdToComp)
        {
            if (
                itemSpriteComp.CheckInvalidate(
                    reloadedModAssets,
                    dataObjectInvalidated,
                    dataBigCraftablesInvalidated,
                    e.Names
                )
            )
            {
                needItemSpriteCompRecheck.Add(key);
            }
        }
    }

    private void OnUpdatedTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (needItemSpriteCompRecheck.Any())
        {
            foreach (string key in needItemSpriteCompRecheck)
            {
                if (TryGetItemSpriteCompForQualifiedItemId(key, out ItemSpriteComp? itemSpriteComp))
                {
                    foreach ((Item item, _) in watchedItems)
                    {
                        if (item.QualifiedItemId == key)
                        {
                            ApplyDynamicSpriteIndex(item);
                        }
                    }
                }
                else
                {
                    qIdToComp.Remove(key);
                }
            }
            needItemSpriteCompRecheck.Clear();
        }
        else if (needApplyDynamicSpriteIndex.Any())
        {
            foreach (WeakReference<Item> itemRef in needApplyDynamicSpriteIndex)
            {
                if (itemRef.TryGetTarget(out Item? item))
                {
                    ApplyDynamicSpriteIndex(item);
                }
            }
            needApplyDynamicSpriteIndex.Clear();
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        Utility.ForEachItem(item =>
        {
            ApplyDynamicSpriteIndex(item);
            return true;
        });
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        watchedItems.Clear();
        qIdToComp.Clear();
    }

    private void OnSaveLoaded_ModDisabled(object? sender, SaveLoadedEventArgs e)
    {
        Utility.ForEachItem(item =>
        {
            item.ResetParentSheetIndex();
            return true;
        });
    }

    internal void ApplyDynamicSpriteIndex(Item item)
    {
        if (TryGetItemSpriteCompForQualifiedItemId(item.QualifiedItemId, out ItemSpriteComp? itemSpriteComp))
        {
            if (itemSpriteComp.TryApplySpriteIndexFromRules(item, out int? spriteIndex))
            {
                if (spriteIndex.Value > 0)
                    watchedItems.GetValue(item, ItemSpriteIndexHolder.Make).SpriteIndex = spriteIndex.Value;
                return;
            }
        }
        watchedItems.Remove(item);
    }

    internal int? GetSpriteIndex(Item item)
    {
        if (watchedItems.TryGetValue(item, out ItemSpriteIndexHolder? holder))
        {
            return holder.SpriteIndex;
        }
        return null;
    }

    private bool EnsureItemSpriteCompForQualifiedItemId(string qualifiedItemId)
    {
        return TryGetItemSpriteCompForQualifiedItemId(qualifiedItemId, out _);
    }

    internal void FixAdditionalMetadata(ItemMetadata metadata)
    {
        if (
            metadata.QualifiedItemId != null
            && qIdToComp.TryGetValue(metadata.QualifiedItemId, out ItemSpriteComp? itemSpriteComp)
        )
        {
            itemSpriteComp.FixAdditionalMetadata(metadata);
        }
    }
}
