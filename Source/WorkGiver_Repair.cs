using RimWorld;
using Verse;
using Verse.AI;

namespace Repair
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    internal class WorkGiver_Repair : WorkGiver_Scanner
    {
        private const string JOBDEF_REPAIR = "RepairItem";

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(def.fixedBillGiverDefs[0]);

        public override Job JobOnThing(Pawn repPawn, Thing thingRepBench)
        {
            var repBench = thingRepBench as Building_RepairTable;
            if (repBench == null)
                return null;

            if (repBench.Suspended || !repBench.UsableNow)
                return null;

            if (!repPawn.CanReserveAndReach(repBench, PathEndMode.Touch, repPawn.NormalMaxDanger()) ||
                repBench.IsBurning() || repBench.IsForbidden(repPawn))
                return null;

            var thing = GenClosest.ClosestThingReachable(repBench.Position,
                ThingRequest.ForGroup((ThingRequestGroup) 4),
                PathEndMode.Touch,
                TraverseParms.For(repPawn, repPawn.NormalMaxDanger()),
                repBench.SearchRadius/2f,
                item =>
                {
                    if (!repBench.GetStoreSettings().filter.Allows(item))
                        return false;

                    if (item.HitPoints <= 0 || item.HitPoints >= item.MaxHitPoints)
                        return false;

                    if (item.IsForbidden(repPawn))
                        return false;

                    if (!repPawn.CanReserve(item))
                        return false;

                    if (item.IsBurning())
                        return false;

                    if (!repBench.OutsideItems && !Find.RoofGrid.Roofed(item.Position))
                        return false;

                    return true;
                });

            if (thing == null)
                return null;

            var job = new Job(DefDatabase<JobDef>.GetNamed(JOBDEF_REPAIR), repBench, thing)
            {
                maxNumToCarry = 1
            };

            if (repBench.HaulStockpile)
            {
                IntVec3 foundCell;
                if (StoreUtility.TryFindBestBetterStoreCellFor(thing, repPawn, 0, repPawn.Faction, out foundCell))
                {
                    repPawn.Reserve(foundCell);
                    job.targetC = foundCell;
                }
            }

            return job;
        }
    }
}