using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEngine;
using Verse;

namespace Repair
{
    internal class Settings : ModSettings
    {
        // Static constructor is called 'sometime' before the first member access
        static Settings()
        {
            var configPath = Path.Combine(GenFilePaths.SaveDataFolderPath, @"Config/RepairBench.xml");

            if (!File.Exists(configPath))
                return;

            try
            {
                Debug.PrintLine($"RepairBench: Loading config file: {configPath}");

                var doc = new XmlDocument();
                doc.LoadXml(File.ReadAllText(configPath));

                if (doc.DocumentElement == null)
                    return;

                Debug.PrintLine(doc.DocumentElement.Name);

                foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                {
                    // ignore comments
                    if (node is XmlComment)
                        continue;

                    // if this looks like a val (it should be)
                    if (node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Text)
                    {
                        var fieldInfo = typeof(Settings).GetField(node.Name,
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                        if (fieldInfo == null)
                            continue;

                        var value = fieldInfo.FieldType.IsEnum ? Enum.Parse(fieldInfo.FieldType, node.InnerText) : Convert.ChangeType(node.InnerText, fieldInfo.FieldType);

                        fieldInfo.SetValue(null, value);
                    }
                }

                // remove old xml file, we don't need it anymore
                File.Delete(configPath);
            }
            catch (Exception e)
            {
                Log.Error($"RepairBench: LoadXML: {e}");
            }
        }

        internal const string MOD_NAME = "Repair Workbench";

        #region Settings

        internal static bool DebugEnabled = false;

        internal const string JOBDEF_REPAIR = "RepairItem"; //JobDef defined in XML
        internal const string THINGDEF_REPKIT = "RepairKit"; //ThingDef defined in XML

        internal static ResourceModes ResourceMode = ResourceModes.REPAIR_KIT;

        internal static int RepairRate = 60; // game ticks per reptick
        internal static float SkillGain = 0.55f; // Skill gain per reptick
        internal const int HP_GAIN = 1; // durability regen per reptick

        internal static bool HpPercentage = false; // is HpPerPack a percentage, or a flat value?
        internal static int HpPerPack = 5; // durability per kit
        internal const float INGRED_REPAIR_PERCENT = 1.0f; // percentage of ingredients required to repair item 100%

        #endregion

        /// <summary>
        ///     Serializes mod settings.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref HpPercentage, "percentMode");
            Scribe_Values.Look(ref ResourceMode, "resMode", ResourceModes.REPAIR_KIT);
            Scribe_Values.Look(ref RepairRate, "repairRate", 60);
            Scribe_Values.Look(ref SkillGain, "skillGain", 0.55f);
            Scribe_Values.Look(ref HpPerPack, "HpPerPack", 5);
            Scribe_Values.Look(ref DebugEnabled, "debug");
        }

        public static void DoSettingsWindowContents(Rect inRect)
        {
            string ticksBuffer = null;
            string skillBuffer = null;
            string packBuffer = null;

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

                    options.Add(new FloatMenuOption(Enum.GetName(typeof(ResourceModes), mode).CapitalizeFirst(), () => { Settings.ResourceMode = mode; }));
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
            list.CheckboxLabeled("Repair.Mcm.PercentModeText".Translate(), ref Settings.HpPercentage, "Repair.Mcm.PercentModeTip".Translate());

            list.End();
        }
    }

    internal enum ResourceModes
    {
        ERROR = 0,
        NONE = 1,
        REPAIR_KIT = 2,
        INGREDIENTS = 3
    }
}