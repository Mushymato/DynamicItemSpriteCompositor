using DynamicItemSpriteCompositor.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;

namespace DynamicItemSpriteCompositor.Framework;

internal record DisplayInfo(Texture2D Icon, Rectangle IconSourceRect, string Label)
{
    internal float Scale = 64f / IconSourceRect.Height;
    internal int XOffset = (int)((64 - IconSourceRect.Width * (64f / IconSourceRect.Height)) / 2);
    internal bool Chosen { get; set; } = false;

    internal void DrawIcon(SpriteBatch b, Rectangle bounds, int PaddingX, int PaddingY)
    {
        b.Draw(
            Icon,
            new(bounds.X + PaddingX + ModSpritePicker.PADDING + XOffset, bounds.Y + PaddingY + ModSpritePicker.PADDING),
            IconSourceRect,
            Color.White,
            0,
            Vector2.Zero,
            Scale,
            SpriteEffects.None,
            1f
        );
    }
}

internal sealed record AtlasPickDisplayInfo(
    Texture2D Icon,
    Rectangle IconSourceRect,
    string Label,
    int DisplayIdx,
    ItemSpriteRuleAtlas Atlas,
    IGameContentHelper Content,
    Texture2D IconError,
    Rectangle IconErrorSourceRect
) : DisplayInfo(Icon, IconSourceRect, Label)
{
    internal Texture2D? IconCurrent { get; private set; } = null!;
    internal Rectangle IconCurrentSourceRect { get; private set; } = Rectangle.Empty;

    internal void UpdateIconCurrent()
    {
        if (Atlas.Enabled)
        {
            IconCurrent = Content.Load<Texture2D>(Atlas.ChosenSourceTexture.SourceTextureAsset);
            IconCurrentSourceRect = ItemSpriteComp.GetSourceRectForIndex(
                IconCurrent.ActualWidth,
                DisplayIdx,
                new(IconSourceRect.Width, IconSourceRect.Height)
            );
        }
        else
        {
            IconCurrent = IconError;
            IconCurrentSourceRect = IconErrorSourceRect;
        }
    }
}

internal class RelativeCC(Rectangle bounds, string name) : ClickableComponent(bounds, name)
{
    protected static readonly Rectangle MenuRectInset = new(0, 320, 60, 60);
    protected static readonly Rectangle MenuRectRaised = new(64, 320, 60, 60);

    internal int BaseX { get; set; } = 0;
    internal int BaseY { get; set; } = 0;

    internal int PaddingX { get; set; } = 0;
    internal int PaddingY { get; set; } = 0;

    public void Reposition(int x, int y)
    {
        bounds.X = BaseX + x;
        bounds.Y = BaseY + y;
    }

    public virtual void Draw(SpriteBatch b) { }

    public virtual void Draw(SpriteBatch b, DisplayInfo display) { }
}

internal sealed class AtlasPickCC(Rectangle bounds, string name) : RelativeCC(bounds, name)
{
    public override void Draw(SpriteBatch b, DisplayInfo display)
    {
        if (!visible)
        {
            return;
        }

        AtlasPickDisplayInfo aDisplay = (AtlasPickDisplayInfo)display;

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            MenuRectInset,
            bounds.X + PaddingX,
            bounds.Y + PaddingY,
            bounds.Width - PaddingX,
            bounds.Height - PaddingY,
            Color.White,
            drawShadow: false
        );
        aDisplay.DrawIcon(b, bounds, PaddingX, PaddingY);
        if (aDisplay.IconCurrent != null)
        {
            b.Draw(
                aDisplay.IconCurrent,
                new(
                    bounds.X + PaddingX + ModSpritePicker.PADDING * 2 + 64 + aDisplay.XOffset,
                    bounds.Y + PaddingY + ModSpritePicker.PADDING
                ),
                aDisplay.IconCurrentSourceRect,
                Color.White,
                0,
                Vector2.Zero,
                aDisplay.Scale,
                SpriteEffects.None,
                1f
            );
        }
        Utility.drawTextWithShadow(
            b,
            aDisplay.Label,
            Game1.smallFont,
            new(
                bounds.X + PaddingX + (ModSpritePicker.PADDING * 2 + 64) * 2,
                bounds.Y + PaddingY + ModSpritePicker.PADDING + 18
            ),
            Game1.textColor
        );
    }
}

