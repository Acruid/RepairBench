using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Repair
{
    internal class ItemRepairProgress
    {
        private class ConsumeProgress
        {
            private ThingDef _ItemDef;
            private int _ToConsume;
            private int _Consumed;
            private Func<ThingDef, int, bool> _RemoveHandler;

            public ConsumeProgress(ThingDef itemDef, int toConsume, Func<ThingDef, int, bool> removeHandler) {
                this._ItemDef = itemDef;
                this._ToConsume = toConsume;
                this._RemoveHandler = removeHandler;
            }

            public bool UpdateProgress(float progress) {
                var targetConsumed = (int)Math.Floor(_ToConsume * progress) - _Consumed;
                if (targetConsumed > 0) {
                    _Consumed += targetConsumed;
                    return _RemoveHandler(_ItemDef, targetConsumed);
                }
                return true;
            }
        }
        private List<ConsumeProgress> _UsageTable;
        private List<IntVec3> _IngridientSpaces;
        private int _ToRepair;
        private float _RepairedAmount;
        private List<Thing> _Ingridients;
        private Pawn _Pawn;

        public ItemRepairProgress(Pawn pawn, IEnumerable<IntVec3> ingridientStacks, List<ThingDefCount> toConsume, int repairAmount) {
            _Pawn = pawn;
            _ToRepair = repairAmount;
            _RepairedAmount = 0;
            _IngridientSpaces = ingridientStacks.ToList();
            _UsageTable = toConsume.Select(_ => new ConsumeProgress(_.ThingDef, _.Count, ConsumeIngredient)).ToList();
        }

        public bool AddRepairedAmount(int amount) {
            _RepairedAmount += amount;
            var progress = _RepairedAmount / _ToRepair;
            var result = true;
            _Ingridients = _IngridientSpaces.SelectMany(spot => _Pawn.Map.thingGrid.ThingsListAt(spot)).ToList();
            foreach (var usage in _UsageTable) {
                result &= usage.UpdateProgress(progress);
            }
            return result;
        }

        private bool ConsumeIngredient(ThingDef item, int amount) {
            //This should check if ingredient is available. Remove wahever possible but not more than required. Return true if enough consiumed and false othervise.
            var ingridientsOfType = _Ingridients.Where(_ => _.def == item).ToArray();
            foreach (var ingredientStack in ingridientsOfType) {
                if (ingredientStack.stackCount <= amount) {
                    amount -= ingredientStack.stackCount;
                    ingredientStack.DeSpawn();
                    ingredientStack.Destroy();
                    _Ingridients.Remove(ingredientStack);
                    if (amount == 0)
                        return true;
                } else {
                    var kit = ingredientStack.SplitOff(amount);
                    kit.Destroy();
                    return true;
                }
            }
            return false;
        }
    }
}
