using Microsoft.Xna.Framework;

namespace DynamicItemSpriteCompositor.Integration;

public interface IExtraMachineConfigApi
{
    // Returns the override color for the provided UNqualified item ID.
    // If there are no overrides, returns null.
    public Color? GetColorOverride(string unqualifiedItemId);
}
