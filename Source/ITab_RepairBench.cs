using UnityEngine;
using Verse;

namespace Repair
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    internal class ITab_RepairBench : ITab
    {
        private Vector2 _scrollPosition;

        public ITab_RepairBench()
        {
            size = new Vector2(300f, 480f);
            labelKey = "Repair.tableTab";
        }

        protected override void FillTab()
        {
            var repTable = SelThing as Building_RepairTable;
            if (repTable == null)
            {
                Log.Error("Repair mod: ITab - SelThing is not a RepairTable!");
                return;
            }

            // A lot of this comes from Dialog_BillConfig.cs

            var position = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            var listingStandard = new Listing_Standard(position);

            listingStandard.Gap(); // move the checkbox under the IFrame close "X"

            listingStandard.CheckboxLabeled("Repair.tabSuspend".Translate(), ref repTable.Suspended);
            listingStandard.CheckboxLabeled("Repair.tabStockpile".Translate(), ref repTable.HaulStockpile);
            listingStandard.CheckboxLabeled("Repair.tabOutside".Translate(), ref repTable.OutsideItems);

            listingStandard.Label($"{"Repair.tabSearch".Translate()} {(int) repTable.SearchRadius}");
            repTable.SearchRadius = listingStandard.Slider(repTable.SearchRadius, 3f, 100f);
            if (repTable.SearchRadius >= 100.0)
                repTable.SearchRadius = 999f;
            
            listingStandard.Gap(4f);// some spacing between slider and ThingFilterUI

            listingStandard.End();
            
            var topOffset = listingStandard.CurHeight;
            var thingPosition = new Rect(position.x, topOffset, position.width, position.height - topOffset);
            ThingFilterUI.DoThingFilterConfigWindow(thingPosition,
                ref _scrollPosition, repTable.GetStoreSettings().filter, repTable.GetParentStoreSettings().filter, 8);
        }

        public override void TabUpdate()
        {
            var repTable = SelThing as Building_RepairTable;
            if (repTable == null)
            {
                Log.Error("Repair mod: ITab - SelThing is not a RepairTable!");
                return;
            }

            if (repTable.SearchRadius <= GenRadial.MaxRadialPatternRadius)
                GenDraw.DrawRadiusRing(repTable.Position, repTable.SearchRadius);
        }
    }
}