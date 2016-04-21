using System.Collections.Generic;
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
        private const string THINGDEF_REPKIT = "RepairKit";

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

            //TODO: Haul stuff off table


            var damagedThing = GenClosest.ClosestThingReachable(repBench.Position,
                ThingRequest.ForGroup((ThingRequestGroup) 4),
                PathEndMode.Touch,
                TraverseParms.For(repPawn, repPawn.NormalMaxDanger()),
                repBench.SearchRadius/2f,
                item =>
                {
                    if (!repBench.GetStoreSettings().AllowedToAccept(item))
                        return false;

                    //TODO: bug workaround, investigate why the parent settings are not being applied to the repbench StoreSettings
                    if (!repBench.GetParentStoreSettings().AllowedToAccept(item))
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

                    // Log.Warning(String.Format("RepBench: Item={0}, CurAllowed={1}, DefAllowed={2}", item, repBench.GetStoreSettings().filter.Allows(item), repBench.GetParentStoreSettings().filter.Allows(item)));
                    return true;
                });

            if (damagedThing == null)
                return null;
            
            var repKit = GenClosest.ClosestThingReachable(repBench.Position,
                ThingRequest.ForDef(ThingDef.Named(THINGDEF_REPKIT)),
                PathEndMode.OnCell,
                TraverseParms.For(repPawn, repPawn.NormalMaxDanger()), 
                9999f,
                item => !item.IsForbidden(repPawn) && HaulAIUtility.PawnCanAutomaticallyHaulFast(repPawn, item));

            if (repKit == null)
                return null;

            var job = new Job(DefDatabase<JobDef>.GetNamed(JOBDEF_REPAIR), repBench);
            job.targetQueueB = new List<TargetInfo>(2);
            job.numToBringList = new List<int>(2);

            job.targetQueueB.Add(damagedThing);
            job.numToBringList.Add(1);
            job.SetTarget(TargetIndex.B, damagedThing);

            job.targetQueueB.Add(repKit);
            job.numToBringList.Add(20);

            job.haulMode = HaulMode.ToCellNonStorage;

            return job;
        }
    }
}