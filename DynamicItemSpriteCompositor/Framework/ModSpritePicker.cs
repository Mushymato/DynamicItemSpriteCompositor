using System.Text;
using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace DynamicItemSpriteCompositor.Framework;

internal sealed class ModSpritePicker : IClickableMenu
{
    private readonly Rectangle MenuRectBG = new(0, 256, 60, 60);
    private readonly Rectangle MenuRectInset = new(0, 320, 60, 60);

    private readonly StringBuilder sb = new();
    private readonly IModHelper helper;
    private readonly Dictionary<IAssetName, ModProidedDataHolder> modDataAssets;
    private readonly Action<string> updateCompTxForQId;

    private ModProidedDataHolder? currentMod = null;
    private OptionsDropDown? dropDown = null;

    internal ModSpritePicker(
        IModHelper helper,
        Dictionary<IAssetName, ModProidedDataHolder> modDataAssets,
        Action<string> updateCompTxForQId
    )
        : base(
            Game1.viewport.X + 96,
            Game1.viewport.Y + 96,
            256,
            Game1.viewport.Height - 96,
            showUpperRightCloseButton: false
        )
    {
        this.helper = helper;
        this.modDataAssets = modDataAssets;
        this.updateCompTxForQId = updateCompTxForQId;

        foreach ((IAssetName assetName, ModProidedDataHolder? dataHolder) in modDataAssets)
        {
            if (
                dataHolder.TryGetModRuleAtlas(
                    helper.GameContent,
                    assetName,
                    out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
                )
            )
            {
                currentMod ??= dataHolder;
            }
        }

        // WIP menu
        helper.ConsoleCommands.Add(
            "disco-pick",
            "Configure which sprite is being used for each content pack.",
            ConsoleDiscoPick
        );
        helper.ConsoleCommands.Add(
            "disco-pick-ui",
            "Configure which sprite is being used for each content pack.",
            ConsoleDiscoPickUI
        );
    }

    public override void draw(SpriteBatch b)
    {
        drawTextureBox(
            b,
            Game1.menuTexture,
            MenuRectBG,
            xPositionOnScreen,
            yPositionOnScreen,
            width,
            height,
            Color.White
        );
        base.draw(b);
        const int MARGIN = 16;
        // const int PADDING = 8;
        int offsetY = yPositionOnScreen + MARGIN;
        if (dropDown != null)
        {
            dropDown.draw(b, xPositionOnScreen + MARGIN, offsetY);
            // foreach ((string key, ItemSpriteRuleAtlas ruleAtlas) in currentMod.Data)
            // {
            //     string option = ruleAtlas.ConfigName ?? key;
            //     Point stringSize = Game1.smallFont.MeasureString(option).ToPoint();
            //     drawTextureBox(
            //         b,
            //         Game1.menuTexture,
            //         MenuRectInset,
            //         xPositionOnScreen + MARGIN,
            //         offsetY,
            //         width - MARGIN * 2,
            //         stringSize.Y + PADDING * 2,
            //         Color.White
            //     );
            //     Utility.drawTextWithShadow(
            //         b,
            //         option,
            //         Game1.smallFont,
            //         new Vector2(xPositionOnScreen + MARGIN + PADDING, offsetY + PADDING),
            //         Game1.textColor
            //     );
            //     offsetY += PADDING + stringSize.Y + PADDING * 2 + MARGIN;
            // }
        }
        Game1.mouseCursorTransparency = 1f;
        if (!Game1.options.SnappyMenus)
        {
            drawMouse(b);
        }
    }

    private void ConsoleDiscoPickUI(string arg1, string[] arg2)
    {
        if (currentMod == null)
            return;
        dropDown ??= new("", 0);
        dropDown.dropDownOptions.Clear();
        foreach ((string key, ItemSpriteRuleAtlas ruleAtlas) in currentMod.Data)
        {
            dropDown.dropDownOptions.Add(key);
            dropDown.dropDownDisplayOptions.Add(ruleAtlas.ConfigName ?? key);
        }
        dropDown.RecalculateBounds();

        xPositionOnScreen = 96;
        yPositionOnScreen = 96;
        width = 256;
        height = Game1.uiViewport.Height - 96 * 2;
        Game1.activeClickableMenu = this;
    }

    private void ConsoleDiscoPick(string command, string[] args)
    {
        foreach ((IAssetName assetName, ModProidedDataHolder? dataHolder) in modDataAssets)
        {
            if (
                !dataHolder.TryGetModRuleAtlas(
                    helper.GameContent,
                    assetName,
                    out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
                )
            )
            {
                continue;
            }
            if (modRuleAtlas.Count == 0)
                continue;

            sb.Append("\n= [");
            sb.Append(dataHolder.Mod.UniqueID);
            sb.Append("] ");
            sb.Append(dataHolder.Mod.Name);
            sb.Append(" =");
            foreach ((string key, ItemSpriteRuleAtlas ruleAtlas) in modRuleAtlas)
            {
                if (
                    ItemSpriteManager.SafeResolveMetadata(ruleAtlas.QualifiedItemId)?.GetParsedData()
                    is not ParsedItemData parsedItemData
                )
                {
                    continue;
                }
                sb.Append("\n- [");
                sb.Append(key);
                sb.Append("] ");
                sb.Append(parsedItemData.DisplayName);
                sb.Append(" - ");
                sb.Append(parsedItemData.QualifiedItemId);
                sb.Append(" -");
                int idx = 0;
                foreach (SourceTextureOption option in ruleAtlas.SourceTextureList)
                {
                    sb.Append("\n  ");
                    sb.Append(idx == ruleAtlas.ChosenIdx ? '*' : '.');
                    sb.Append(" [");
                    sb.Append(idx.ToString("D2"));
                    sb.Append("] ");
                    sb.Append(option.ConfigName ?? option.Texture);
                    idx++;
                }
                sb.Append(' ');
            }
        }

        sb.Append('\n');
        ModEntry.Log(sb.ToString(), LogLevel.Info);
        sb.Clear();

        if (args.Length >= 3)
        {
            foreach (ModProidedDataHolder? dataHolder in modDataAssets.Values)
            {
                if (
                    dataHolder.IsValid
                    && dataHolder.Mod.UniqueID == args[0]
                    && dataHolder.Data.TryGetValue(args[1], out ItemSpriteRuleAtlas? ruleAtlas)
                    && int.TryParse(args[2], out int idx)
                    && ruleAtlas.ChosenIdx != idx
                )
                {
                    ruleAtlas.ChosenIdx = idx;
                    ModEntry.Log($"Set {args[0]} {args[1]} -> {idx}", LogLevel.Info);
                    updateCompTxForQId(ruleAtlas.QualifiedItemId);
                }
            }
        }
    }
}
