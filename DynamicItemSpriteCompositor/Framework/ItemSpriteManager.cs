using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DynamicItemSpriteCompositor.Models;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
using StardewValley.ItemTypeDefinitions;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed class ItemSpriteManager
{
    private readonly IModHelper helper;

    internal readonly Dictionary<IAssetName, ModProidedDataHolder> modDataAssets;
    internal readonly Dictionary<string, ItemSpriteComp> qIdToComp = [];

    private readonly HashSet<string> needItemSpriteCompRecheck = [];
    private readonly ConditionalWeakTable<Item, ItemSpriteIndexHolder> watchedItems = [];

    internal static ItemSpriteManager? Make(IModHelper helper)
    {
        Dictionary<IAssetName, ModProidedDataHolder> modDataAssets = [];
        foreach (IModInfo info in helper.ModRegistry.GetAll())
        {
            if (info.Manifest.Dependencies.Any(dep => dep.UniqueID.EqualsIgnoreCase(ModEntry.ModId)))
            {
                IAssetName modAssetName = helper.GameContent.ParseAssetName(
                    string.Concat(ModEntry.ModId, "/Data/", info.Manifest.UniqueID)
                );
                modDataAssets[modAssetName] = new(modAssetName, info.Manifest);
                ModEntry.Log($"Tracking '{modAssetName}' asset for '{info.Manifest.UniqueID}'");
            }
        }

        if (modDataAssets.Count == 0)
        {
            ModEntry.Log("No content packs detected, mod is disabled.", LogLevel.Warn);
            return null;
        }

        return new(helper, modDataAssets);
    }

    internal ItemSpriteManager(IModHelper helper, Dictionary<IAssetName, ModProidedDataHolder> modDataAssets)
    {
        this.helper = helper;
        this.modDataAssets = modDataAssets;

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

        helper.ConsoleCommands.Add(
            "disco-recheck",
            "Recheck dynamic sprites on every item in the world.",
            ConsoleRecheck
        );
    }

    private void ConsoleExport(string arg1, string[] arg2)
    {
        string exportDir = Path.Combine(helper.DirectoryPath, "export");
        Directory.CreateDirectory(exportDir);
        ModEntry.Log($"Export to '{exportDir}':", LogLevel.Info);
        ReloadAllItemSpriteComp();
        foreach (ItemSpriteComp itemSpriteComp in qIdToComp.Values)
        {
            itemSpriteComp.Export(exportDir);
        }
    }

    private void ConsoleRecheck(string arg1, string[] arg2)
    {
        if (Context.IsWorldReady)
        {
            RecheckAllDynamicSpriteIndex();
        }
    }

    internal void UpdateCompTxForQId(string qualifiedItemId, bool enabledStatusChanged)
    {
        if (TryGetItemSpriteCompForQualifiedItemId(qualifiedItemId, out ItemSpriteComp? itemSpriteComp))
        {
            itemSpriteComp.UpdateCompTx();
            if (enabledStatusChanged)
            {
                foreach ((Item item, _) in watchedItems)
                {
                    if (item.QualifiedItemId == qualifiedItemId)
                    {
                        ApplyDynamicSpriteIndex(item, itemSpriteComp, out _);
                    }
                }
            }
        }
        else
        {
            RemoveWatchedItemsByQId(qualifiedItemId);
        }
    }

    private void RemoveWatchedItemsByQId(string qualifiedItemId)
    {
        List<Item> matchingWatchedItems = [];
        foreach ((Item item, _) in watchedItems)
        {
            if (item.QualifiedItemId == qualifiedItemId)
            {
                matchingWatchedItems.Add(item);
            }
        }
        foreach (Item item in matchingWatchedItems)
        {
            watchedItems.Remove(item);
        }
    }

    internal static ItemMetadata? SafeResolveMetadata(string qualifiedItemId)
    {
        Patches.ItemMetadata_SetTypeDefinition_Postfix_Enabled = false;
        ItemMetadata metadata = ItemRegistry.ResolveMetadata(qualifiedItemId);
        Patches.ItemMetadata_SetTypeDefinition_Postfix_Enabled = true;
        return metadata;
    }

    // internal static string SafeGetQualifiedItemId(Item item)
    // {
    //     Patches.ItemMetadata_ParentSheetIndex_Enabled = false;
    //     string qualifiedItemId = item.QualifiedItemId;
    //     Patches.ItemMetadata_ParentSheetIndex_Enabled = true;
    //     return qualifiedItemId;
    // }

    private bool TryGetItemSpriteCompForQualifiedItemId(
        string qualifiedItemId,
        [NotNullWhen(true)] out ItemSpriteComp? itemSpriteComp,
        bool makeNewIfNotFound = false
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
            if (makeNewIfNotFound)
            {
                itemSpriteComp = new ItemSpriteComp(helper.GameContent);
                qIdToComp[qualifiedItemId] = itemSpriteComp;
            }
            else
            {
                return false;
            }
        }

        if (itemSpriteComp.IsValid)
        {
            // itemSpriteComp.FixAdditionalMetadata(itemMetadata);
            return true;
        }

        if (itemSpriteComp.IsDataValid && !itemSpriteComp.IsCompTxValid)
        {
            itemSpriteComp.UpdateCompTx();
            // itemSpriteComp.FixAdditionalMetadata(itemMetadata);
            return true;
        }

        List<ItemSpriteRuleAtlas> combinedRules = [];
        foreach (ModProidedDataHolder dataHolder in modDataAssets.Values)
        {
            if (
                !dataHolder.TryGetModRuleAtlas(
                    helper.GameContent,
                    out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
                )
            )
            {
                continue;
            }

            foreach (ItemSpriteRuleAtlas ruleAtlas in modRuleAtlas.Values)
            {
                if (
                    ruleAtlas.TypeIdentifier == itemMetadata.TypeIdentifier
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
            e.LoadFrom(() => new Dictionary<string, ItemSpriteRuleAtlas>(), AssetLoadPriority.Exclusive);
        }
        else if (e.Name.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(Edit_BC_SpritesPerIndex_Defaults, AssetEditPriority.Early);
        }
    }

    private void Edit_BC_SpritesPerIndex_Defaults(IAssetData asset)
    {
        IDictionary<string, BigCraftableData> bcData = asset.AsDictionary<string, BigCraftableData>().Data;
        AddSpritesPerIndex(bcData, "130", "6"); // Chest
        AddSpritesPerIndex(bcData, "165", "2"); // Auto-Grabber
        AddSpritesPerIndex(bcData, "216", "3"); // Mini-Fridge
        AddSpritesPerIndex(bcData, "232", "6"); // Stone Chest
        AddSpritesPerIndex(bcData, "248", "6"); // Mini-Shipping Bin
        AddSpritesPerIndex(bcData, "256", "6"); // Junimo Chest
        AddSpritesPerIndex(bcData, "275", "3"); // Hopper
        AddSpritesPerIndex(bcData, "BigChest", "6");
        AddSpritesPerIndex(bcData, "BigStoneChest", "6");
    }

    private static void AddSpritesPerIndex(IDictionary<string, BigCraftableData> bcData, string id, string num)
    {
        bcData[id].CustomFields ??= [];
        bcData[id].CustomFields[ItemSpriteComp.CustomFields_SpritePerIndex] = num;
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
                SampleObjectCache.Invalidate();
            }
            else if (name.IsEquivalentTo("Data/BigCraftables"))
            {
                dataBigCraftablesInvalidated = true;
                SampleObjectCache.Invalidate();
            }
            else if (name.IsEquivalentTo("Data/Machines"))
            {
                dataBigCraftablesInvalidated = true;
            }
            else if (modDataAssets.ContainsKey(name))
            {
                modDataAssets[name].IsValid = false;
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
                if (
                    TryGetItemSpriteCompForQualifiedItemId(
                        key,
                        out ItemSpriteComp? itemSpriteComp,
                        makeNewIfNotFound: true
                    )
                )
                {
                    foreach ((Item item, _) in watchedItems)
                    {
                        if (item.QualifiedItemId == key)
                        {
                            ApplyDynamicSpriteIndex(item, itemSpriteComp, out _);
                        }
                    }
                }
                else
                {
                    qIdToComp.Remove(key);
                    RemoveWatchedItemsByQId(key);
                }
            }
            needItemSpriteCompRecheck.Clear();
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        ReloadAllItemSpriteComp();
        RecheckAllDynamicSpriteIndex();
    }

    private void ReloadAllItemSpriteComp()
    {
        ModEntry.config.LoadContentPackTextureOptions(this.modDataAssets.Values);
        foreach (ModProidedDataHolder dataHolder in modDataAssets.Values)
        {
            if (
                !dataHolder.TryGetModRuleAtlas(
                    helper.GameContent,
                    out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
                )
            )
            {
                continue;
            }
            foreach (ItemSpriteRuleAtlas holder in modRuleAtlas.Values)
            {
                TryGetItemSpriteCompForQualifiedItemId(holder.QualifiedItemId, out _, makeNewIfNotFound: true);
            }
        }
    }

    private void RecheckAllDynamicSpriteIndex()
    {
        watchedItems.Clear();
        Utility.ForEachItem(item =>
        {
            if (TryGetItemSpriteCompForQualifiedItemId(item.QualifiedItemId, out ItemSpriteComp? itemSpriteComp))
            {
                ApplyDynamicSpriteIndex(item, itemSpriteComp, out _);
            }
            return true;
        });
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        needItemSpriteCompRecheck.Clear();
        watchedItems.Clear();
        qIdToComp.Clear();
    }

    internal bool ApplyDynamicSpriteIndex(
        Item item,
        ItemSpriteComp itemSpriteComp,
        [NotNullWhen(true)] out ItemSpriteIndexHolder? holder
    )
    {
        holder = null;
        if (itemSpriteComp.CanApplySpriteIndexFromRules)
        {
            holder = watchedItems.GetValue(item, ItemSpriteIndexHolder.Make);
            itemSpriteComp.DoApplySpriteIndexFromRules(item, holder);
            return true;
        }
        return false;
    }

    internal bool EnsureSpriteIndexForThisDraw(SObject obj, [NotNullWhen(true)] out ItemSpriteIndexHolder? holder)
    {
        if (watchedItems.TryGetValue(obj, out holder))
        {
            if (
                DynamicMethods.Item_get_contextTagsDirty(obj)
                && TryGetItemSpriteCompForQualifiedItemId(obj.QualifiedItemId, out ItemSpriteComp? itemSpriteComp)
            )
            {
                return ApplyDynamicSpriteIndex(obj, itemSpriteComp, out holder);
            }
            return true;
        }
        else if (
            TryGetItemSpriteCompForQualifiedItemId(obj.QualifiedItemId, out ItemSpriteComp? itemSpriteComp)
            && ApplyDynamicSpriteIndex(obj, itemSpriteComp, out holder)
        )
        {
            return true;
        }
        watchedItems.Remove(obj);
        return false;
    }
}
