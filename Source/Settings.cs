using System;
using System.IO;
using System.Reflection;
using System.Xml;
using Verse;

namespace Repair
{
    internal class Settings : ModSettings
    {
        // Static constructor is called 'sometime' before the first member access
        static Settings()
        {
            var configPath = Path.Combine(GenFilePaths.SaveDataFolderPath, @"Config\RepairBench.xml");

            if (!File.Exists(configPath))
            {
                Debug.PrintLine($"RepairBench: Creating config file: {configPath}");

                var doc = new XmlDocument();

                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "UTF-8", null));

                var settingsNode = doc.CreateElement(typeof(Settings).Name);
                doc.AppendChild(settingsNode);

                var fieldsInfo = typeof(Settings).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fieldsInfo)
                {
                    // filter constants
                    if (field.IsLiteral)
                        continue;

                    var value = field.GetValue(null);
                    var str = (string) Convert.ChangeType(value, typeof(string));

                    XmlNode nameNode = doc.CreateElement(field.Name);
                    nameNode.AppendChild(doc.CreateTextNode(str));
                    settingsNode.AppendChild(nameNode);
                }

                doc.Save(configPath);
            }
            else
            {
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

                            var value = fieldInfo.FieldType.IsEnum ? 
                                Enum.Parse(fieldInfo.FieldType, node.InnerText) : 
                                Convert.ChangeType(node.InnerText, fieldInfo.FieldType);

                            fieldInfo.SetValue(null, value);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"RepairBench: LoadXML: {e}");
                }
            }
        }

        internal const string MOD_NAME = "Repair Workbench";

        #region Settings

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
    }

    internal enum ResourceModes
    {
        ERROR = 0,
        NONE = 1,
        REPAIR_KIT = 2,
        INGREDIENTS = 3
    }
}