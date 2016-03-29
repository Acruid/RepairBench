using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Mending
{
    // ReSharper disable once InconsistentNaming
    internal class JobDriver_MendItem : JobDriver
    {
        private const float REPAIR_RATE = 12f; // game ticks per reptick
        private const float SKILL_GAIN = 0.55f; // Skill gain per reptick
        private const int HP_GAIN = 1; // durability regen per reptick

        private const TargetIndex TI_REPBENCH = TargetIndex.A;
        private const TargetIndex TI_ITEM = TargetIndex.B;
        private const TargetIndex TI_CELL = TargetIndex.C;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrForbidden(TI_REPBENCH);
            this.FailOnBurningImmobile(TI_REPBENCH);

            this.FailOnDestroyedOrForbidden(TI_ITEM);
            this.FailOnBurningImmobile(TI_ITEM);

            yield return Toils_Reserve.Reserve(TI_REPBENCH);

            yield return Toils_Reserve.Reserve(TI_ITEM);

            yield return Toils_Goto.GotoCell(TI_ITEM, PathEndMode.OnCell);

            yield return Toils_Haul.StartCarryThing(TI_ITEM);

            yield return Toils_Goto.GotoThing(TI_REPBENCH, PathEndMode.InteractionCell);

            yield return Toils_Haul.PlaceHauledThingInCell(TI_REPBENCH, null, false);

            var item = pawn.CurJob.GetTarget(TI_ITEM).Thing;
            var ticksToNextRepair = REPAIR_RATE;
            var repairToil = new Toil
            {
                tickAction = () =>
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, SKILL_GAIN);
                    ticksToNextRepair -= pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);

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

            /*

                        var toilColor = new Toil();
                        TargetInfo target3 = target1;
                        toilColor.initAction = () =>
                        {
                            //Color the Apparel
                            if (target3.Thing is Apparel)
                            {
                                var clothing = target3.Thing as Apparel;
            //                if(clothing.Stuff.defName == "Cloth")
                                var comp = clothing.GetComp<CompColorable>();

                                if (comp == null || !comp.Active)
                                    return;

                                // set it to default color
                                comp.Color = comp.parent.def.defaultColor;

                                // turn off the color comp
                                FieldInfo fieldInfo = typeof (CompColorable).GetField("active", BindingFlags.Instance | BindingFlags.NonPublic);
                                if (fieldInfo != null)
                                    fieldInfo.SetValue(comp, false);
                            }
                        };
                        toilColor.defaultCompleteMode = (ToilCompleteMode) 1;
                        yield return toilColor;
            */

            yield return Toils_Haul.StartCarryThing(TI_ITEM);

            if (pawn.CurJob.GetTarget(TI_CELL).Cell.IsValidStorageFor(item))
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
                        pawn.carrier.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out resultingThing);
                    },
                    defaultCompleteMode = ToilCompleteMode.Instant
                };
                yield return dropObjToil;
            }

            yield return Toils_Reserve.Release(TI_REPBENCH);
        }
    }
}