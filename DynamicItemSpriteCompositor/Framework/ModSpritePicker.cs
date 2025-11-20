using System.Text;
using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;

namespace DynamicItemSpriteCompositor.Framework;

internal record DisplayInfo(Texture2D Icon, Rectangle IconSourceRect, string Label);

internal sealed record AtlasPickDisplayInfo(
    Texture2D Icon,
    Rectangle IconSourceRect,
    string Label,
    ItemSpriteRuleAtlas Atlas
) : DisplayInfo(Icon, IconSourceRect, Label);

internal class RelativeCC(Rectangle bounds, string name) : ClickableComponent(bounds, name)
{
    protected static readonly Rectangle MenuRectInset = new(0, 320, 60, 60);
    protected static readonly Rectangle MenuRectRaised = new(64, 320, 60, 60);

    internal int BaseX { get; set; } = 0;
    internal int BaseY { get; set; } = 0;

    public void Reposition(int x, int y)
    {
        bounds.X = BaseX + x;
        bounds.Y = BaseY + y;
    }

    public virtual void Draw(SpriteBatch b, DisplayInfo display, float alpha) { }
}

internal sealed class AtlasPickCC(Rectangle bounds, string name) : RelativeCC(bounds, name)
{
    public override void Draw(SpriteBatch b, DisplayInfo display, float alpha)
    {
        if (!visible)
        {
            return;
        }
        Color clr = Color.White * alpha;

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            MenuRectInset,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            clr,
            drawShadow: false
        );
        b.Draw(
            display.Icon,
            new(bounds.X + ModSpritePicker.PADDING, bounds.Y + ModSpritePicker.PADDING, 64, 64),
            display.IconSourceRect,
            clr,
            0,
            Vector2.Zero,
            SpriteEffects.None,
            1f
        );
        Utility.drawTextWithShadow(
            b,
            display.Label,
            Game1.smallFont,
            new(bounds.X + ModSpritePicker.PADDING * 2 + 64, bounds.Y + ModSpritePicker.PADDING + 12),
            Game1.textColor * alpha
        );
    }
}

internal sealed class TexturePicksCC(Rectangle bounds, string name) : RelativeCC(bounds, name)
{
    public override void Draw(SpriteBatch b, DisplayInfo display, float alpha)
    {
        if (!visible)
        {
            return;
        }
        Color clr = Color.White * alpha;

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            MenuRectRaised,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            clr,
            drawShadow: false
        );
        b.Draw(
            display.Icon,
            new(bounds.X + ModSpritePicker.PADDING / 2, bounds.Y + ModSpritePicker.PADDING / 2, 64, 64),
            display.IconSourceRect,
            clr,
            0,
            Vector2.Zero,
            SpriteEffects.None,
            1f
        );
    }
}

internal sealed class ModSpritePicker : IClickableMenu
{
    internal const int MARGIN = 16;
    internal const int PADDING = 8;

    internal const int ATLAS_ROW_CNT = 6;
    internal const int ATLAS_COL_CNT = 2;
    internal const int TEXTURE_ROW_CNT = 5;
    internal const int TEXTURE_COL_CNT = 10;

    private readonly Rectangle MenuRectBG = new(0, 256, 60, 60);

    private readonly StringBuilder sb = new();
    private readonly IModHelper helper;
    private readonly Dictionary<IAssetName, ModProidedDataHolder> modDataAssets;
    private readonly Action<string> updateCompTxForQId;

    private ModProidedDataHolder? currentMod = null;

    private readonly List<RelativeCC> AtlasPicks = [];
    private int AtlasPicksDisplayIdx = 0;
    private readonly List<DisplayInfo> AtlasPicksDisplay = [];

    private int AtlasCurrIdx = 0;

    private readonly List<RelativeCC> TexturePicks = [];
    private int TexturePicksDisplayIdx = 0;
    private readonly List<List<DisplayInfo>> TexturePicksDisplayAll = [];
    private List<DisplayInfo> TexturePicksDisplayCurr => TexturePicksDisplayAll[AtlasCurrIdx];

