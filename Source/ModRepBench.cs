using UnityEngine;
using Verse;

namespace Repair
{
    public sealed class ModRepBench : Verse.Mod
    {
        public ModSettings settings;


        public ModRepBench(ModContentPack mcp) : base(mcp)
        {
            settings = GetSettings<Settings>();
        }

        /// <summary>
        ///     Adds the mod to the ModSettings system.
        /// </summary>
        public override string SettingsCategory()
        {
            return Settings.MOD_NAME;
        }
        
        /// <summary>
        ///     Draws the contents of the settings window to the screen.
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }
}
