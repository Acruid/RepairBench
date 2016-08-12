using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Repair
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    internal class JobDriver_RepairItem : JobDriver
    {
        private const TargetIndex TI_REPBENCH = TargetIndex.A;
        private const TargetIndex TI_ITEM = TargetIndex.B;
        private const TargetIndex TI_CELL = TargetIndex.C;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            //This toil is yielded later
            var gotoBillGiver = Toils_Goto.GotoThing(TI_REPBENCH, PathEndMode.InteractionCell);

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
                var extract = Toils_JobTransforms.ExtractNextTargetFromQueue(TI_ITEM);
                yield return extract;

                //Get to ingredient and pick it up
                //Note that these fail cases must be on these toils, otherwise the recipe work fails if you stacked
                //   your targetB into another object on the bill giver square.
                var getToHaulTarget = Toils_Goto.GotoThing(TI_ITEM, PathEndMode.ClosestTouch)
                    .FailOnDespawnedNullOrForbidden(TI_ITEM);
                yield return getToHaulTarget;

                yield return Toils_Haul.StartCarryThing(TI_ITEM);

                //Jump to pick up more in this run if we're collecting from multiple stacks at once
                yield return JumpToCollectNextIntoHandsForBill(getToHaulTarget, TargetIndex.B);

                //Carry ingredient to the bill giver and put it on the square
                yield return Toils_Goto.GotoThing(TI_REPBENCH, PathEndMode.InteractionCell)
                    .FailOnDestroyedOrNull(TI_ITEM);

                var findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(TI_REPBENCH, TI_ITEM, TI_CELL);
                yield return findPlaceTarget;
                yield return Toils_Haul.PlaceHauledThingInCell(TI_CELL, findPlaceTarget, false);

                //Jump back if there is another ingredient needed
                //Can happen if you can't carry all the ingredients in one run
                yield return Toils_Jump.JumpIfHaveTargetInQueue(TI_ITEM, extract);
            }

            //For it no ingredients needed, just go to the bill giver
            //This will do nothing if we took ingredients and are thus already at the bill giver
            yield return gotoBillGiver;

            var ticksToNextRepair = Settings.RepairRate;
            var repairedAmount = 0;
            var repairToil = new Toil
            {
                tickAction = () =>
                {
                    CurJob.SetTarget(TargetIndex.B, item);

                    pawn.skills.Learn(SkillDefOf.Crafting, Settings.SKILL_GAIN);
                    pawn.GainComfortFromCellIfPossible();
                    ticksToNextRepair -= (int)Math.Round(pawn.GetStatValue(StatDefOf.WorkSpeedGlobal)*table.WorkSpeedFactor);

                    if (ticksToNextRepair > 0.0)
                        return;

                    ticksToNextRepair = Settings.RepairRate;
                    item.HitPoints += Settings.HP_GAIN;
                    repairedAmount += Settings.HP_GAIN;

                    if (Settings.ResourceMode == ResourceModes.REPAIR_KIT && repairedAmount%Settings.HpPerPack == 0)
                    {
                        //TODO: investigate job.placedTargets instead of searching every time
                        Thing repkits = null;
                        foreach (var spot in table.IngredientStackCells)
                        {
                            if (!spot.IsValid || repkits != null)
                                break;

                            var list = Find.ThingGrid.ThingsListAt(spot).Where(thing => thing.def == ThingDef.Named(Settings.THINGDEF_REPKIT));
                            foreach (var thing in list)
                            {
                                repkits = thing;
                                break;
                            }
                        }

                        // out of kits
                        if (repkits == null)
                        {
                            //Technically we did not Succeed, but the job itself did not fail, we just ran out of kits.
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
                yield return new Toil
                {
                    initAction = () =>
                    {
                        IntVec3 foundCell;
                        if (!StoreUtility.TryFindBestBetterStoreCellFor(item, pawn, StoragePriority.Unstored,
                            pawn.Faction, out foundCell)) return;
                        pawn.Reserve(foundCell);
                        CurJob.SetTarget(TI_CELL, foundCell);
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
            var toil = new Toil();
            toil.initAction = () =>
            {
                const float maxDist = 8;
                var actor = toil.actor;
                var curJob = actor.jobs.curJob;
                var targetQueue = curJob.GetTargetQueue(ind);

                if (targetQueue.NullOrEmpty())
                    return;

                if (actor.carrier.CarriedThing == null)
                {
                    Log.Error("JumpToAlsoCollectTargetInQueue run on " + actor + " who is not carrying something.");
                    return;
                }

                //Find an item in the queue matching what you're carrying
                for (var i = 0; i < targetQueue.Count; i++)
                {
                    //Can't use item - skip
                    if (!GenAI.CanUseItemForWork(actor, targetQueue[i].Thing))
                        continue;

                    //Cannot stack with thing in hands - skip
                    if (!targetQueue[i].Thing.CanStackWith(actor.carrier.CarriedThing))
                        continue;

                    //Too far away - skip
                    if ((actor.Position - targetQueue[i].Thing.Position).LengthHorizontalSquared > maxDist*maxDist)
                        continue;

                    //Determine num in hands
                    var numInHands = (int) actor.carrier.CarriedThing?.stackCount;

                    //Determine num to take
                    var numToTake = curJob.numToBringList[i];
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