    internal ModSpritePicker(
        IModHelper helper,
        Dictionary<IAssetName, ModProidedDataHolder> modDataAssets,
        Action<string> updateCompTxForQId
    )
        : base(
            Game1.viewport.X + 96,
            Game1.viewport.Y + 96,
            (64 + PADDING * 2) * TEXTURE_COL_CNT + MARGIN * 2,
            (64 + PADDING * 2) * ATLAS_ROW_CNT + MARGIN * 2,
            showUpperRightCloseButton: false
        )
    {
        this.helper = helper;
        this.modDataAssets = modDataAssets;
        this.updateCompTxForQId = updateCompTxForQId;

        int cellWidth = width / 2 - MARGIN - PADDING / 4;
        int cellHeight = (height - MARGIN * 2) / 6;
        int myID = 0;
        int row = 0;
        int col = 0;

        for (int i = 0; i < (ATLAS_COL_CNT * ATLAS_ROW_CNT); i++)
        {
            row = i % ATLAS_COL_CNT;
            col = i / ATLAS_COL_CNT;
            myID = 100 + i;
            // string name, Rectangle bounds, string label, string hoverText, Texture2D texture, Rectangle sourceRect, float scale, bool drawShadow = false
            AtlasPicks.Add(
                new AtlasPickCC(new(0, 0, cellWidth, cellHeight), $"AtlasPicks_{i}")
                {
                    BaseX = MARGIN + (PADDING / 2 + cellWidth) * row,
                    BaseY = MARGIN + (PADDING + cellHeight) * col,
                    myID = myID,
                    upNeighborID = row > 0 ? myID - ATLAS_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    leftNeighborID = col > 0 ? myID - 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    rightNeighborID = col < ATLAS_COL_CNT - 1 ? myID + 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    downNeighborID =
                        row < ATLAS_ROW_CNT - 1 ? myID + ATLAS_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                }
            );
        }

        cellWidth = 64 + PADDING;
        cellHeight = 64 + PADDING;
        for (int i = 0; i < (TEXTURE_COL_CNT * TEXTURE_ROW_CNT); i++)
        {
            row = i % TEXTURE_COL_CNT;
            col = i / TEXTURE_COL_CNT;
            myID = 200 + i;
            // string name, Rectangle bounds, string label, string hoverText, Texture2D texture, Rectangle sourceRect, float scale, bool drawShadow = false
            TexturePicks.Add(
                new TexturePicksCC(new(0, 0, cellWidth, cellHeight), $"TexturePicks_{i}")
                {
                    BaseX = MARGIN + (PADDING + cellWidth) * row,
                    BaseY = MARGIN + (PADDING + cellHeight) * (col + 1),
                    myID = myID,
                    upNeighborID = row > 0 ? myID - TEXTURE_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    leftNeighborID = col > 0 ? myID - 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    rightNeighborID = col < TEXTURE_COL_CNT - 1 ? myID + 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    downNeighborID =
                        row < TEXTURE_ROW_CNT - 1 ? myID + TEXTURE_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                }
            );
        }

        // WIP menu
        helper.ConsoleCommands.Add(
            "disco-pick-cli",
            "Configure which sprite is being used for each content pack.",
            ConsoleDiscoPickCLI
        );
        helper.ConsoleCommands.Add(
            "disco-pick",
            "Configure which sprite is being used for each content pack.",
            ConsoleDiscoPickUI
        );
    }

    public static void DrawWithDisplayInfo(SpriteBatch b, List<RelativeCC> picks, List<DisplayInfo> displays, int start)
    {
        int length = Math.Min(picks.Count, displays.Count - start);
        for (int i = start; i < length; i++)
        {
            RelativeCC pick = picks[i];
            DisplayInfo disp = displays[i];
            pick.Draw(b, disp, 1f);
        }
    }

