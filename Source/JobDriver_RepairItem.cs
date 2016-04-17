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
        private const float REPAIR_RATE = 125f; // game ticks per reptick, (5 in-game hours to repair 100hp)
        private const float SKILL_GAIN = 0.55f; // Skill gain per reptick
        private const int HP_GAIN = 1; // durability regen per reptick

        private const TargetIndex TI_REPBENCH = TargetIndex.A;
        private const TargetIndex TI_ITEM = TargetIndex.B;
        private const TargetIndex TI_CELL = TargetIndex.C;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TI_REPBENCH);
            this.FailOnBurningImmobile(TI_REPBENCH);

            this.FailOnDestroyedNullOrForbidden(TI_ITEM);
            this.FailOnBurningImmobile(TI_ITEM);

            yield return Toils_Reserve.Reserve(TI_REPBENCH);

            yield return Toils_Reserve.Reserve(TI_ITEM);

            yield return Toils_Goto.GotoCell(TI_ITEM, PathEndMode.OnCell);

            yield return Toils_Haul.StartCarryThing(TI_ITEM);

            yield return Toils_Goto.GotoThing(TI_REPBENCH, PathEndMode.InteractionCell);

            yield return Toils_Haul.PlaceHauledThingInCell(TI_REPBENCH, null, false);

            var item = pawn.CurJob.GetTarget(TI_ITEM).Thing;
            var table = pawn.CurJob.GetTarget(TI_REPBENCH).Thing as Building_RepairTable;
            var ticksToNextRepair = REPAIR_RATE;
            var repairToil = new Toil
            {
                tickAction = () =>
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, SKILL_GAIN);
                    ticksToNextRepair -= pawn.GetStatValue(StatDefOf.WorkSpeedGlobal) * table.WorkSpeedFactor;

                    if (ticksToNextRepair > 0.0)
                        return;

                    ticksToNextRepair = REPAIR_RATE;
                    item.HitPoints += HP_GAIN;

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

            var info = pawn.CurJob.GetTarget(TI_CELL);
            if (info != null && info.IsValid && info.Cell.IsValidStorageFor(item))
            {
                yield return Toils_Reserve.Reserve(TI_CELL);

                yield return Toils_Haul.CarryHauledThingToCell(TI_CELL);

                yield return Toils_Haul.PlaceHauledThingInCell(TI_CELL, null, true);

                yield return Toils_Reserve.Release(TI_CELL);
            }
            else
            {
                var dropObjToil = new Toil
                {
                    initAction = () =>
                    {
                        Thing resultingThing;
                        pawn.carrier.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out resultingThing);
                    },
                    defaultCompleteMode = ToilCompleteMode.Instant
                };
                yield return dropObjToil;
            }

            yield return Toils_Reserve.Release(TI_REPBENCH);
        }
    }
}