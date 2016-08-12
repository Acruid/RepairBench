namespace Repair
{
    internal static class Settings
    {
        internal const string JOBDEF_REPAIR = "RepairItem"; //JobDef defined in XML
        internal const string THINGDEF_REPKIT = "RepairKit"; //ThingDef defined in XML

        internal static ResourceModes ResourceMode = ResourceModes.REPAIR_KIT;

        internal static int RepairRate = 60; // game ticks per reptick
        internal const float SKILL_GAIN = 0.55f; // Skill gain per reptick
        internal const int HP_GAIN = 1; // durability regen per reptick
        internal static int HpPerPack = 5; // durability per pack
    }

    internal enum ResourceModes
    {
        ERROR = 0,
        NONE = 1,
        REPAIR_KIT = 2,
    }
}