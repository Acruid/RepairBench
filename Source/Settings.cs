namespace Repair
{
    internal static class Settings
    {
        internal const string JOBDEF_REPAIR = "RepairItem"; //JobDef defined in XML
        internal const string THINGDEF_REPKIT = "RepairKit"; //ThingDef defined in XML

        internal const float REPAIR_RATE = 60f; // game ticks per reptick
        internal const float SKILL_GAIN = 0.55f; // Skill gain per reptick
        internal const int HP_GAIN = 1; // durability regen per reptick
        internal const int HP_PER_PACK = 5; // durability per pack
    }
}