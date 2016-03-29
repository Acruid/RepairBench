using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mending
{
    // ReSharper disable once InconsistentNaming
    internal class WorkGiver_Mending : WorkGiver_Scanner
    {
        private MenderBuildingComp _mbc;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDef.Named("TableMender"));

        public override Job JobOnThing(Pawn menderPawn, Thing menderTableThing)
        {
            var buildingWorkTable = menderTableThing as Building_WorkTable;

            if (buildingWorkTable == null)
                return null;

            _mbc = menderTableThing.TryGetComp<MenderBuildingComp>();

            if (_mbc == null || !menderPawn.CanReserveAndReach(menderTableThing, PathEndMode.Touch, menderPawn.NormalMaxDanger()))
                return null;

            var compPowerTrader = (buildingWorkTable).GetComp<CompPowerTrader>();
            if (compPowerTrader != null && !compPowerTrader.PowerOn)
                return null;
            try
            {
                Thing thing = GenClosest.ClosestThingReachable(menderTableThing.Position,
                    ThingRequest.ForGroup((ThingRequestGroup)4),
                    PathEndMode.Touch,
                    TraverseParms.For(menderPawn, menderPawn.NormalMaxDanger()),
                    _mbc.SearchRadius / 2f,
                    SearchPredicate);

                if (thing == null)
                    return null;

                IntVec3 invalid;
                if (
                    !StoreUtility.TryFindBestBetterStoreCellFor(thing, menderPawn, 0, menderPawn.Faction, out invalid))
                    invalid = IntVec3.Invalid;
                else
                    menderPawn.Reserve(invalid);

                var job = new Job(DefDatabase<JobDef>.GetNamed("MendItem"), menderTableThing, thing)
                {
                    maxNumToCarry = 1
                };

                if (invalid != IntVec3.Invalid)
                    job.targetC = invalid;

                return job;
            }
            catch (Exception ex)
            {
                Log.Error("Mending mod: JobOnThing: " + ex.Message);
            }
            return null;
        }

        private bool SearchPredicate(Thing t)
        {
            try
            {
                if (!_mbc.GetAllowances().Allows(t))
                    return false;

                if (t.HitPoints <= 0 || t.HitPoints >= t.MaxHitPoints)
                    return false;

                if (Find.Reservations.FirstReserverOf(t, Faction.OfColony) != null)
                    return false;

                if (t.IsForbidden(Faction.OfColony))
                    return false;

                if (t.IsBurning())
                    return false;

                if (!_mbc.OutsideItems && !Find.RoofGrid.Roofed(t.Position))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Mending mod: SearchPredicate: " + ex.Message);
            }
            return false;
        }
    }
}