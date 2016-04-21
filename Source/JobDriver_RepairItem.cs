using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Repair
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    internal class JobDriver_RepairItem : JobDriver
    {
        private const string THINGDEF_REPKIT = "RepairKit";
        private const float REPAIR_RATE = 60f; // game ticks per reptick
        private const float SKILL_GAIN = 0.55f; // Skill gain per reptick
        private const int HP_GAIN = 1; // durability regen per reptick
        private const int HP_PER_PACK = 5; // durability per pack

        private const TargetIndex TI_REPBENCH = TargetIndex.A;
        private const TargetIndex TI_ITEM = TargetIndex.B;
        private const TargetIndex TI_CELL = TargetIndex.C;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            const TargetIndex BillGiverInd = TI_REPBENCH;
            const TargetIndex IngredientInd = TI_ITEM;
            const TargetIndex IngredientPlaceCellInd = TI_CELL;

            //This toil is yielded later
            Toil gotoBillGiver = Toils_Goto.GotoThing(BillGiverInd, PathEndMode.InteractionCell);

            this.FailOnDestroyedNullOrForbidden(TI_REPBENCH);
            this.FailOnBurningImmobile(TI_REPBENCH);

            //Reserve the bill giver and all the ingredients
            yield return Toils_Reserve.Reserve(TI_REPBENCH);
            yield return Toils_Reserve.ReserveQueue(TI_ITEM);

            //these are initially set up by workgiver
            var item = CurJob.GetTargetQueue(TI_ITEM)[0].Thing;
            var table = CurJob.GetTarget(TI_REPBENCH).Thing as Building_RepairTable;

            if (table == null)
                throw new Exception("RepBench: JobDriver - RepairTable was null.");

            //Gather ingredients
            {
                //Extract an ingredient into TargetB
                var extract = Toils_JobTransforms.ExtractNextTargetFromQueue(IngredientInd);
                yield return extract;

                //Get to ingredient and pick it up
                //Note that these fail cases must be on these toils, otherwise the recipe work fails if you stacked
                //   your targetB into another object on the bill giver square.
                var getToHaulTarget = Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch)
                                        .FailOnDespawnedNullOrForbidden(IngredientInd);
                yield return getToHaulTarget;

                yield return Toils_Haul.StartCarryThing(IngredientInd);

                //Jump to pick up more in this run if we're collecting from multiple stacks at once
                //Todo bring this back
                yield return JumpToCollectNextIntoHandsForBill(getToHaulTarget, TargetIndex.B);

                //Carry ingredient to the bill giver and put it on the square
                yield return Toils_Goto.GotoThing(BillGiverInd, PathEndMode.InteractionCell)
                                        .FailOnDestroyedOrNull(IngredientInd);

                var findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(BillGiverInd, IngredientInd, IngredientPlaceCellInd);
                yield return findPlaceTarget;
                yield return Toils_Haul.PlaceHauledThingInCell(IngredientPlaceCellInd, findPlaceTarget, false);

                //Jump back if there is another ingredient needed
                //Can happen if you can't carry all the ingredients in one run
                yield return Toils_Jump.JumpIfHaveTargetInQueue(IngredientInd, extract);
            }

            //For it no ingredients needed, just go to the bill giver
            //This will do nothing if we took ingredients and are thus already at the bill giver
            yield return gotoBillGiver;
            
            var ticksToNextRepair = REPAIR_RATE;
            var repairedAmount = 0;
            var repairToil = new Toil
            {
                tickAction = () =>
                {

                    CurJob.SetTarget(TargetIndex.B, item);

                    pawn.skills.Learn(SkillDefOf.Crafting, SKILL_GAIN);
                    pawn.GainComfortFromCellIfPossible();
                    ticksToNextRepair -= pawn.GetStatValue(StatDefOf.WorkSpeedGlobal)*table.WorkSpeedFactor;

                    if (ticksToNextRepair > 0.0)
                        return;

                    ticksToNextRepair = REPAIR_RATE;
                    item.HitPoints += HP_GAIN;
                    repairedAmount += HP_GAIN;

                    if (repairedAmount % HP_PER_PACK == 0)
                    {
                        //TODO: investigate job.placedTargets instead of searching every time
                        Thing repkits = null;
                        foreach (var spot in table.IngredientStackCells)
                        {
                            if(!spot.IsValid || repkits != null)
                                break;

                            var list = Find.ThingGrid.ThingsListAt(spot);
                            foreach (var thing in list)
                            {
                                if (thing.def == ThingDef.Named(THINGDEF_REPKIT))
                                {
                                    repkits = thing;
                                    break;
                                }
                            }
                        }

                        if (repkits == null)
                        {
                            EndJobWith(JobCondition.Succeeded);
                            return;
                        }

                        if (repkits.stackCount > 1)
                        {
                            var kit = repkits.SplitOff(1);
                            kit.Destroy();
                        }
                        else
                        {
                            repkits.DeSpawn();
                            repkits.Destroy();
                            EndJobWith(JobCondition.Succeeded);
                            return;
                        }
                    }

                    if (item.HitPoints < item.MaxHitPoints)
                        return;

                    // break
                    ReadyForNextToil();
                },
                defaultCompleteMode = ToilCompleteMode.Never
            };
            repairToil.WithEffect(item.def.repairEffect, TI_ITEM);
            yield return repairToil;

            yield return Toils_Haul.StartCarryThing(TI_ITEM);
            
            if (table.HaulStockpile)
            {
                yield return new Toil()
                {
                    initAction = () =>
                    {
                        IntVec3 foundCell;
                        if (StoreUtility.TryFindBestBetterStoreCellFor(item, pawn, StoragePriority.Unstored, pawn.Faction, out foundCell))
                        {
                            pawn.Reserve(foundCell);
                            CurJob.SetTarget(TI_CELL, foundCell);
                        }
                    }
                };
            
                yield return Toils_Reserve.Reserve(TI_CELL);

                yield return Toils_Haul.CarryHauledThingToCell(TI_CELL);

                yield return Toils_Haul.PlaceHauledThingInCell(TI_CELL, null, true);

                yield return Toils_Reserve.Release(TI_CELL);
            }
            else
            {
                yield return new Toil
                {
                    initAction = () =>
                    {
                        Thing resultingThing;
                        pawn.carrier.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out resultingThing);
                    },
                    defaultCompleteMode = ToilCompleteMode.Instant
                };
            }

            yield return Toils_Reserve.Release(TI_REPBENCH);
        }

        private static Toil JumpToCollectNextIntoHandsForBill(Toil gotoGetTargetToil, TargetIndex ind)
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                const float MaxDist = 8;
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                List<TargetInfo> targetQueue = curJob.GetTargetQueue(ind);

                if (targetQueue.NullOrEmpty())
                    return;

                if (actor.carrier.CarriedThing == null)
                {
                    Log.Error("JumpToAlsoCollectTargetInQueue run on " + actor + " who is not carrying something.");
                    return;
                }

                //Find an item in the queue matching what you're carrying
                for (int i = 0; i < targetQueue.Count; i++)
                {
                    //Can't use item - skip
                    if (!GenAI.CanUseItemForWork(actor, targetQueue[i].Thing))
                        continue;

                    //Cannot stack with thing in hands - skip
                    if (!targetQueue[i].Thing.CanStackWith(actor.carrier.CarriedThing))
                        continue;

                    //Too far away - skip
                    if ((actor.Position - targetQueue[i].Thing.Position).LengthHorizontalSquared > MaxDist*MaxDist)
                        continue;

                    //Determine num in hands
                    int numInHands = (actor.carrier.CarriedThing == null) ? 0 : actor.carrier.CarriedThing.stackCount;

                    //Determine num to take
                    int numToTake = curJob.numToBringList[i];
                    if (numToTake + numInHands > targetQueue[i].Thing.def.stackLimit)
                        numToTake = targetQueue[i].Thing.def.stackLimit - numInHands;

                    //Won't take any - skip
                    if (numToTake == 0)
                        continue;

                    //Remove the amount to take from the num to bring list
                    curJob.numToBringList[i] -= numToTake;

                    //Set me to go get it
                    curJob.maxNumToCarry = numInHands + numToTake;
                    curJob.SetTarget(ind, targetQueue[i].Thing);

                    //Remove from queue if I'm going to take all
                    if (curJob.numToBringList[i] == 0)
                    {
                        curJob.numToBringList.RemoveAt(i);
                        targetQueue.RemoveAt(i);
                    }

                    actor.jobs.curDriver.JumpToToil(gotoGetTargetToil);
                    return;
                }
            };

            return toil;
        }
    }
}