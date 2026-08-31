using StardewValley;

namespace StardewControllerMenu.Framework
{
    /// <summary>Small text-fitting helpers shared by every menu, so control hints and status lines never draw outside a menu's bounds regardless of how long a mod/preset/profile name turns out to be.</summary>
    internal static class TextLayout
    {
        /// <summary>Shrink text with an ellipsis if it's wider than the given pixel width using the game's small font.</summary>
        public static string FitToWidth(string text, float maxWidth)
        {
            if (Game1.smallFont.MeasureString(text).X <= maxWidth)
                return text;

            const string ellipsis = "...";
            while (text.Length > 0 && Game1.smallFont.MeasureString(text + ellipsis).X > maxWidth)
                text = text[..^1];
            return text + ellipsis;
        }
    }
}
