using System;
using UnityEngine;
using Verse;

namespace Mending
{
    // ReSharper disable once InconsistentNaming
    internal class ITab_Mending_Building : ITab
    {
        private Vector2 _scrollPosition;

        public ITab_Mending_Building()
        {
            size = new Vector2(300f, 480f);
            labelKey = "mending.buildingTab";
        }

        protected override void FillTab()
        {
            var menderBuildingComp = SelThing.TryGetComp<MenderBuildingComp>();

            if (menderBuildingComp == null)
            {
                Log.Error("Mending mod: could not find MenderBuildingComp, error");
            }
            else
            {
                GUI.Label(new Rect(10f, 20f, 150f, 20f), "Search radius: " + (int)menderBuildingComp.SearchRadius);

                menderBuildingComp.SearchRadius = GUI.HorizontalSlider(new Rect(10f, 50f, 150f, 20f),
                    menderBuildingComp.SearchRadius, 1f, 100f);

                if (Math.Abs(menderBuildingComp.SearchRadius - 100.0) < 0.01)
                    menderBuildingComp.SearchRadius = 9999f;

                if (Widgets.TextButton(new Rect(190f, 30f, 100f, 50f), "Clear all"))
                    menderBuildingComp.ClearAll();

                Widgets.Checkbox(new Vector2(10f, 70f), ref menderBuildingComp.OutsideItems, 24f);

                GUI.Label(new Rect(35f, 70f, 100f, 30f), "Outside items");

                ThingFilterUI.DoThingFilterConfigWindow(new Rect(10.0f, 110.0f, size.x - 20.0f, (size).y - 120.0f),
                    ref _scrollPosition, menderBuildingComp.GetAllowances(), menderBuildingComp.GetPossibleAllowances());
                GUI.EndGroup();
            }
        }
    }
}