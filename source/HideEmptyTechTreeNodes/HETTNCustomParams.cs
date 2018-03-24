//using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
//using KSP.UI.Screens;
//using System.Linq;
using System.Reflection;
//using System.Reflection.Emit;
//using System.Text;
//using System.Threading.Tasks;
//using UnityEngine;

namespace HideEmptyTechTreeNodes
{
    abstract class HETTNCustomParams : GameParameters.CustomParameterNode
    {
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.CAREER | GameParameters.GameMode.SCIENCE; } }
        public override string Section { get { return "Hide Empty Tech Tree Nodes"; } }
        public override string DisplaySection { get { return "#autoLOC_HETTN_01001"; } } // "Hide Empty Tech Tree Nodes"
        public override bool HasPresets { get { return false; } }
    }

    class HETTNCustomParams_Nodes : HETTNCustomParams
    {
        public override string Title { get { return Localizer.Format("#autoLOC_HETTN_01101"); } } // "Node Options"
        public override int SectionOrder { get { return 1; } }

        // Basic Options.
        // "Research Requirements", toolTip = "Sets which parent research is required to unlock a node."
        [GameParameters.CustomFloatParameterUI("#autoLOC_HETTN_01102", toolTip = "#autoLOC_HETTN_01103")]
        public string researchRequirements = Localizer.Format("#autoLOC_HETTN_01401"); // "Default";
        // "Hide Unresearchable Nodes", toolTip = "Enable to hide nodes until they become researchable."
        [GameParameters.CustomParameterUI("#autoLOC_HETTN_01104", toolTip = "#autoLOC_HETTN_01105")]
        public bool forceHideUnresearchable = false;
        // "Hide Empty Nodes", toolTip = "Enable to hide nodes with no parts or part upgrades in them.\nTech lines will connect to non-empty nodes as well.\n\nDisable to see all nodes."
        [GameParameters.CustomParameterUI("#autoLOC_HETTN_01106", toolTip = "#autoLOC_HETTN_01107")]
        public bool forceHideEmpty = true;
        // Remove Empty Vertical Space, toolTip = "Shifts nodes up/down to remove the empty space created by rows of hidden tech tree nodes."
        [GameParameters.CustomParameterUI("#autoLOC_HETTN_01108", toolTip = "#autoLOC_HETTN_01109")]
        public bool shiftVertically = false;
        // Remove Empty Horizontal Space, toolTip = "Shifts left/right horizontally to remove the empty space created by columns of hidden tech tree nodes."
        [GameParameters.CustomParameterUI("#autoLOC_HETTN_01110", toolTip = "#autoLOC_HETTN_01111")]
        public bool shiftHorizontally = false;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            // These Field must always be Interactible.
            if (member.Name == "researchRequirements" || member.Name == "forceHideUnresearchable" || member.Name == "forceHideEmpty")
                return true;
            // Otherwise it depends on the value of below boolean. If it's false then disable and return false.
            if (forceHideEmpty == false)
            {
                shiftVertically = false;
                shiftHorizontally = false;
                return false;
            }
            // Otherwise return true.
            return true;
        }

        public override IList ValidValues(MemberInfo member)
        {
            if (member.Name == "researchRequirements")
            {
                List<string> researchRequirementsList = new List<string>(new string[]
                {
                    Localizer.Format("#autoLOC_HETTN_01401"), // "Default",
                    Localizer.Format("#autoLOC_HETTN_01402"), // "Any",
                    Localizer.Format("#autoLOC_HETTN_01403"), // "All"
                });
                return researchRequirementsList;
            }
            else
            {
                return null;
            }
        }
    }

    class HETTNCustomParams_Zoom : HETTNCustomParams
    {
        public override string Title { get { return Localizer.Format("#autoLOC_HETTN_01201"); } } // "Zoom Options"
        public override int SectionOrder { get { return 2; } }

        public float zoomMax = 1.0f;
        public float zoomMin = 0.35f;
        public float zoomSpeed = 0.05f;

        // "Maximum Zoom (%)", toolTip = "Sets the maximum scroll zoom when viewing the tech tree."
        [GameParameters.CustomFloatParameterUI("#autoLOC_HETTN_01202", displayFormat = "N0", asPercentage = false, minValue = 60f, maxValue = 150f,
            toolTip = "#autoLOC_HETTN_01203")]
        public float zoomMaxParam
        {
            get { return zoomMax * 100; }
            set { zoomMax = value / 100.0f; }
        }
        // "Minimum Zoom (%)", toolTip = "Sets the minimum scroll zoom when viewing the tech tree."
        [GameParameters.CustomFloatParameterUI("#autoLOC_HETTN_01204", displayFormat = "N0", asPercentage = false, minValue = 5f, maxValue = 60f,
            toolTip = "#autoLOC_HETTN_01205")]
        public float zoomMinParam
        {
            get { return zoomMin * 100; }
            set { zoomMin = value / 100.0f; }
        }
        // "Zoom Speed (%)", toolTip = "Sets the zoom scroll speed when viewing the tech tree."
        [GameParameters.CustomFloatParameterUI("#autoLOC_HETTN_01206", displayFormat = "N0", asPercentage = false, minValue = 1f, maxValue = 25f,
           toolTip = "#autoLOC_HETTN_01207")]
        public float zoomSpeedParam
        {
            get { return zoomSpeed * 100; }
            set { zoomSpeed = value / 100.0f; }
        }

        //[GameParameters.CustomFloatParameterUI("Maximum Zoom", asPercentage = true, minValue = 0.6f, maxValue = 1.5f,
        //    toolTip = "Sets the maximum scroll zoom when viewing the tech tree.")]
        //public float zoomMax = 1.0f;
        //[GameParameters.CustomFloatParameterUI("Minimum Zoom", asPercentage = true, displayFormat = "N1", minValue = 0.05f, maxValue = 0.6f,
        //    toolTip = "Sets the minimum scroll zoom when viewing the tech tree.")]
        //public float zoomMin = 0.35f;
        //[GameParameters.CustomFloatParameterUI("Zoom Speed", displayFormat = "N2", asPercentage = true, minValue = 0.01f, maxValue = 0.25f,
        //    toolTip = "Sets the zoom scroll speed when viewing the tech tree.")]
        //public float zoomSpeed = 0.05f;
    }

    class HETTNCustomParams_Misc : HETTNCustomParams
    {
        public override string Title { get { return Localizer.Format("#autoLOC_HETTN_01301"); } } // "Misc. Options"
        public override int SectionOrder { get { return 3; } }

        // "Extra Debug Logging", toolTip = "Enable to output extra log info for reporting a problem.\n\nKeep off otherwise to minimize game stutter."
        [GameParameters.CustomParameterUI("#autoLOC_HETTN_01302", toolTip = "#autoLOC_HETTN_01303")]
        public bool debugging = false;
    }
}
