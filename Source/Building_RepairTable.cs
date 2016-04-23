using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Repair
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Building_RepairTable : Building, IStoreSettingsParent, IBillGiver
    {
        private StorageSettings _allowedStorage;
        private CompAffectedByFacilities _facilityComp;
        private CompPowerTrader _powerComp;
        public bool HaulStockpile = true;
        public bool OutsideItems = true;
        public float SearchRadius = 999.0f;
        public bool Suspended;

        public Building_RepairTable()
        {
            BillStack = null; // we don't use bills
        }

        private bool CanWorkWithoutPower => _powerComp == null || def.building.unpoweredWorkTableWorkSpeedFactor > 0.0;
        public bool UsableNow => CanWorkWithoutPower || _powerComp != null && _powerComp.PowerOn;

        public float WorkSpeedFactor
        {
            get
            {
                if (!UsableNow)
                    return 0;

                var powerFactor = _powerComp.PowerOn ? 1.0f : def.building.unpoweredWorkTableWorkSpeedFactor;

                if (_facilityComp == null)
                    return powerFactor;

                return _facilityComp.GetStatOffset(StatDefOf.WorkTableWorkSpeedFactor) + powerFactor;
            }
        }

        public bool CurrentlyUsable()
        {
            return UsableNow;
        }

        public BillStack BillStack { get; }
        public IEnumerable<IntVec3> IngredientStackCells => GenAdj.CellsOccupiedBy(this);

        public bool StorageTabVisible => true;

        public StorageSettings GetStoreSettings()
        {
            return _allowedStorage;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public override void PostMake()
        {
            base.PostMake();

            _allowedStorage = new StorageSettings(this);
            if (def.building.defaultStorageSettings == null)
                return;

            _allowedStorage.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.LookValue(ref SearchRadius, "SearchRadius", 999.0f);
            Scribe_Values.LookValue(ref OutsideItems, "OutsideItems", true);
            Scribe_Values.LookValue(ref HaulStockpile, "HaulStockpile", true);
            Scribe_Values.LookValue(ref Suspended, "Suspended", false);

            Scribe_Deep.LookDeep(ref _allowedStorage, "AllowedStorage");
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            _powerComp = GetComp<CompPowerTrader>();
            _facilityComp = GetComp<CompAffectedByFacilities>();
        }
    }
}