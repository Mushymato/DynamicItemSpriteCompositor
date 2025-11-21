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

    private readonly Dictionary<IAssetName, ModProidedDataHolder> modDataAssets = [];
    internal readonly Dictionary<string, ItemSpriteComp> qIdToComp = [];

    private readonly HashSet<string> needItemSpriteCompRecheck = [];
    private HashSet<Item> needApplyDynamicSpriteIndex = [];
    private readonly ConditionalWeakTable<Item, ItemSpriteIndexHolder> watchedItems = [];

    private readonly ModSpritePicker spritePicker;

    internal void AddToNeedApplyDynamicSpriteIndex(Item item)
    {
        if (ApplyDynamicSpriteIndex(item, resetSpriteIndex: true))
        {
            needApplyDynamicSpriteIndex.Add(item);
        }
    }

    internal void AddToNeedApplyDynamicSpriteIndexIfWatched(Item item)
    {
        if (watchedItems.TryGetValue(item, out _))
            needApplyDynamicSpriteIndex.Add(item);
    }

    internal ItemSpriteManager(IModHelper helper)
    {
        this.helper = helper;
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
            ModEntry.Log(
                $"No content packs detected, mod is disabled. All sprite indexes will be reset once a save is loaded."
            );
            this.helper.Events.GameLoop.SaveLoaded += OnSaveLoaded_ModDisabled;
            spritePicker = null!;
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

        helper.ConsoleCommands.Add(
            "disco-recheck",
            "Recheck dynamic sprites on every item in the world.",
            ConsoleRecheck
        );

        Patches.Register();

        spritePicker = new(helper, modDataAssets, UpdateCompTxForQId);
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
                        ApplyDynamicSpriteIndex(item, itemSpriteComp: itemSpriteComp);
                    }
                }
            }
        }
    }

    internal static ItemMetadata? SafeResolveMetadata(string qualifiedItemId)
    {
        Patches.ItemMetadata_SetTypeDefinition_Postfix_Enabled = false;
        ItemMetadata metadata = ItemRegistry.ResolveMetadata(qualifiedItemId);
        Patches.ItemMetadata_SetTypeDefinition_Postfix_Enabled = true;
        return metadata;
    }

    private bool TryGetItemSpriteCompForQualifiedItemId(
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
            ModEntry.LogDebug($"Load '{e.Name}' for content pack");
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
                if (TryGetItemSpriteCompForQualifiedItemId(key, out ItemSpriteComp? itemSpriteComp))
                {
                    foreach ((Item item, _) in watchedItems)
                    {
                        if (item.QualifiedItemId == key)
                        {
                            ApplyDynamicSpriteIndex(item, itemSpriteComp: itemSpriteComp);
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
            HashSet<Item> currentToApply = needApplyDynamicSpriteIndex;
            needApplyDynamicSpriteIndex = [];
            foreach (Item item in currentToApply)
            {
                ApplyDynamicSpriteIndex(item);
            }
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => RecheckAllDynamicSpriteIndex(true);

    private void RecheckAllDynamicSpriteIndex(bool isSaveLoaded = false)
    {
        Utility.ForEachItem(item =>
        {
            ApplyDynamicSpriteIndex(item, isSaveLoaded: isSaveLoaded);
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

    internal bool ApplyDynamicSpriteIndex(
        Item item,
        bool resetSpriteIndex = false,
        bool isSaveLoaded = false,
        ItemSpriteComp? itemSpriteComp = null
    )
    {
        if (itemSpriteComp != null || TryGetItemSpriteCompForQualifiedItemId(item.QualifiedItemId, out itemSpriteComp))
        {
            if (itemSpriteComp.TryApplySpriteIndexFromRules(item, out int? spriteIndex))
            {
                watchedItems
                    .GetValue(item, ItemSpriteIndexHolder.Make)
                    .Apply(item, spriteIndex.Value, itemSpriteComp.baseSpriteIndex, resetSpriteIndex, isSaveLoaded);
                return true;
            }
        }
        watchedItems.Remove(item);
        return false;
    }

    internal bool SetSpriteIndex(Item item, int newSpriteIndex)
    {
        if (watchedItems.TryGetValue(item, out ItemSpriteIndexHolder? holder))
        {
            holder.Change(item, newSpriteIndex);
            return false;
        }
        return true;
    }

    internal int GetSpriteIndex(Item item, int currentSpriteIndex)
    {
        if (watchedItems.TryGetValue(item, out ItemSpriteIndexHolder? holder))
        {
            return holder.SpriteIndex;
        }
        return currentSpriteIndex;
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
