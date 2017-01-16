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
    internal class WorkGiver_Repair : WorkGiver_Scanner
    {
        private readonly List<ThingAmount> chosenIngThings;
        private static readonly IntRange ReCheckFailedBillTicksRange;
        private static string _missingMaterialsTranslated;
        private static string _missingSkillTranslated;
        private static readonly List<Thing> RelevantThings;
        private static readonly List<Thing> NewRelevantThings;
        private static readonly HashSet<Thing> AssignedThings;
        private static readonly DefCountList AvailableCounts;

        public WorkGiver_Repair()
        {
            chosenIngThings = new List<ThingAmount>();
            if (_missingSkillTranslated == null)
                _missingSkillTranslated = "MissingSkill".Translate();
            if (_missingMaterialsTranslated != null)
                return;
            _missingMaterialsTranslated = "MissingMaterials".Translate();
        }

        static WorkGiver_Repair()
        {
            ReCheckFailedBillTicksRange = new IntRange(500, 600);
            RelevantThings = new List<Thing>();
            NewRelevantThings = new List<Thing>();
            AssignedThings = new HashSet<Thing>();
            AvailableCounts = new DefCountList();
        }

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                if (def.fixedBillGiverDefs != null && def.fixedBillGiverDefs.Count == 1)
                    return ThingRequest.ForDef(def.fixedBillGiverDefs[0]);

                return ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);
            }
        }

        public override Job JobOnThing(Pawn pawn, Thing bench)
        {
            var giver = bench as IBillGiver;

            if (giver == null || !ThingIsUsableBillGiver(bench) || !giver.CurrentlyUsable() || !giver.BillStack.AnyShouldDoNow || bench.IsBurning() || bench.IsForbidden(pawn))
                return null;

            if (!pawn.CanReserve(bench))
                return null;

            if (!pawn.CanReserveAndReach(bench.InteractionCell, PathEndMode.OnCell, Danger.Some))
                return null;
            
            giver.BillStack.RemoveIncompletableBills();

            // clears off workbench
            var jobHaul = WorkGiverUtility.HaulStuffOffBillGiverJob(pawn, giver, null);
            if (jobHaul != null)
                return jobHaul;

            foreach (var bill in giver.BillStack)
            {
                if ((bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType) || 
                    (Find.TickManager.TicksGame < bill.lastIngredientSearchFailTicks + ReCheckFailedBillTicksRange.RandomInRange && !FloatMenuMakerMap.making) || 
                    !bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn))
                    continue;

                if (!bill.recipe.PawnSatisfiesSkillRequirements(pawn))
                {
                    JobFailReason.Is(_missingSkillTranslated);
                    return null;
                }

                var damagedItems = FindDamagedItems(pawn, bench, bill);
                if (damagedItems == null)
                {
                    JobFailReason.Is(_missingMaterialsTranslated);
                    return null;
                }
                
                Debug.PrintList("JobOnThing.damagedItems", damagedItems);

                foreach (var item in damagedItems)
                {
                    if (TryFindBestBillIngredients(bill, pawn, bench, chosenIngThings, item))
                        return StartNewRepairJob(bill, giver, item, chosenIngThings);
                }

                if (!FloatMenuMakerMap.making)
                    bill.lastIngredientSearchFailTicks = Find.TickManager.TicksGame;
            }
            
            JobFailReason.Is(_missingMaterialsTranslated);
            return null;
        }

        /// <summary>
        /// Find all accessable damaged items.
        /// </summary>
        /// <param name="pawn">The pawn to fetch the item.</param>
        /// <param name="bench">Starting point of the search.</param>
        /// <param name="bill">The workbill for repairing the item.</param>
        /// <returns>List of all accessable damaged items.</returns>
        private static List<Thing> FindDamagedItems(Pawn pawn, Thing bench, Bill bill)
        {
            List<Thing> validItems = new List<Thing>();
            List<Thing> relevantItems = new List<Thing>();

            //get the root region that the bench is in.
            Region rootRegion = pawn.Map.regionGrid.GetValidRegionAt(GetBillGiverRootCell(bench, pawn));
            if (rootRegion == null)
                return validItems;
            
            //Predicate: if the pawn can enter the region from the root, consider checking it.
            RegionEntryPredicate regionEntryCondition = (from, to) => to.Allows(TraverseParms.For(pawn), false);

            //Predicate: if the item is damaged and valid.
            Predicate<Thing> itemValidator = item =>
            {
                if (!item.Spawned)
                    return false;

                if (!bill.ingredientFilter.Allows(item))
                    return false;

                if (0 >= item.HitPoints || item.HitPoints >= item.MaxHitPoints)
                    return false;

                if ((item.Position - bench.Position).LengthHorizontalSquared >= bill.ingredientSearchRadius * (double) bill.ingredientSearchRadius)
                    return false;

                if (item.IsForbidden(pawn))
                    return false;

                if (!pawn.CanReserve(item))
                    return false;

                if (item.IsBurning())
                    return false;
                
                return true;
            };

            //Delegate: process the current region being scanned.
            RegionProcessor regionProcessor = region =>
            {
                // gets a list of haulable things from the region.
                var regionItems = region.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways));
                
                // find out which of these items are relevant.
                relevantItems.AddRange(regionItems.Where(item => itemValidator(item)));

                // nothing valid in this region
                if (relevantItems.Count <= 0)
                    return false;

                // Comparison: which item is closer to the pawn?
                Comparison<Thing> comparison = (t1, t2) =>
                        (t1.Position - pawn.Position).LengthHorizontalSquared
                        .CompareTo((t2.Position - pawn.Position).LengthHorizontalSquared);
                
                relevantItems.Sort(comparison);

                validItems.AddRange(relevantItems);

                relevantItems.Clear();
                
                // returning true stops the traverse, we want to exaust it
                return false;
            };
            
            // run the region traverse
            RegionTraverser.BreadthFirstTraverse(rootRegion, regionEntryCondition, regionProcessor, 99999);

            return validItems;
        }

        /// <summary>
        /// Find the first accessable damaged item.
        /// </summary>
        /// <param name="pawn">The pawn to fetch the item.</param>
        /// <param name="bench">Starting point of the search.</param>
        /// <param name="bill">The workbill for repairing the item.</param>
        /// <returns>List of first accessible damaged item.</returns>
        private static List<Thing> FindFirstDamagedItem(Pawn pawn, Thing bench, Bill bill)
        {
            var damagedThing = GenClosest.ClosestThingReachable(bench.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways),
                PathEndMode.Touch,
                TraverseParms.For(pawn, pawn.NormalMaxDanger()),
                bill.ingredientSearchRadius,
                item =>
                {
                    if (!bill.ingredientFilter.Allows(item))
                        return false;

                    if (item.HitPoints <= 0 || item.HitPoints >= item.MaxHitPoints)
                        return false;

                    if (item.IsForbidden(pawn))
                        return false;

                    if (!pawn.CanReserve(item))
                        return false;

                    if (item.IsBurning())
                        return false;

                    return true;
                });

            return new List<Thing>(1) { damagedThing };
        }

        /// <summary>
        /// Checks to see if the Thing is a proper BillGiver.
        /// </summary>
        /// <param name="thing">The thing to check.</param>
        /// <returns></returns>
        private bool ThingIsUsableBillGiver(Thing thing)
        {
            var pawn1 = thing as Pawn;
            var corpse = thing as Corpse;
            Pawn pawn2 = null;
            if (corpse != null)
                pawn2 = corpse.InnerPawn;
            return def.fixedBillGiverDefs != null && def.fixedBillGiverDefs.Contains(thing.def) ||
                   pawn1 != null &&
                   (def.billGiversAllHumanlikes && pawn1.RaceProps.Humanlike || def.billGiversAllMechanoids && pawn1.RaceProps.IsMechanoid ||
                    def.billGiversAllAnimals && pawn1.RaceProps.Animal) ||
                   corpse != null && pawn2 != null &&
                   (def.billGiversAllHumanlikesCorpses && pawn2.RaceProps.Humanlike ||
                    def.billGiversAllMechanoidsCorpses && pawn2.RaceProps.IsMechanoid || def.billGiversAllAnimalsCorpses && pawn2.RaceProps.Animal);
        }

        private static bool TryFindBestBillIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingAmount> chosen, Thing itemDamaged)
        {
            chosen.Clear();

            var neededIngreds = CalculateTotalIngredients(itemDamaged);
            if (neededIngreds == null)
                return true;
            
            Debug.PrintList("FindBest.neededIngreds", neededIngreds);

            // free repair!
            if (neededIngreds.Count == 0)
                return true;

            var rootRegion = pawn.Map.regionGrid.GetValidRegionAt(GetBillGiverRootCell(billGiver, pawn));
            if (rootRegion == null)
                return false;
            //MakeIngredientsListInProcessingOrder(ingredientsOrdered, bill)
            
            RelevantThings.Clear();
            var foundAll = false;
            
            Predicate<Thing> baseValidator = t =>
            {
                if (!t.Spawned || t.IsForbidden(pawn) || 
                (t.Position - billGiver.Position).LengthHorizontalSquared >= bill.ingredientSearchRadius*(double) bill.ingredientSearchRadius || 
                !neededIngreds.Any( ingred => ingred.ThingDef == t.def) ||
                !pawn.CanReserve(t))
                    return false;

                return !bill.CheckIngredientsIfSociallyProper || t.IsSociallyProper(pawn);
            };
            
            var billGiverIsPawn = billGiver is Pawn;
            
            RegionProcessor regionProcessor = r =>
            {
                NewRelevantThings.Clear();
                var thingList = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                foreach (var thing in thingList)
                {
                    if (baseValidator(thing) && (!thing.def.IsMedicine || !billGiverIsPawn))
                        NewRelevantThings.Add(thing);
                }

                if (NewRelevantThings.Count <= 0)
                    return false;

                Comparison<Thing> comparison =
                    (t1, t2) =>
                        (t1.Position - pawn.Position).LengthHorizontalSquared.CompareTo((t2.Position - pawn.Position).LengthHorizontalSquared);
                NewRelevantThings.Sort(comparison);
                RelevantThings.AddRange(NewRelevantThings);
                NewRelevantThings.Clear();
                if (TryFindBestBillIngredientsInSet_NoMix(RelevantThings, neededIngreds, chosen))
                {
                    foundAll = true;
                    return true;
                }
                return false;
            };
            
            RegionEntryPredicate entryCondition = (from, to) => to.Allows(TraverseParms.For(pawn), false);
            
            RegionTraverser.BreadthFirstTraverse(rootRegion, entryCondition, regionProcessor, 99999);
            
            Debug.PrintLine("foundall " + foundAll);
            Debug.PrintList("FindBest.chosen", chosen);

            return foundAll;
        }

        /// <summary>
        /// Calculate the total number of ingredients needed to fully repair an item.
        /// </summary>
        /// <param name="itemDamaged">The item to be repaired.</param>
        /// <returns></returns>
        private static List<ThingCount> CalculateTotalIngredients(Thing itemDamaged)
        {
            List<ThingCount> totalCost;
            switch (Settings.ResourceMode)
            {
                case ResourceModes.REPAIR_KIT:
                    totalCost = new List<ThingCount>();

                    int hpPerPack;
                    if (Settings.HpPercentage)
                    {
                        hpPerPack = (int) Math.Floor(itemDamaged.MaxHitPoints * (Settings.HpPerPack / 100.0f));
                        if (hpPerPack == 0)
                        {
                            hpPerPack = 100;
                            Log.Error($"RepairBench Error: Thing={itemDamaged}, MaxHitPoints={itemDamaged.MaxHitPoints}, Settings.HpPerPack={Settings.HpPerPack}, hpPercentage=true, hpPerPack=0%, did you put bad values in the config?");
                        }
                    }
                    else
                        hpPerPack = Settings.HpPerPack;

                    var kitsToFetch = (itemDamaged.MaxHitPoints - itemDamaged.HitPoints) / hpPerPack;
                    totalCost.Add(new ThingCount(ThingDef.Named(Settings.THINGDEF_REPKIT), kitsToFetch));
                    break;

                case ResourceModes.INGREDIENTS:

                    //tmpTotalCost is cached, DO NOT MODIFY IT
                    var tmpTotalCost = itemDamaged.CostListAdjusted();
                    totalCost = new List<ThingCount>(tmpTotalCost.Count);
                    totalCost.AddRange(tmpTotalCost.Select(thingCount => new ThingCount(thingCount.thingDef, thingCount.count)));

                    foreach (var thingCount in totalCost)
                    {
                        var origCount = thingCount.Count;
                        var damPercent = (itemDamaged.MaxHitPoints - itemDamaged.HitPoints) / (float) itemDamaged.MaxHitPoints;
                        var newCount = (int) Math.Floor(origCount * damPercent * Settings.INGRED_REPAIR_PERCENT);
                        thingCount.WithCount(newCount);
                        Log.Message($"origCount: {origCount} | damPer:{damPercent} | newCount:{newCount}");
                    }
                    break;

                default:
                    return new List<ThingCount>(0);
            }

            return totalCost.Where(tc => tc.Count != 0).ToList();
        }


        private static bool TryFindBestBillIngredientsInSet_NoMix(List<Thing> availableThings, List<ThingCount> neededIngreds, List<ThingAmount> chosen)
        {
            chosen.Clear();
            AssignedThings.Clear();
            AvailableCounts.Clear();
            AvailableCounts.GenerateFrom(availableThings);
            foreach (var ingredientCount in neededIngreds)
            {
                var flag = false;
                for (var index2 = 0; index2 < AvailableCounts.Count; ++index2)
                {
                    float f = ingredientCount.Count;

                    if (!(f <= (double) AvailableCounts.GetCount(index2)) || ingredientCount.ThingDef != AvailableCounts.GetDef(index2))
                        continue;

                    foreach (var item in availableThings)
                    {
                        if (item.def != AvailableCounts.GetDef(index2) || AssignedThings.Contains(item))
                            continue;

                        var countToAdd = Mathf.Min(Mathf.FloorToInt(f), item.stackCount);
                        ThingAmount.AddToList(chosen, item, countToAdd);
                        f -= countToAdd;
                        AssignedThings.Add(item);
                        if (f < 1.0/1000.0)
                        {
                            flag = true;
                            var val = AvailableCounts.GetCount(index2) - ingredientCount.Count;
                            AvailableCounts.SetCount(index2, val);
                            break;
                        }
                    }
                    if (flag)
                        break;
                }
                if (!flag)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Creates a new instance of the repair job.
        /// </summary>
        /// <param name="bill">the work bill for the job.</param>
        /// <param name="workbench">The building that gave the job.</param>
        /// <param name="itemDamaged">Damaged item to be repaired.</param>
        /// <param name="ingredients">Resources to consume for repair.</param>
        /// <returns></returns>
        private static Job StartNewRepairJob(Bill bill, IBillGiver workbench, Thing itemDamaged, IList<ThingAmount> ingredients)
        {
            // create the new job
            var job = new Job(DefDatabase<JobDef>.GetNamed(Settings.JOBDEF_REPAIR), (Thing) workbench)
            {
                haulMode = HaulMode.ToCellNonStorage,
                bill = bill,
                targetQueueB = new List<LocalTargetInfo>(ingredients.Count),
                countQueue = new List<int>(ingredients.Count)
            };

            // add item to be repaired
            job.targetQueueB.Add(itemDamaged);
            job.countQueue.Add(1);

            // add ingredients
            for (var index = 0; index < ingredients.Count; ++index)
            {
                job.targetQueueB.Add(ingredients[index].thing);
                job.countQueue.Add(ingredients[index].count);
            }

            return job;
        }

        /// <summary>
        /// Gets the cell location of the bill giver.
        /// </summary>
        /// <param name="billGiver">The thing that gave the work bill.</param>
        /// <param name="forPawn">The pawn that was assigned to the work bill.</param>
        /// <returns></returns>
        private static IntVec3 GetBillGiverRootCell(Thing billGiver, Pawn forPawn)
        {
            Building building = billGiver as Building;
            if (building == null)
                return billGiver.Position;

            if (building.def.hasInteractionCell)
                return building.InteractionCell;

            Log.Error("Tried to find bill ingredients for " + billGiver + " which has no interaction cell.");
            return forPawn.Position;
        }

        private class DefCountList
        {
            private readonly List<ThingDef> _defs;
            private readonly List<float> _counts;

            public int Count => _defs.Count;

            private float this[ThingDef def]
            {
                get
                {
                    var index = _defs.IndexOf(def);
                    if (index < 0)
                        return 0.0f;
                    return _counts[index];
                }
                set
                {
                    var index = _defs.IndexOf(def);
                    if (index < 0)
                    {
                        _defs.Add(def);
                        _counts.Add(value);
                        index = _defs.Count - 1;
                    }
                    else
                        _counts[index] = value;
                    CheckRemove(index);
                }
            }

            public DefCountList()
            {
                _defs = new List<ThingDef>();
                _counts = new List<float>();
            }

            public float GetCount(int index)
            {
                return _counts[index];
            }

            public void SetCount(int index, float val)
            {
                _counts[index] = val;
                CheckRemove(index);
            }

            public ThingDef GetDef(int index)
            {
                return _defs[index];
            }

            private void CheckRemove(int index)
            {
                if (Math.Abs(_counts[index]) > 0.001f)
                    return;
                _counts.RemoveAt(index);
                _defs.RemoveAt(index);
            }

            public void Clear()
            {
                _defs.Clear();
                _counts.Clear();
            }

            public void GenerateFrom(List<Thing> things)
            {
                Clear();
                foreach (var thing in things)
                    this[thing.def] += thing.stackCount;
            }
        }
    }
}