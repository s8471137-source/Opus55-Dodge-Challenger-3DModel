using UnityEngine;
namespace Hellcat.Game.UI
{
    /// <summary>Use a bundled font instead of relying on the host OS LegacyRuntime font.</summary>
    public static class EmbeddedUIFont
    {
        static Font font;
        public static void Apply()
        {
            if (font == null) font = Resources.Load<Font>("HellcatUI");
            if (font != null) GUI.skin.font = font;
        }
    }
}
