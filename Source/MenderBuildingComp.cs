using System;
using Verse;

namespace Mending
{
    internal class MenderBuildingComp : ThingComp
    {
        public ThingFilter Allowances;
        public bool OutsideItems;
        public ThingFilter PossibleAllowances;
        public float SearchRadius;

        public MenderBuildingComp()
        {
            Allowances = null;
            PossibleAllowances = null;
            SearchRadius = 9999f;
            OutsideItems = true;
        }

        public ThingFilter GetAllowances()
        {
            if (Allowances == null)
                BuildAllowancesLists();
            return Allowances;
        }

        public ThingFilter GetPossibleAllowances()
        {
            if (PossibleAllowances == null)
                BuildAllowancesLists();
            return PossibleAllowances;
        }

        public override void PostExposeData()
        {
            Scribe_Deep.LookDeep(ref Allowances, "allowances");
            Scribe_Deep.LookDeep(ref PossibleAllowances, "possibleAllowances");

            Scribe_Values.LookValue(ref SearchRadius, "searchRadius", 9999.0f, true);
            Scribe_Values.LookValue(ref OutsideItems, "outsideItems", true, true);
        }

        private void BuildAllowancesLists()
        {
            PossibleAllowances = new ThingFilter();
            PossibleAllowances.SetDisallowAll();
            PossibleAllowances.SetAllow(SpecialThingFilterDef.Named("AllowRotten"), true);
            PossibleAllowances.SetAllow(ThingCategoryDef.Named("Weapons"), true);
            PossibleAllowances.SetAllow(ThingCategoryDef.Named("Apparel"), true);
            PossibleAllowances.SetAllow(ThingCategoryDef.Named("Items"), true);
            Allowances = new ThingFilter();
            Allowances.CopyFrom(PossibleAllowances);
            Allowances.SetAllow(ThingCategoryDef.Named("Unfinished"), false);
            PossibleAllowances.ResolveReferences();
            Allowances.ResolveReferences();
        }

        public override void PostDraw()
        {
            if (Find.Selector.SingleSelectedThing != parent || SearchRadius >= 1000.0)
                return;
            try
            {
                GenDraw.DrawRadiusRing(parent.Position, SearchRadius - 0.1f);
            }
            catch (Exception e)
            {
                // Suppress errors
            }
        }

        public void ClearAll()
        {
            Allowances.SetDisallowAll();
        }
    }
}