    public static bool TryCheckBounds(
        List<RelativeCC> picks,
        List<DisplayInfo> displays,
        int start,
        int x,
        int y,
        out int index
    )
    {
        index = -1;
        int length = Math.Min(picks.Count, displays.Count - start);
        for (int i = start; i < length; i++)
        {
            RelativeCC pick = picks[i];
            if (pick.bounds.Contains(x, y))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    public override void draw(SpriteBatch b)
    {
        if (currentMod == null)
        {
            return;
        }

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
        SpriteText.drawStringWithScrollCenteredAt(
            b,
            currentMod.Mod.Name,
            xPositionOnScreen + width / 2,
            yPositionOnScreen - 64
        );
        if (AtlasCurrIdx < 0)
        {
            DrawWithDisplayInfo(b, AtlasPicks, AtlasPicksDisplay, AtlasPicksDisplayIdx);
        }
        else
        {
            Utility.drawTextWithShadow(
                b,
                AtlasPicksDisplay[AtlasCurrIdx].Label,
                Game1.dialogueFont,
                new(xPositionOnScreen + MARGIN + PADDING, yPositionOnScreen + MARGIN + PADDING),
                Game1.textColor
            );
            DrawWithDisplayInfo(b, TexturePicks, TexturePicksDisplayCurr, TexturePicksDisplayIdx);
        }

        Game1.mouseCursorTransparency = 1f;
        if (!Game1.options.SnappyMenus)
        {
            drawMouse(b);
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (AtlasCurrIdx < 0)
        {
            if (TryCheckBounds(AtlasPicks, AtlasPicksDisplay, AtlasPicksDisplayIdx, x, y, out int index))
            {
                AtlasCurrIdx = index;
                TexturePicksDisplayIdx = 0;
                return;
            }
        }
        else
        {
            if (TryCheckBounds(TexturePicks, TexturePicksDisplayCurr, TexturePicksDisplayIdx, x, y, out int index))
            {
                ItemSpriteRuleAtlas ruleAtlas = (AtlasPicksDisplay[AtlasCurrIdx] as AtlasPickDisplayInfo)!.Atlas;
                ruleAtlas.ChosenIdx = index;
                updateCompTxForQId(ruleAtlas.QualifiedItemId);
            }
            AtlasCurrIdx = -1;
            return;
        }
        base.receiveLeftClick(x, y, playSound);
    }

    private void RecenterMenu()
    {
        Vector2 position = Utility.getTopLeftPositionForCenteringOnScreen(width, height, 0, 0);
        xPositionOnScreen = (int)position.X;
        yPositionOnScreen = (int)position.Y;

        foreach (RelativeCC ctc in AtlasPicks)
        {
            ctc.Reposition(xPositionOnScreen, yPositionOnScreen);
        }
        foreach (RelativeCC ctc in TexturePicks)
        {
            ctc.Reposition(xPositionOnScreen, yPositionOnScreen);
        }
    }

    private bool PopulateDisplayData()
    {
        AtlasCurrIdx = -1;
        AtlasPicksDisplayIdx = 0;
        TexturePicksDisplayIdx = 0;
        AtlasPicksDisplay.Clear();
        TexturePicksDisplayAll.Clear();
        if (
            !(
                currentMod?.TryGetModRuleAtlas(
                    helper.GameContent,
                    out Dictionary<string, ItemSpriteRuleAtlas>? modRuleAtlas
                ) ?? false
            )
        )
        {
            return false;
        }
        foreach ((string key, ItemSpriteRuleAtlas ruleAtlas) in modRuleAtlas)
        {
            if (
                ItemSpriteManager.SafeResolveMetadata(ruleAtlas.QualifiedItemId)?.GetParsedData()
                is not ParsedItemData parsedItemData
            )
            {
                continue;
            }
            if (ruleAtlas.SourceTextureList.Count == 1)
                continue;
            AtlasPicksDisplay.Add(
                new AtlasPickDisplayInfo(
                    parsedItemData.GetTexture(),
                    parsedItemData.GetSourceRect(),
                    $"{parsedItemData.DisplayName}: {TokenParser.ParseText(ruleAtlas.ConfigName) ?? key}",
                    ruleAtlas
                )
            );
            int displayIdx = ruleAtlas.Rules.SelectMany(rule => rule.SpriteIndexList).Min();
            List<DisplayInfo> textureDisplayList = [];
            foreach (SourceTextureOption option in ruleAtlas.SourceTextureList)
            {
                textureDisplayList.Add(
                    new(
                        helper.GameContent.Load<Texture2D>(option.SourceTextureAsset!),
                        parsedItemData.GetSourceRect(spriteIndex: displayIdx),
                        TokenParser.ParseText(option.ConfigName) ?? option.Texture
                    )
                );
            }
            TexturePicksDisplayAll.Add(textureDisplayList);
        }
        return TexturePicksDisplayAll.Any();
    }

    #region DELEGATES
    private void ConsoleDiscoPickUI(string arg1, string[] arg2)
    {
        if (currentMod == null)
        {
            foreach (ModProidedDataHolder dataHolder in modDataAssets.Values)
            {
                if (dataHolder.TryGetModRuleAtlas(helper.GameContent, out _))
                {
                    currentMod = dataHolder;
                    continue;
                }
            }
        }

        if (PopulateDisplayData())
        {
            RecenterMenu();
            populateClickableComponentList();
            Game1.activeClickableMenu = this;
        }
    }

    private void ConsoleDiscoPickCLI(string command, string[] args)
    {
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
                    sb.Append(TokenParser.ParseText(option.ConfigName) ?? option.Texture);
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
    #endregion
}
