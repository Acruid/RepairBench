using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
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

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.GetTarget(TI_REPBENCH), job, 1, -1, null, errorOnFailed))
                return false;

            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TI_ITEM), job);
            return true;
        }

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
            var itemTargetQueue = job.GetTargetQueue(TI_ITEM);

            if (itemTargetQueue.NullOrEmpty())
            {
                Log.Warning("RepBench: JobDriver - itemTargetQueue was null.");
                yield return Toils_Reserve.Release(TI_REPBENCH);
                yield return Toils_Reserve.Release(TI_ITEM);
                yield break;
            }

            var firstTargetInfo = itemTargetQueue.First();
            var item = firstTargetInfo.Thing;

            var table = job.GetTarget(TI_REPBENCH).Thing as Building_WorkTable;

            if (table == null)
            {
                Log.Warning("RepBench: JobDriver - RepairTable was null.");
                yield return Toils_Reserve.Release(TI_REPBENCH);
                yield return Toils_Reserve.Release(TI_ITEM);
                yield break;
            }

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

            float ticksToNextRepair = Settings.RepairRate;
            var repairedAmount = 0;
            var repairToil = new Toil
            {
                initAction = () =>
                {
                    Debug.PrintLine("repairToil.PreInit");
                    job.bill.Notify_DoBillStarted(pawn);
                    Debug.PrintLine("repairToil.PostInit");
                },

                tickAction = () =>
                {
//                    Debug.PrintLine("repairToil.tick.Check");
//                    pawn.jobs.CheckForJobOverride();

                    job.bill.Notify_PawnDidWork(pawn);
                    job.SetTarget(TargetIndex.B, item);

                    pawn.skills.Learn(SkillDefOf.Crafting, Settings.SkillGain);
                    pawn.GainComfortFromCellIfPossible();

                    ticksToNextRepair -= pawn.GetStatValue(StatDefOf.WorkSpeedGlobal)*table.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor);
                    if (ticksToNextRepair > 0.0)
                        return;

                    ticksToNextRepair = Settings.RepairRate;
                    item.HitPoints += Settings.HP_GAIN;
                    repairedAmount += Settings.HP_GAIN;

                    if (Settings.ResourceMode == ResourceModes.REPAIR_KIT && ShouldConsumeKit(repairedAmount, item.MaxHitPoints))
                    {
                        //TODO: investigate job.placedTargets instead of searching every time
                        Thing repkits = null;
                        foreach (var spot in table.IngredientStackCells)
                        {
                            if (!spot.IsValid || repkits != null)
                                break;

                            var list = pawn.Map.thingGrid.ThingsListAt(spot).Where(thing => thing.def == ThingDef.Named(Settings.THINGDEF_REPKIT));
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

            var itemRepairedToil = new Toil
            {
                initAction = () =>
                {
                    var list = new List<Thing> { item };
                    job.bill.Notify_IterationCompleted(pawn, list);
                    RecordsUtility.Notify_BillDone(pawn, list);
                }
            };

            yield return itemRepairedToil;

            yield return Toils_Haul.StartCarryThing(TI_ITEM);

            if (job.bill.GetStoreMode() == BillStoreModeDefOf.BestStockpile)
            {
                yield return new Toil
                {
                    initAction = () =>
                    {
                        if (!StoreUtility.TryFindBestBetterStoreCellFor(item, pawn, pawn.Map, StoragePriority.Unstored,
                            pawn.Faction, out var foundCell)) return;
                        pawn.Reserve(foundCell, job);
                        job.SetTarget(TI_CELL, foundCell);
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
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out resultingThing);
                    },
                    defaultCompleteMode = ToilCompleteMode.Instant
                };
            }

            yield return Toils_Reserve.Release(TI_REPBENCH);
        }

        private static bool ShouldConsumeKit(int repairedAmount, int maxHP)
        {
            if(Settings.HpPercentage == false)
            {
                return repairedAmount % Settings.HpPerPack == 0;
            }
            else
            {
                var hpPerPack = (int)Math.Floor(maxHP * (Settings.HpPerPack / 100.0f));
                return repairedAmount % hpPerPack == 0;
            }
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

                if (actor.carryTracker.CarriedThing == null)
                {
                    Log.Error("JumpToAlsoCollectTargetInQueue run on " + actor + " who is not carrying something.");
                    return;
                }

                if (actor.carryTracker.Full)
                    return;

                //Find an item in the queue matching what you're carrying
                for (var i = 0; i < targetQueue.Count; i++)
                {
                    //Can't use item - skip
                    if (!GenAI.CanUseItemForWork(actor, targetQueue[i].Thing))
                        continue;

                    //Cannot stack with thing in hands - skip
                    if (!targetQueue[i].Thing.CanStackWith(actor.carryTracker.CarriedThing))
                        continue;

                    //Too far away - skip
                    if ((actor.Position - targetQueue[i].Thing.Position).LengthHorizontalSquared > maxDist*maxDist)
                        continue;

                    //Determine num in hands
                    var numInHands = actor.carryTracker.CarriedThing?.stackCount ?? 0;
                    var numToTake = Mathf.Min(Mathf.Min(curJob.countQueue[i], targetQueue[i].Thing.def.stackLimit - numInHands), actor.carryTracker.AvailableStackSpace(targetQueue[i].Thing.def));

                    if (numToTake <= 0)
                        continue;

                    //Set me to go get it
                    curJob.count = numToTake;
                    curJob.SetTarget(ind, targetQueue[i].Thing);

                    List<int> intList;
                    int index2;
                    (intList = curJob.countQueue)[index2 = i] = intList[index2] - numToTake;

                    //Remove from queue if I'm going to take all
                    if (curJob.countQueue[i] == 0)
                    {
                        curJob.countQueue.RemoveAt(i);
                        targetQueue.RemoveAt(i);
                    }
                    actor.jobs.curDriver.JumpToToil(gotoGetTargetToil);
                    break;


                    /*
                    //Won't take any - skip
                    if (numToTake <= 0)
                        continue;

                    //Remove the amount to take from the num to bring list
                    curJob.countQueue[i] -= numToTake;

                    //Set me to go get it
                    curJob.maxNumToCarry = numInHands + numToTake;
                    curJob.SetTarget(ind, targetQueue[i].Thing);

                    //Remove from queue if I'm going to take all
                    if (curJob.countQueue[i] == 0)
                    {
                        curJob.countQueue.RemoveAt(i);
                        targetQueue.RemoveAt(i);
                    }

                    actor.jobs.curDriver.JumpToToil(gotoGetTargetToil);
                    return;
                    */
                }
            };

            return toil;
        }
    }
}