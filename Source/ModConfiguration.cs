using System;
using System.Collections.Generic;
using CommunityCoreLibrary;
using CommunityCoreLibrary.UI;
using UnityEngine;
using Verse;

namespace Repair
{
#if USE_CCL
    /// <summary>
    /// Adds mod configuration settings to MCM using CCL.
    /// Credit: Based off of the Fluffy Tabs Work UI mod.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public class ModConfiguration : ModConfigurationMenu
    {
        private readonly LabeledTextboxIntClamped _repairRateTextbox = new LabeledTextboxIntClamped(Settings.RepairRate, "Repair.Mcm.RepairRateText".Translate(), new IntRange(1, 1200), "Repair.Mcm.RepairRateTip".Translate());
        private readonly LabeledTextboxIntClamped _hpPerPackTextbox = new LabeledTextboxIntClamped(Settings.HpPerPack, "Repair.Mcm.HpPerPackText".Translate(), new IntRange(1, 200), "Repair.Mcm.HpPerPackTip".Translate());

        /// <summary>
        /// Paints the window contents.
        /// </summary>
        /// <param name="rect">Client window area.</param>
        /// <returns></returns>
        public override float DoWindowContents(Rect rect)
        {
            var optionRect = new Rect(rect.xMin, rect.yMin, rect.width, rect.height + 30f);
            
            //Todo: Seperate option names from enum names.
            if (Widgets.ButtonText(optionRect, "Repair.Mcm.ResourceModePrefix".Translate() + " : " + Enum.GetName(typeof(ResourceModes), Settings.ResourceMode)))
            {
                var options = new List<FloatMenuOption>();
                foreach (var num in Enum.GetValues(typeof(ResourceModes)))
                {
                    ResourceModes mode = (ResourceModes)num;

                    if (mode == ResourceModes.ERROR)
                        continue;

                    options.Add(new FloatMenuOption(Enum.GetName(typeof(ResourceModes), mode).CapitalizeFirst(), () =>
                    {
                        Settings.ResourceMode = mode;
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            TooltipHandler.TipRegion(optionRect, "Repair.Mcm.ResourceModeTip".Translate());
            
            optionRect.y += 40f;
            _repairRateTextbox.Draw(optionRect);
            Settings.RepairRate = _repairRateTextbox.Value;

            optionRect.y += 40f;
            _hpPerPackTextbox.Draw(optionRect);
            Settings.HpPerPack = _hpPerPackTextbox.Value;

            return 0;
        }

        /// <summary>
        /// Serializeable data.
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.LookValue(ref Settings.ResourceMode, "ResourceMode", ResourceModes.REPAIR_KIT);
            Scribe_Values.LookValue(ref Settings.RepairRate, "RepairRate");
            Scribe_Values.LookValue(ref Settings.HpPerPack, "HpPerPack");
        }

        private class LabeledTextboxIntClamped : LabeledInput_Int
        {
            private IntRange _range;

            public LabeledTextboxIntClamped(int value, string label, IntRange range, string tip = "") : base(value, label, tip)
            {
                _range = range;
                validator = Validator;
            }

            private bool Validator(string s)
            {
                int result;
                return int.TryParse(s, out result) && (_range.min <= result && result <= _range.max);
            }
        }
    }
#endif
}
