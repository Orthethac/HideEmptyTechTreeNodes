//using KSP.UI.Screens;
using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace HideEmptyTechTreeNodes
{
    internal class HETTNSettings
    {
        // ----------------
        // Option Settings.
        // ----------------
        #region OPTION SETTINGS
        internal HETTNCustomParams_Nodes HETTNSettingsParams1 = new HETTNCustomParams_Nodes();
        internal HETTNCustomParams_Zoom HETTNSettingsParams2 = new HETTNCustomParams_Zoom();
        internal HETTNCustomParams_Misc HETTNSettingsParams3 = new HETTNCustomParams_Misc();

        // Default values (same as default values from HETTNCustomParams).
        internal string researchRequirements = "Default";
        internal bool forceHideUnresearchable = false;
        internal bool forceHideEmpty = true;
        internal bool forceHideManual = false;
        internal string propagateScience = "Default (Do not transfer science)";
        internal bool shiftVertically = false;
        internal bool shiftHorizontally = false;

        internal float zoomMax = 1.0f;
        internal float zoomMin = 0.60f;
        internal float zoomSpeed = 0.05f;

        internal static bool debugging = false;

        // Get settings.
        internal HETTNSettings()
        {
            ApplySettings();
        }

        // Settings.
        internal void ApplySettings()
        {
            Log2("Applying settings...");
            HETTNSettingsParams1 = HighLogic.CurrentGame.Parameters.CustomParams<HETTNCustomParams_Nodes>();
            HETTNSettingsParams2 = HighLogic.CurrentGame.Parameters.CustomParams<HETTNCustomParams_Zoom>();
            HETTNSettingsParams3 = HighLogic.CurrentGame.Parameters.CustomParams<HETTNCustomParams_Misc>();

            if (HETTNSettingsParams1 != null)
            {
                researchRequirements = HETTNSettingsParams1.researchRequirements;
                forceHideUnresearchable = HETTNSettingsParams1.forceHideUnresearchable;
                forceHideEmpty = HETTNSettingsParams1.forceHideEmpty;
                forceHideManual = HETTNSettingsParams1.forceHideManual;
                propagateScience = HETTNSettingsParams1.propagateScience;
                shiftVertically = HETTNSettingsParams1.shiftVertically;
                shiftHorizontally = HETTNSettingsParams1.shiftHorizontally;
            }
            else
            {
                LogWarning("Could not find Node options. Using default settings.");
            }
            if (HETTNSettingsParams2 != null)
            {
                zoomMax = HETTNSettingsParams2.zoomMax;// (float)Math.Round((decimal)HETTNSettingsParams2.zoomMax * 100) / 100;
                zoomMin = HETTNSettingsParams2.zoomMin;// (float)Math.Round((decimal)HETTNSettingsParams2.zoomMin * 100) / 100;
                zoomSpeed = HETTNSettingsParams2.zoomSpeed;// (float)Math.Round((decimal)HETTNSettingsParams2.zoomSpeed * 100) / 100;
            }
            else
            {
                LogWarning("Could not find Zoom options. Using default settings.");
            }
            if (HETTNSettingsParams3 != null)
            {
                debugging = HETTNSettingsParams3.debugging;
            }
            else
            {
                LogWarning("Could not find Misc. options. Using default settings.");
            }
            Log2("Finished applying settings.");
        }
        #endregion

        // -------------
        // Log Messages.
        // -------------
        #region LOG MESSAGES
        // Output these logs always.
        [System.Diagnostics.Conditional("DEBUG")]
        internal static void Log(string s, params object[] o)
        {
            string toLog = "";
            s = string.Format(s, o);
            if (debugging)
                toLog = string.Format("[HETTN Debug] {0}", s);
            else
                toLog = string.Format("[HETTN] {0}", s);
            Debug.Log(toLog);
        }

        // Output these logs if debug setting is true.
        internal static void Log2(string s, params object[] o)
        {
            if (debugging)
                Log(s, o);
        }

        // Output warnings always.
        [System.Diagnostics.Conditional("DEBUG")]
        internal static void LogWarning(string s, params object[] o)
        {
            string toLog = "";
            s = string.Format(s, o);
            if (debugging)
                toLog = string.Format("[HETTN Debug] {0}", s);
            else
                toLog = string.Format("[HETTN] {0}", s);
            Debug.LogWarning(toLog);
        }

        // Output errors always.
        [System.Diagnostics.Conditional("DEBUG")]
        internal static void LogError(string s, params object[] o)
        {
            string toLog = "";
            s = string.Format(s, o);
            if (debugging)
                toLog = string.Format("[HETTN Debug] {0}", s);
            else
                toLog = string.Format("[HETTN] {0}", s);
            Debug.LogError(toLog);
        }
        #endregion
    }
}