internal sealed class TexturePicksCC(Rectangle bounds, string name) : RelativeCC(bounds, name)
{
    public override void Draw(SpriteBatch b, DisplayInfo display)
    {
        if (!visible)
        {
            return;
        }

        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            display.Chosen ? MenuRectInset : MenuRectRaised,
            bounds.X + PaddingX,
            bounds.Y + PaddingY,
            bounds.Width - PaddingX,
            bounds.Height - PaddingY,
            Color.White,
            drawShadow: false
        );
        display.DrawIcon(b, bounds, PaddingX, PaddingY);
    }
}

internal sealed class ArrowCC(Rectangle bounds, string name) : RelativeCC(bounds, name)
{
    internal Rectangle CursorsSourceRect { get; set; }

    public override void Draw(SpriteBatch b)
    {
        if (!visible)
        {
            return;
        }
        Color clr = Color.White;

        b.Draw(
            Game1.mouseCursors,
            new(bounds.X, bounds.Y, 64, 64),
            CursorsSourceRect,
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
    internal const int TEXTURE_ROW_CNT = 6;
    internal const int TEXTURE_COL_CNT = 9;

    private readonly Rectangle MenuRectBG = new(0, 256, 60, 60);

    private readonly IModHelper helper;
    private readonly ModConfigHelper config;
    internal readonly List<ModProidedDataHolder> modDataHolders;
    private readonly Action<string, bool> updateForQId;

    private int CurrentModIdx
    {
        get => field;
        set
        {
            field = value;
            PopulateDisplayData();
        }
    } = -1;
    private ModProidedDataHolder? CurrentMod =>
        (CurrentModIdx < 0 || CurrentModIdx >= modDataHolders.Count) ? null : modDataHolders[CurrentModIdx];

    private readonly List<RelativeCC> AtlasPicks = [];
    private int AtlasPicksDisplayIdx = 0;
    private readonly List<DisplayInfo> AtlasPicksDisplay = [];

    private int AtlasCurrIdx = 0;

    private readonly List<RelativeCC> TexturePicks = [];
    private int TexturePicksDisplayIdx = 0;
    private readonly List<List<DisplayInfo>> TexturePicksDisplayAll = [];
    private List<DisplayInfo> TexturePicksDisplayCurr => TexturePicksDisplayAll[AtlasCurrIdx];

    private readonly ArrowCC Mod_L = new(new(0, 0, 64, 64), "Mod_L") { CursorsSourceRect = new(0, 256, 64, 64) };
    private readonly ArrowCC Mod_R = new(new(0, 0, 64, 64), "Mod_R") { CursorsSourceRect = new(0, 192, 64, 64) };

    internal ModSpritePicker(
        IModHelper helper,
        ModConfigHelper config,
        Dictionary<IAssetName, ModProidedDataHolder> modDataAssets,
        Action<string, bool> updateForQId
    )
        : base(
            Game1.viewport.X + 96,
            Game1.viewport.Y + 96,
            (64 + PADDING * 3) * TEXTURE_COL_CNT + MARGIN * 2 + PADDING,
            (64 + PADDING * 3) * TEXTURE_ROW_CNT + MARGIN * 2,
            showUpperRightCloseButton: false
        )
    {
        this.helper = helper;
        this.config = config;
        this.modDataHolders = modDataAssets.Values.ToList();
        this.updateForQId = updateForQId;

        Mod_L.BaseX = 0;
        Mod_L.BaseY = -64 - PADDING;
        Mod_R.BaseX = width - 64;
        Mod_R.BaseY = -64 - PADDING;

        int cellWidth = (64 + PADDING * 3) * TEXTURE_COL_CNT / 2;
        int cellHeight = 64 + PADDING * 3;
        int myID = 0;
        int row = 0;
        int col = 0;

        for (int i = 0; i < (ATLAS_COL_CNT * ATLAS_ROW_CNT); i++)
        {
            row = i / ATLAS_COL_CNT;
            col = i % ATLAS_COL_CNT;
            myID = 100 + i;
            AtlasPicks.Add(
                new AtlasPickCC(new(0, 0, cellWidth, cellHeight), $"AtlasPicks_{i}")
                {
                    BaseX = MARGIN + cellWidth * col,
                    BaseY = MARGIN + cellHeight * row,
                    PaddingX = PADDING / 2,
                    PaddingY = PADDING,
                    myID = myID,
                    upNeighborID = row > 0 ? myID - ATLAS_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    leftNeighborID = col > 0 ? myID - 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    rightNeighborID = col < ATLAS_COL_CNT - 1 ? myID + 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    downNeighborID =
                        row < ATLAS_ROW_CNT - 1 ? myID + ATLAS_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                }
            );
        }

        cellWidth = 64 + PADDING * 2;
        cellHeight = 64 + PADDING * 2;
        for (int i = 0; i < (TEXTURE_COL_CNT * TEXTURE_ROW_CNT); i++)
        {
            row = i / TEXTURE_COL_CNT;
            col = i % TEXTURE_COL_CNT;
            myID = 200 + i;
            TexturePicks.Add(
                new TexturePicksCC(new(0, 0, PADDING + cellWidth, PADDING + cellHeight), $"TexturePicks_{i}")
                {
                    BaseX = MARGIN + (PADDING + cellWidth) * col,
                    BaseY = MARGIN + (PADDING + cellHeight) * row,
                    PaddingX = PADDING / 2,
                    PaddingY = PADDING / 2,
                    myID = myID,
                    upNeighborID = row > 0 ? myID - TEXTURE_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    leftNeighborID = col > 0 ? myID - 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    rightNeighborID = col < TEXTURE_COL_CNT - 1 ? myID + 1 : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                    downNeighborID =
                        row < TEXTURE_ROW_CNT ? myID + TEXTURE_COL_CNT : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                }
            );
        }

        helper.ConsoleCommands.Add(
            "disco-pick",
            "Configure which sprite is being used for each content pack.",
            ConsolePickUI
        );
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        config.SetupGMCM(this);
    }

    public static void DrawWithDisplayInfo(
        SpriteBatch b,
        List<RelativeCC> picks,
        List<DisplayInfo> displays,
        int start,
        int chosen = -1
    )
    {
        int length = Math.Min(picks.Count, displays.Count - start);
        for (int i = 0; i < length; i++)
        {
            RelativeCC pick = picks[i];
            DisplayInfo disp = displays[start + i];
            disp.Chosen = chosen == start + i;
            pick.Draw(b, disp);
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
        if (CurrentMod == null)
        {
            return;
        }
        if (GetParentMenu() is TitleMenu titleMenu)
        {
            b.Draw(
                Game1.mouseCursors,
                new Rectangle(
                    0,
                    (int)(-300 * TitleMenu.pixelZoom - titleMenu.viewportY * 0.66f),
                    titleMenu.width,
                    300 * TitleMenu.pixelZoom + titleMenu.height - 120 * TitleMenu.pixelZoom
                ),
                new Rectangle(703, 1912, 1, 264),
                Color.White
            );
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
        if (AtlasCurrIdx < 0)
        {
            SpriteText.drawStringWithScrollCenteredAt(
                b,
                CurrentMod.Mod.Name,
                xPositionOnScreen + width / 2,
                yPositionOnScreen - 64
            );
            Mod_L.Draw(b);
            Mod_R.Draw(b);
            DrawWithDisplayInfo(b, AtlasPicks, AtlasPicksDisplay, AtlasPicksDisplayIdx);
        }
        else
        {
            SpriteText.drawStringWithScrollCenteredAt(
                b,
                $"{CurrentMod.Mod.Name} - {AtlasPicksDisplay[AtlasCurrIdx].Label}",
                xPositionOnScreen + width / 2,
                yPositionOnScreen - 64
            );

            ItemSpriteRuleAtlas atlas = ((AtlasPickDisplayInfo)AtlasPicksDisplay[AtlasCurrIdx]).Atlas;
            DrawWithDisplayInfo(
                b,
                TexturePicks,
                TexturePicksDisplayCurr,
                TexturePicksDisplayIdx,
                atlas.Enabled ? atlas.ChosenIdx + 1 : 0
            );
        }

        Game1.mouseCursorTransparency = 1f;
        drawMouse(b);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (AtlasCurrIdx < 0)
        {
            if (Mod_L.bounds.Contains(x, y))
            {
                PrevMod();
            }
            else if (Mod_R.bounds.Contains(x, y))
            {
                NextMod();
            }
            if (TryCheckBounds(AtlasPicks, AtlasPicksDisplay, AtlasPicksDisplayIdx, x, y, out int index))
            {
                AtlasCurrIdx = index;
                TexturePicksDisplayIdx = 0;
                snapToDefaultClickableComponent();
                Game1.playSound("bigSelect");
                return;
            }
        }
        else
        {
            if (TryCheckBounds(TexturePicks, TexturePicksDisplayCurr, TexturePicksDisplayIdx, x, y, out int index))
            {
                AtlasPickDisplayInfo atlasPickDisplay = (AtlasPickDisplayInfo)AtlasPicksDisplay[AtlasCurrIdx];
                ItemSpriteRuleAtlas ruleAtlas = atlasPickDisplay.Atlas;
                index -= 1;
                if (index < 0)
                {
                    ruleAtlas.Enabled = false;
                    updateForQId(ruleAtlas.QualifiedItemId, true);
                }
                else if (index != ruleAtlas.ChosenIdx || !ruleAtlas.Enabled)
                {
                    bool enabledStatusChanged = !ruleAtlas.Enabled;
                    ruleAtlas.Enabled = true;
                    ruleAtlas.ChosenIdx = index;
                    updateForQId(ruleAtlas.QualifiedItemId, enabledStatusChanged);
                }
                atlasPickDisplay.UpdateIconCurrent();
                Game1.playSound("smallSelect");
            }
            else
            {
                AtlasCurrIdx = -1;
                snapToDefaultClickableComponent();
                Game1.playSound("bigDeSelect");
            }
            return;
        }
        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (AtlasCurrIdx >= 0 && key != Keys.None && Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            AtlasCurrIdx = -1;
            snapToDefaultClickableComponent();
            Game1.playSound("bigDeSelect");
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (AtlasCurrIdx == -1)
        {
            switch (button)
            {
                case Buttons.LeftShoulder:
                    PrevMod();
                    snapToDefaultClickableComponent();
                    return;
                case Buttons.RightShoulder:
                    NextMod();
                    snapToDefaultClickableComponent();
                    return;
            }
        }
        base.receiveGamePadButton(button);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        bool scrolled = false;
        if (AtlasCurrIdx == -1)
        {
            if (direction > 0 && AtlasPicksDisplayIdx > 0)
            {
                AtlasPicksDisplayIdx -= ATLAS_COL_CNT;
                scrolled = true;
            }
            else if (direction < 0 && AtlasPicksDisplayIdx < Math.Max(0, AtlasPicksDisplay.Count - AtlasPicks.Count))
            {
                AtlasPicksDisplayIdx += ATLAS_COL_CNT;
                scrolled = true;
            }
        }
        else
        {
            if (direction > 0 && TexturePicksDisplayIdx > 0)
            {
                TexturePicksDisplayIdx -= TEXTURE_COL_CNT;
                scrolled = true;
            }
            else if (
                direction < 0
                && TexturePicksDisplayIdx < Math.Max(0, TexturePicksDisplayCurr.Count - TexturePicks.Count)
            )
            {
                TexturePicksDisplayIdx += TEXTURE_COL_CNT;
                scrolled = true;
            }
        }
        if (scrolled)
        {
            Game1.playSound("shiny4");
        }
    }

    public override bool readyToClose()
    {
        return base.readyToClose() && AtlasCurrIdx < 0;
    }

    public override void snapToDefaultClickableComponent()
    {
        if (AtlasCurrIdx < 0)
        {
            currentlySnappedComponent = getComponentWithID(100);
        }
        else
        {
            currentlySnappedComponent = getComponentWithID(200);
        }
        snapCursorToCurrentSnappedComponent();
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents ??= [];
        allClickableComponents.Clear();
        allClickableComponents.AddRange(AtlasPicks);
        allClickableComponents.AddRange(TexturePicks);
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

        Mod_L.Reposition(xPositionOnScreen, yPositionOnScreen);
        Mod_R.Reposition(xPositionOnScreen, yPositionOnScreen);
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        RecenterMenu();
    }

    private bool PopulateDisplayData()
    {
        ResetDisplayData();

        if (
            !(
                CurrentMod?.TryGetModRuleAtlas(
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
                ItemRegistry.GetMetadata(ruleAtlas.QualifiedItemId)?.GetParsedData()
                is not ParsedItemData parsedItemData
            )
            {
                continue;
            }
            Rectangle baseSourceRect = parsedItemData.GetSourceRect();
            int displayIdx =
                ruleAtlas.ConfigIconSpriteIndex ?? ruleAtlas.Rules.SelectMany(rule => rule.SpriteIndexList).Min();
            List<DisplayInfo> textureDisplayList =
            [
                new(
                    parsedItemData.ItemType.GetErrorTexture(),
                    parsedItemData.ItemType.GetErrorSourceRect(),
                    $"{ModEntry.ModId}_DISABLE"
                ),
            ];
            foreach (SourceTextureOption option in ruleAtlas.SourceTextureOptions)
            {
                Texture2D sourceTx = helper.GameContent.Load<Texture2D>(option.SourceTextureAsset);
                textureDisplayList.Add(
                    new(
                        sourceTx,
                        ItemSpriteComp.GetSourceRectForIndex(
                            sourceTx.ActualWidth,
                            displayIdx,
                            new(baseSourceRect.Width, baseSourceRect.Height)
                        ),
                        option.Texture
                    )
                );
            }
            Texture2D currTx = helper.GameContent.Load<Texture2D>(ruleAtlas.ChosenSourceTexture.SourceTextureAsset);
            AtlasPickDisplayInfo atlasPickDisplay = new(
                parsedItemData.GetTexture(),
                parsedItemData.GetSourceRect(),
                TokenParser.ParseText(ruleAtlas.ConfigName) ?? parsedItemData.DisplayName,
                displayIdx,
                ruleAtlas,
                helper.GameContent,
                parsedItemData.ItemType.GetErrorTexture(),
                parsedItemData.ItemType.GetErrorSourceRect()
            );
            atlasPickDisplay.UpdateIconCurrent();
            AtlasPicksDisplay.Add(atlasPickDisplay);

            TexturePicksDisplayAll.Add(textureDisplayList);
        }
        return TexturePicksDisplayAll.Any();
    }

    private void ResetDisplayData()
    {
        AtlasCurrIdx = -1;
        AtlasPicksDisplayIdx = 0;
        TexturePicksDisplayIdx = 0;
        AtlasPicksDisplay.Clear();
        TexturePicksDisplayAll.Clear();
    }

    private void NextMod()
    {
        int i = CurrentModIdx + 1;
        if (i < 0 || i >= modDataHolders.Count)
            i = 0;
        while (i < modDataHolders.Count)
        {
            if (modDataHolders[i].HasDisplayData(helper.GameContent))
            {
                CurrentModIdx = i;
                Game1.playSound("shwip");
                return;
            }
            i++;
        }
        if (CurrentModIdx <= 0 || CurrentModIdx >= modDataHolders.Count - 1)
            return;
        i = 0;
        int prev = CurrentModIdx;
        while (i < prev)
        {
            if (modDataHolders[i].HasDisplayData(helper.GameContent))
            {
                CurrentModIdx = i;
                Game1.playSound("shwip");
                return;
            }
            i++;
        }
    }

    private void PrevMod()
    {
        int i = CurrentModIdx - 1;
        if (i < 0 || i >= modDataHolders.Count)
            i = modDataHolders.Count - 1;
        while (i >= 0)
        {
            if (modDataHolders[i].HasDisplayData(helper.GameContent))
            {
                CurrentModIdx = i;
                Game1.playSound("shwip");
                return;
            }
            i--;
        }
        if (CurrentModIdx <= 0 || CurrentModIdx >= modDataHolders.Count - 1)
            return;
        i = modDataHolders.Count - 1;
        int prev = CurrentModIdx;
        while (i >= prev)
        {
            if (modDataHolders[i].HasDisplayData(helper.GameContent))
            {
                CurrentModIdx = i;
                Game1.playSound("shwip");
                return;
            }
            i--;
        }
    }

    protected override void cleanupBeforeExit()
    {
        ResetDisplayData();
        config.SaveContentPackTextureOptions(modDataHolders);
        base.cleanupBeforeExit();
    }

    internal void Cleanup() => cleanupBeforeExit();

    private bool PreparePickUI()
    {
        if (CurrentMod == null)
        {
            config.LoadContentPackTextureOptions(this.modDataHolders);
            NextMod();
        }

        if (PopulateDisplayData())
        {
            RecenterMenu();
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            {
                if (allClickableComponents == null)
                    populateClickableComponentList();
                snapToDefaultClickableComponent();
            }
            return true;
        }
        return false;
    }

    private void ConsolePickUI(string arg1, string[] arg2)
    {
        if (PreparePickUI())
        {
            Game1.activeClickableMenu = this;
        }
    }

    internal void GMCMPickUI()
    {
        if (PreparePickUI())
        {
            if (Game1.activeClickableMenu is TitleMenu titleMenu)
            {
                titleMenu.SetChildMenu(this);
            }
            else
            {
                DelayedAction.functionAfterDelay(
                    () =>
                    {
                        if (Game1.activeClickableMenu == null)
                        {
                            Game1.activeClickableMenu = this;
                            return;
                        }
                        IClickableMenu menu = Game1.activeClickableMenu;
                        while (menu.GetChildMenu() != null)
                            menu = menu.GetChildMenu();
                        menu.GetParentMenu().SetChildMenu(null);
                        Game1.nextClickableMenu.Add(Game1.activeClickableMenu);
                        Game1.activeClickableMenu = this;
                    },
                    0
                );
            }
        }
    }
}
