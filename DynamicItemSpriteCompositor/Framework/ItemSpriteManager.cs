using System.Diagnostics.CodeAnalysis;
using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed class ItemSpriteManager
{
    private readonly IModHelper helper;

    internal readonly List<IAssetName> orderedValidModAssets = [];
    private readonly Dictionary<IAssetName, Dictionary<string, ItemSpriteRuleAtlas>?> modDataAssets = [];
    private readonly Dictionary<string, ItemSpriteComp> itemToAtlas = [];
    internal const string TxPrefix = $"{ModEntry.ModId}@Tx";

    private readonly HashSet<IAssetName> willInvalidateIn1Tick = [];
    private readonly HashSet<string> needSpriteIndexRecheckIn2Tick = [];
    private readonly HashSet<WeakReference<Item>> willApplyDynamicSpriteIn1Tick = [];

    internal void PushItemToApplyDynamicSpriteIn1Tick(Item item) => willApplyDynamicSpriteIn1Tick.Add(new(item));

    internal ItemSpriteManager(IModHelper helper)
    {
        this.helper = helper;
        foreach (IModInfo info in helper.ModRegistry.GetAll())
        {
            if (info.Manifest.Dependencies.Any(dep => dep.UniqueID.EqualsIgnoreCase(ModEntry.ModId) && dep.IsRequired))
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
        this.helper.Events.GameLoop.DayStarted += OnDayStarted;
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
        foreach (ItemSpriteComp itemSpriteComp in itemToAtlas.Values)
        {
            itemSpriteComp.Export(helper, exportDir);
        }
    }

    internal bool TryGetItemSpriteCompForQualifiedItemId(
        string qualifiedItemId,
        [NotNullWhen(true)] out ItemSpriteComp? itemSpriteComp
    )
    {
        itemSpriteComp = null;
        if (
            ItemRegistry.ResolveMetadata(qualifiedItemId) is not ItemMetadata itemMetadata
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

        if (!itemToAtlas.TryGetValue(qualifiedItemId, out itemSpriteComp))
        {
            itemSpriteComp = new ItemSpriteComp(helper.GameContent);
            itemToAtlas[qualifiedItemId] = itemSpriteComp;
        }

        if (itemSpriteComp.IsItemDataValid && itemSpriteComp.IsSpriteRuleAtlasValid)
        {
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

        if (!itemSpriteComp.IsItemDataValid)
        {
            itemSpriteComp.UpdateItemMetadataAndSpriteAtlas(itemMetadata, combinedRules);
        }
        else
        {
            itemSpriteComp.UpdateSpriteAtlas(combinedRules);
        }

        return true;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (modDataAssets.ContainsKey(e.NameWithoutLocale))
        {
            ModEntry.LogDebug($"Load '{e.Name}' for content pack");
            e.LoadFrom(() => new Dictionary<string, ItemSpriteRuleAtlas>(), AssetLoadPriority.Exclusive);
        }
        else if (e.Name.IsDirectlyUnderPath(TxPrefix))
        {
            string[] parts = e.Name.BaseName.Split('/');
            if (parts.Length < 2)
                return;
            string qId = parts[1];
            if (TryGetItemSpriteCompForQualifiedItemId(qId, out ItemSpriteComp? itemSpriteComp))
            {
                e.LoadFrom(itemSpriteComp.Load, AssetLoadPriority.Exclusive);
                e.Edit(itemSpriteComp.Edit, AssetEditPriority.Late);
            }
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
            if (name.IsEquivalentTo("Data/BigCraftables"))
            {
                dataBigCraftablesInvalidated = true;
            }
            else if (modDataAssets.ContainsKey(name))
            {
                modDataAssets[name] = null;
                reloadedModAssets.Add(name);
            }
        }
        foreach ((string key, ItemSpriteComp itemSpriteComp) in itemToAtlas)
        {
            if (itemSpriteComp.AssetName is not IAssetName assetName)
            {
                continue;
            }
            if (willInvalidateIn1Tick.Contains(assetName))
            {
                continue;
            }
            if (
                itemSpriteComp.CheckInvalidate(
                    reloadedModAssets,
                    dataObjectInvalidated,
                    dataBigCraftablesInvalidated,
                    e.Names
                )
            )
            {
                willInvalidateIn1Tick.Add(assetName);
                if (itemSpriteComp.HasWatchedItems)
                {
                    needSpriteIndexRecheckIn2Tick.Add(key);
                }
            }
        }
    }

    private void OnUpdatedTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (willInvalidateIn1Tick.Any())
        {
            foreach (IAssetName name in willInvalidateIn1Tick)
            {
                helper.GameContent.InvalidateCache(name);
            }
            willInvalidateIn1Tick.Clear();
        }
        else if (needSpriteIndexRecheckIn2Tick.Any())
        {
            foreach (string key in needSpriteIndexRecheckIn2Tick)
            {
                if (TryGetItemSpriteCompForQualifiedItemId(key, out ItemSpriteComp? itemSpriteComp))
                {
                    itemSpriteComp.RecheckWatchedItems();
                }
                else
                {
                    itemToAtlas.Remove(key);
                }
            }
            needSpriteIndexRecheckIn2Tick.Clear();
        }
        else if (willApplyDynamicSpriteIn1Tick.Any())
        {
            foreach (WeakReference<Item> itemRef in willApplyDynamicSpriteIn1Tick)
            {
                if (itemRef.TryGetTarget(out Item? item))
                {
                    ApplyDynamicSpriteIndex(item);
                }
            }
            willApplyDynamicSpriteIn1Tick.Clear();
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        itemToAtlas.Clear();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        foreach (ItemSpriteComp itemSpriteComp in itemToAtlas.Values)
        {
            itemSpriteComp.ClearWatchedItems();
        }
        Utility.ForEachItem(item =>
        {
            ApplyDynamicSpriteIndex(item);
            return true;
        });
    }

    private void OnSaveLoaded_ModDisabled(object? sender, SaveLoadedEventArgs e)
    {
        Utility.ForEachItem(item =>
        {
            item.ResetParentSheetIndex();
            return true;
        });
    }

    internal void ApplyDynamicSpriteIndex(Item item, bool watch = true)
    {
        if (!TryGetItemSpriteCompForQualifiedItemId(item.QualifiedItemId, out ItemSpriteComp? itemSpriteComp))
        {
            return;
        }
        if (itemSpriteComp.TryApplySpriteIndexFromRules(item, out int? spriteIndex))
        {
            itemSpriteComp.WatchItem(item, spriteIndex.Value);
        }
    }
}
