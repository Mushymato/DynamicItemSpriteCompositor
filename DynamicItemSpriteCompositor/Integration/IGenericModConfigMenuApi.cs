using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace DynamicItemSpriteCompositor.Integration;

/// <summary>The API which lets other mods add a config UI through Generic Mod Config Menu.</summary>
public interface IGenericModConfigMenuApi
{
    /*********
    ** Methods
    *********/
    /****
    ** Must be called first
    ****/
    /// <summary>Register a mod whose config can be edited through the UI.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="reset">Reset the mod's config to its default values.</param>
    /// <param name="save">Save the mod's current config to the <c>config.json</c> file.</param>
    /// <param name="titleScreenOnly">Whether the options can only be edited from the title screen.</param>
    /// <remarks>Each mod can only be registered once, unless it's deleted via <see cref="Unregister"/> before calling this again.</remarks>
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    /****
    ** Advanced
    ****/
    /// <summary>Add an option at the current position in the form using custom rendering logic.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="draw">Draw the option in the config UI. This is called with the sprite batch being rendered and the pixel position at which to start drawing.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="beforeMenuOpened">A callback raised just before the menu containing this option is opened.</param>
    /// <param name="beforeSave">A callback raised before the form's current values are saved to the config (i.e. before the <c>save</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="afterSave">A callback raised after the form's current values are saved to the config (i.e. after the <c>save</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="beforeReset">A callback raised before the form is reset to its default values (i.e. before the <c>reset</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="afterReset">A callback raised after the form is reset to its default values (i.e. after the <c>reset</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="beforeMenuClosed">A callback raised just before the menu containing this option is closed.</param>
    /// <param name="height">The pixel height to allocate for the option in the form, or <c>null</c> for a standard input-sized option. This is called and cached each time the form is opened.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    /// <remarks>The custom logic represented by the callback parameters is responsible for managing its own state if needed. For example, you can store state in a static field or use closures to use a state variable.</remarks>
    void AddComplexOption(
        IManifest mod,
        Func<string> name,
        Action<SpriteBatch, Vector2> draw,
        Func<string>? tooltip = null,
        Action? beforeMenuOpened = null,
        Action? beforeSave = null,
        Action? afterSave = null,
        Action? beforeReset = null,
        Action? afterReset = null,
        Action? beforeMenuClosed = null,
        Func<int>? height = null,
        string? fieldId = null
    );
}
