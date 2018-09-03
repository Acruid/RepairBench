using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Repair
{
    public sealed class ModRepBench : Verse.Mod
    {
        private string ticksBuffer;
        private string skillBuffer;
        private string packBuffer;

        public ModRepBench(ModContentPack mcp) : base(mcp) { }

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
            var list = new Listing_Standard(GameFont.Small);
            list.ColumnWidth = inRect.width / 2;
            var centerRect = new Rect(inRect.x + inRect.width / 4, inRect.y, inRect.width - inRect.width / 2, inRect.height).Rounded();
            list.Begin(centerRect);

            list.Gap();

            var lineRect = list.GetRect(Text.LineHeight).Rounded();
            TooltipHandler.TipRegion(lineRect, "Repair.Mcm.ResourceModeTip".Translate());
            if (Widgets.ButtonText(lineRect, "Repair.Mcm.ResourceModePrefix".Translate() + " : " + Enum.GetName(typeof(ResourceModes), Settings.ResourceMode)))
            {
                var options = new List<FloatMenuOption>();
                foreach (var num in Enum.GetValues(typeof(ResourceModes)))
                {
                    var mode = (ResourceModes)num;

                    if (mode == ResourceModes.ERROR)
                        continue;

                    options.Add(new FloatMenuOption(Enum.GetName(typeof(ResourceModes), mode).CapitalizeFirst(), () =>
                    {
                        Settings.ResourceMode = mode;
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            list.Gap(6);
            lineRect = list.GetRect(Text.LineHeight);
            TooltipHandler.TipRegion(lineRect, "Repair.Mcm.RepairRateTip".Translate());
            Widgets.TextFieldNumericLabeled(lineRect, "Repair.Mcm.RepairRateText".Translate(), ref Settings.RepairRate, ref ticksBuffer, 1);

            list.Gap(6);
            lineRect = list.GetRect(Text.LineHeight);
            TooltipHandler.TipRegion(lineRect, "Repair.Mcm.SkillMultTip".Translate());
            Widgets.TextFieldNumericLabeled(lineRect, "Repair.Mcm.SkillMultText".Translate(), ref Settings.SkillGain, ref skillBuffer, 0, 1);

            list.Gap(6);
            lineRect = list.GetRect(Text.LineHeight);
            TooltipHandler.TipRegion(lineRect, "Repair.Mcm.HpPerPackTip".Translate());
            Widgets.TextFieldNumericLabeled(lineRect, "Repair.Mcm.HpPerPackText".Translate(), ref Settings.HpPerPack, ref packBuffer, 1);

            list.Gap(6);
            list.CheckboxLabeled("Repair.Mcm.PercentModeText".Translate(), ref Settings.HpPercentage,
                "Repair.Mcm.PercentModeTip".Translate());

            list.End();
        }
    }
}
