#nullable enable
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// ADR-066 Barlow Condensed bank for IMGUI. Falls back to the engine skin font if a face is missing.
    /// </summary>
    public static class UiFonts
    {
        private static Font? body;
        private static Font? button;
        private static Font? display;
        private static bool resolved;
        private static bool warned;

        public static Font? Body
        {
            get
            {
                Ensure();
                return body;
            }
        }

        public static Font? Button
        {
            get
            {
                Ensure();
                return button ?? body;
            }
        }

        public static Font? Display
        {
            get
            {
                Ensure();
                return display ?? button ?? body;
            }
        }

        /// <summary>Pick display (≥22) vs body face for labels.</summary>
        public static Font? ForLabelSize(int size) => size >= 22 ? Display : Body;

        private static void Ensure()
        {
            if (resolved) return;
            resolved = true;
            body = Resources.Load<Font>("BarlowCondensed-Regular");
            button = Resources.Load<Font>("BarlowCondensed-SemiBold");
            display = Resources.Load<Font>("BarlowCondensed-Bold");
            if ((body == null || button == null || display == null) && !warned)
            {
                warned = true;
                Debug.LogWarning("SHOOTER_UI fonts: Barlow Condensed bank incomplete; using engine IMGUI fallback where needed.");
            }
        }
    }
}
