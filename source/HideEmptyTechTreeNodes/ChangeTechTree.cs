// MOD DESCRIPTON
// Goes through a bunch of conditions to change the parents from hidden tech nodes to ones that aren't.
// Lots of debugging code, haha.
//
// v1.3.0 - 2020/12/21 - Recompiled for KSPv1.11.0; Fixed Propagate Science option getting stuck in infite loop
// v1.2.0 - 2020/12/04 - Recompiled for KSPv1.10.1; Fixed depricated parts counting towards total parts in nodes, causing empty nodes; Reverted min zooom to stock 60% value.
// v1.1.2 - 2019/11/07 - Recompiled for KSPv1.8.1; Changed Target Framework to .NET 4.5; Increased maximum allowable zoom to 200%; Added Russian localization (thx @Sooll3)
// v1.1.1 - 2019/04/20 - Recompiled for KSPv1.7.0
// v1.1.0 - 2018/01/12 - Recompiled for KSPv1.6.1; Added manual-hide option; Added science transfer option
// v1.0.5 - 2018/10/27 - Recompiled for KSPv1.5.1
// v1.0.4 - 2018/05/06 - Added .version file (no changes to any code)
// v1.0.3 - 2018/03/25 - Path file separator fix (thx @nightingale)
// v1.0.2 - 2018/03/24 - Recompiled for KSPv1.4.1
// v1.0.1 - 2017/10/07 - Recompiled for KSPv1.3.1
// v1.0.0 - 2017/09/03 - Added option to change research requirements to "Default", "Any", or "All"; Added option to remove empty space created from rows/columns of empty nodes
// v0.8.0 - 2017/06/02 - Updated to KSPv1.3.0 (fixed new bug in settings)
// v0.7.4 - 2017/01/01 - Fixed infinite loop bug that happens when two nodes are on top of each other
// v0.7.3 - 2017/01/01 - Fixed issue with zoom options due to bug in 1.2.2 when inputs are a percent; Added better error output when there are duplicate nodes. Displays message on screen
// v0.7.2 - 2016/12/15 - Updated to KSPv1.2.2
// v0.7.1 - 2016/12/15 - Support for the ETT. Changes TechRequired field for Parts from the Updates nodes
// v0.7.0 - 2016/21/11 - Changed main mod behavior from "visual" to "active". Plugin now creates its own tech tree file with hidden nodes derived from your modded file, and assigns the new file to the game; Added option to hide unresearchable tech; Added maximum zoom setting;
// v0.6.0 - 2016/11/07 - Converted some settings to in-game settings menu; Removed parents that are the only ancestors of any other parents; Fixed bug where PARTUPGRADE only nodes would still show if Part Upgrades are disabled, causing empty nodes
// v0.5.1 - 2016/10/23 - Fixed debug error when reloading parents; Changed tech tree file path to loaded file (not specific MM file); Force no hide for PARTUPGRADE techRequired nodes; Better support for the ETT
// v0.5.0 - 2016/10/20 - Updated to KSPv1.2; Code cleanup; Added startNodeID option
// v0.4.0 - 2016/04/24 - Updated to KSPv1.1; Renamed to Hide Empty Tech Tree Nodes (HETTN); Added KSP.UI.Screens namespace, because many KSP classes were moved there; Added config to turn debugging on or off
// v0.3.0 - 2015/12/11 - Updated to KSPv1.05; Added option to change zoomMin and zoomSpeed, set to different defaults
// v0.2.0 - 2015/09/21 - Fixed intersecting lines above "start" node
// v0.1.0 - 2015/07/26 - Initial release
//
// Might Do List:
// 1) Option to keep original line paths.
// 2) Split up code into different .cs files once I learn how to best do that.
// 3) Finish de-LINQing?

using KSP.Localization;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace HideEmptyTechTreeNodes
{
    // Starts up every time to match and override MM startup settings.
    [KSPAddon(KSPAddon.Startup.FlightAndKSC, false)]
    public class ChangeTechTree : MonoBehaviour
    {
        // -----------------------------
        // Setup parameters and methods.
        // -----------------------------
        #region SETUP
        // Hide Empty Tech Tree Nodes tech tree file path.
        private static string hettnTechTreeName = "HETTN.TechTree";
        private static string hettnTechTreeUrl = Path.Combine(Path.Combine(Path.Combine("GameData", "HideEmptyTechTreeNodes"), "Resources"), hettnTechTreeName);
        internal static bool IsHettnUrlCreated = false;

        // Default and backup tech tree paths. Default is created once, on scene startup. (Add squad backup?)
        private static string defaultTechTreeUrl = string.Empty;
        private static string backupTechTreeUrl = Path.Combine("GameData", "ModuleManager.TechTree");

        // Settings.
        internal HETTNSettings hettnSettings;

        // Start node
        internal static string startNodeID = "start";
        internal static HENode startNode = null;

        // Used to find bad parents. Tolerances are about half the width of a tech node.
        internal static double posXTolerance = 38 / 2;
        internal static double posYTolerance = 38 / 2;
        internal static double nodeSizeEpsilon = 1.0;
        internal static int maxParents = 3;
        internal static double dividePosGroupCount = 6;

        #endregion


        // ---------------------------------
        // Load the plugin setup and events.
        // ---------------------------------
        #region LOADING
        // Unity method.
        private void Start()
        {
            GameEvents.OnGameSettingsApplied.Add(new EventVoid.OnEvent(this.OnGameSettingsApplied));
            RDTechTree.OnTechTreeSpawn.Add(new EventData<RDTechTree>.OnEvent(this.onTechTreeSpawn));
            RDTechTree.OnTechTreeDespawn.Add(new EventData<RDTechTree>.OnEvent(this.onTechTreeDespawn));
            Setup();
        }

        private void OnDestroy()
        {
            HETTNSettings.Log2("Resetting tech tree changes...");
            GameEvents.OnGameSettingsApplied.Remove(new EventVoid.OnEvent(this.OnGameSettingsApplied));
            RDTechTree.OnTechTreeSpawn.Remove(new EventData<RDTechTree>.OnEvent(this.onTechTreeSpawn));
            RDTechTree.OnTechTreeDespawn.Remove(new EventData<RDTechTree>.OnEvent(this.onTechTreeDespawn));
            if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                IsHettnUrlCreated = false;
                defaultTechTreeUrl = string.Empty;
                HETTNSettings.Log("HETTN.TechTree will reset next game.");
            }
            HETTNSettings.Log2("Finished resetting tech tree changes.");
        }

        // Setup one time. Apply settings and set up the tech tree changes. Otherwise use
        // the new HETTN.TechTree during other plugin load times. IsHettnUrlCreated is changed in ChangeParets().
        public void Setup()
        {
            hettnSettings = new HETTNSettings();
            if (!IsHettnUrlCreated)
            {
                HETTNSettings.Log2("Loading...");
                SetDefaultTechTreeUrl();
                ChangeParents();
                RepopulateTechTreeNode();
                HETTNSettings.Log2("Finished assigning new tech tree.");
            }
            else
            {
                if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl != hettnTechTreeUrl)
                {
                    HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = hettnTechTreeUrl;
                    if (String.IsNullOrEmpty(HighLogic.CurrentGame.Parameters.Career.TechTreeUrl))
                    {
                        HETTNSettings.LogError("Could not find tech tree. Using default path.");
                        HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = defaultTechTreeUrl;
                    }
                    else
                    {
                        RepopulateTechTreeNode();
                    }
                }
                HETTNSettings.Log2("Default tech tree path: {0}.", defaultTechTreeUrl);
                HETTNSettings.Log2("Current tech tree path: {0}.", HighLogic.CurrentGame.Parameters.Career.TechTreeUrl);
            }
        }


        // ETT fix 1 of 3.
        // Check if current tech tree path is set to EngTechTree. Recreate tech tree from this path, reload and respawn new tree.
        private void onTechTreeSpawn(RDTechTree rdTechTree)
        {
            if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl != hettnTechTreeUrl)
            {
                HETTNSettings.Log("ETT fix: Tech Tree url was changed. Respawning using HETTN.TechTree...");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = hettnTechTreeUrl;
                RepopulateTechTreeNode();
                rdTechTree.ReLoad();
                rdTechTree.SpawnTechTreeNodes();
            }
        }

        // ETT fix 2 of 3.
        // Clear RDTechTree on despawn. Not sure if neccessary... do I need to delete tech line and arrow gameObjects as well??
        private void onTechTreeDespawn(RDTechTree rdTechTree)
        {
            int count = rdTechTree.controller.nodes.Count;
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object.Destroy(rdTechTree.controller.nodes[count].gameObject);
            }
            rdTechTree.controller.nodes.Clear();
            rdTechTree = null;
        }
        #endregion


        // -----------------------------------------------------------------------
        // If certain Hide Empty Node settings are changed, reset current settings
        // and create new HETTN tech tree to apply the new settings.
        // -----------------------------------------------------------------------
        #region NEW SETTINGS APPLIED
        private void OnGameSettingsApplied()
        {
            var NewNodeSettings = HighLogic.CurrentGame.Parameters.CustomParams<HETTNCustomParams_Nodes>();
            if (hettnSettings.HETTNSettingsParams1.Equals(NewNodeSettings))
            {
                hettnSettings = new HETTNSettings();
                HETTNSettings.Log("New Node Settings Applied\n  Research Requirements: {0}\n  Hide Unresearchable Nodes: {1}\n  Hide Empty Nodes: {2} ({3}, {4})",
                    NewNodeSettings.researchRequirements,
                    NewNodeSettings.forceHideUnresearchable ? String.Format("enabled") : String.Format("disabled"),
                    NewNodeSettings.forceHideEmpty ? String.Format("enabled") : String.Format("disabled"),
                    NewNodeSettings.shiftVertically ? String.Format("enabled") : String.Format("disabled"),
                    NewNodeSettings.shiftHorizontally ? String.Format("enabled") : String.Format("disabled"));
                ChangeParents();
                RepopulateTechTreeNode();
            }
        }
        #endregion


        // --------------------------------------------------------------------------------
        // Set default tech tree url path method. Create default path if one doesn't exist.
        // Make sure that default path is NOT the HETTN tree. Note: backup is currently
        // only MM tree, doesn't account for ETT tree.
        // --------------------------------------------------------------------------------
        #region SET DEFAULT TECH TREE
        public static void SetDefaultTechTreeUrl()
        {
            HETTNSettings hettnSettings = new HETTNSettings();
            HETTNSettings.Log2("Finding default tech tree path...");
            defaultTechTreeUrl = HighLogic.CurrentGame.Parameters.Career.TechTreeUrl;
            if (defaultTechTreeUrl.Contains(hettnTechTreeName))
            {
                HETTNSettings.LogWarning("Default path mistakenly goes to \"HETTN.TechTree\". Reseting path as backup MM tree.");
                defaultTechTreeUrl = backupTechTreeUrl;
            }
            HETTNSettings.Log2("Default tech tree path: {0}.", defaultTechTreeUrl);
            HETTNSettings.Log2("Current tech tree path: {0}.", HighLogic.CurrentGame.Parameters.Career.TechTreeUrl);
        }
        #endregion


        // ---------------------------------------------------
        // Method to repopulate TechTree node with HETTN file.
        // Note: Required from KSP v1.11.0
        // ---------------------------------------------------
        #region REPOPULATE TECH TREE NODE
        public static void RepopulateTechTreeNode()
        {
            HETTNSettings.Log("Repopulating instanced TechTree Node...");

            // Load RDNodes from HETTN tech tree path.
            ConfigNode configFile = ConfigNode.Load(HighLogic.CurrentGame.Parameters.Career.TechTreeUrl);
            ConfigNode configTechTree = configFile.GetNode("TechTree");
            ConfigNode[] configRDNodes = configTechTree.GetNodes("RDNode");

            // Get instanced TechTree nodes.
            ConfigNode[] instanceTechTreeNodes = GameDatabase.Instance.GetConfigNodes("TechTree");

            // Clear all instanced TechTree nodes.
            for (int i = 0; i < instanceTechTreeNodes.Length; i++)
            {
                instanceTechTreeNodes[i].ClearNodes();
            }

            // Repopulate the first TechTree node with HETTN's RDNodes.
            for (int i = 0; i < configRDNodes.Length; i++)
            {
                instanceTechTreeNodes[0].AddNode(configRDNodes[i]);
            }
        }
        #endregion


        // -------------------------------------------------------------------
        // Goes through several logic conditions based on empty nodes and node 
        // positions to change node parents. Main plugin work is done here.
        // -------------------------------------------------------------------
        #region NODE PARENT CHANGES
        public void ChangeParents()
        {
            // ------------------------------------
            // Check default tech tree path exists.
            // ------------------------------------
            if (String.IsNullOrEmpty(defaultTechTreeUrl))
            {
                defaultTechTreeUrl = HighLogic.CurrentGame.Parameters.Career.TechTreeUrl;
            }

            HETTNSettings.Log("Reading RDNodes from {0}...", defaultTechTreeUrl);

            // --------------------
            // Lists and variables.
            // --------------------
            // Config file paths to RDNodes from default tech tree path.
            ConfigNode configFile = ConfigNode.Load(KSPUtil.ApplicationRootPath + defaultTechTreeUrl);
            ConfigNode configTechTree = configFile.GetNode("TechTree");
            ConfigNode[] configRDNodes = configTechTree.GetNodes("RDNode");

            // Lists for RDNodes in default tech tree and for new HETTN teach tree.
            List<HENode> rdNodesDefaultList = new List<HENode>();
            List<HENode> rdNodesNewList = new List<HENode>();

            // Dictionaries to find RDNodes and postions.
            Dictionary<string, HENode> nodesList = new Dictionary<string, HENode>();
            Dictionary<string, Vector3> positionsList = new Dictionary<string, Vector3>();

            // Start node.
            bool startExists = false;

            // Counts for hidden nodes, changed node-parent links, and added/removed parents.
            int hiddenNodesCount = 0;
            int changedParentsCount = 0;
            int addedParentsCount;
            int removedParentsCount;
            bool removedParents;
            bool removedAnyParents = false;

            // Duplicate nodes.
            int duplicateIDCount = 0;
            List<string> duplicateIDList = new List<string>();


            // ETT fix 3 of 3.
            // ----------------------------------------------------------------------
            // Changes the TechRequired field for all Parts by using the new field in
            // the Unlocks nodes found in the TechTree file.
            // ----------------------------------------------------------------------
            bool HasUnlocks = false;
            for (int i = 0; i < configRDNodes.Length; i++)
            {
                // Load config node.
                HENode rdNode = new HENode();
                rdNode.Load(configRDNodes[i]);
                //rdNode.LoadUnlocks(configRDNodes[i]);
                if (rdNode == null)
                {
                    HETTNSettings.LogError("RDNode \"{0}\" has missing keys! Check tech tree config files! Cannot hide empty nodes.",
                        rdNode.techID);
                    return;
                }

                HETTNSettings.Log2("Checking Unlocks for RDNode: \"{0}\"...", rdNode.techID);
                if (rdNode.unlocks != null && rdNode.unlocks.ettPartsList != null)
                {
                    HasUnlocks = true;
                    HETTNSettings.Log2("   \"{0}\" has {1} parts listed in Unlocks node. Changing TechRequired for any existing parts...", rdNode.techID, rdNode.unlocks.ettPartsList.Length);
                    for (int j = 0; j < rdNode.unlocks.ettPartsList.Length; j++)
                    {
                        if (PartLoader.getPartInfoByName(rdNode.unlocks.ettPartsList[j].Replace("_", ".")) != null)
                        {
                            HETTNSettings.Log2("   Changing \"{0}\" TechRequired field from \"{1}\" to \"{2}\".",
                                PartLoader.getPartInfoByName(rdNode.unlocks.ettPartsList[j].Replace("_", ".")).name,
                                PartLoader.getPartInfoByName(rdNode.unlocks.ettPartsList[j].Replace("_", ".")).TechRequired,
                                rdNode.techID);
                            PartLoader.getPartInfoByName(rdNode.unlocks.ettPartsList[j].Replace("_", ".")).TechRequired = rdNode.techID;
                        }
                    }
                }
            }
            if (HasUnlocks)
                HETTNSettings.Log("\"Unlocks\" nodes were found and TechRequred fields were changed for listed Parts.");


            // PARTUPGRADE fix 1 of 2:
            // -----------------------------------------------------------------------
            // Get PARTUPGRADE config nodes, and add the techRequired field to a list.
            // The node preload section will force these nodes to be unhidden.
            // -----------------------------------------------------------------------
            List<string> techRequiredList = new List<string>();
            int techRequiredNotHiddenCount = 0;

            if (HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().PartUpgradesInCareer)
            {
                ConfigNode[] configPUNodes = GameDatabase.Instance.GetConfigNodes("PARTUPGRADE");
                int count = configPUNodes.Length;
                HETTNSettings.Log2("Found {0} PARTUPGRADE config nodes.", count);
                for (int i = 0; i < count; i++)
                {
                    string techRequired = configPUNodes[i].GetValue("techRequired");
                    if (!String.IsNullOrEmpty(techRequired))
                    {
                        HETTNSettings.Log2("Found tech upgrade in \"{0}\".", techRequired);
                        techRequiredList.Add(techRequired);
                    }
                }
            }
            else
            {
                HETTNSettings.Log("PARTUPGRADEs are not enabled. Enable in Advanced KSP settings if desired.");
            }


            // -------------------------------------------------
            // Preload RDNode config nodes, and find start node.
            // -------------------------------------------------
            for (int i = 0; i < configRDNodes.Length; i++)
            {
                // Load config node.
                HENode rdNode = new HENode();
                rdNode.Load(configRDNodes[i]);
                if (rdNode == null)
                {
                    HETTNSettings.LogError("RDNode \"{0}\" has missing keys! Check tech tree config files! Cannot hide empty nodes.",
                        rdNode.techID);
                    HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = defaultTechTreeUrl;
                    return;
                }

                // Auto-hide all nodes unless manual hide option is enabled.
                if (!hettnSettings.forceHideManual)
                {
                    rdNode.hideIfNoParts = hettnSettings.forceHideEmpty;
                }

                // PARTUPGRADE fix 2 of 2:
                // Unless turned off in Advanced KSP settings, do not hide these tech tree nodes with only PARTUPGRADEs.
                if (HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().PartUpgradesInCareer)
                {
                    if (rdNode.PartsInTotal < 1 && techRequiredList.Exists(tr => tr == rdNode.techID))
                    {
                        rdNode.hideIfNoParts = false;
                        techRequiredNotHiddenCount++;
                    }
                }

                // Change all nodes to the current research requirements setting unless set to Default.
                if (hettnSettings.researchRequirements == Localizer.Format("#autoLOC_HETTN_01402")) //"Any"
                {
                    rdNode.AnyParentToUnlock = true;
                }
                else if (hettnSettings.researchRequirements == Localizer.Format("#autoLOC_HETTN_01403")) //"All"
                {
                    rdNode.AnyParentToUnlock = false;
                }

                // Find the start node.
                if (!startExists && rdNode.techID == startNodeID)
                {
                    startNode = rdNode;
                    startExists = true;
                }

                // Check if RDNode node already exists by id.
                if (nodesList.ContainsKey(rdNode.techID))
                {
                    duplicateIDCount++;
                    duplicateIDList.Add(rdNode.techID);
                    rdNode.techID = String.Concat(rdNode.techID, "Dup", duplicateIDCount);
                    rdNode.title = String.Concat(rdNode.title, " (Duplicate Node #)", duplicateIDCount);
                    rdNode.partsAssigned.Clear();
                    rdNode.PartsInTotal = rdNode.partsAssigned.Count;
                }

                // Add the nodes to a default list.
                rdNodesDefaultList.Add(rdNode);

                //HETTNSettings.Log2("RDNode: \"{0}\"\n Title: {1}\n Description: {2}\n Cost: {3}\n HideEmpty: {4}\n Name: {5}\n Unlock: {6}\n Icon: {7}\n Pos: {8}\n Group: {9}\n Scale: {10}\n NumOfParts: {11}\n NumOfParents: {12}.",
                //                    rdNode.techID,
                //                    rdNode.title,
                //                    rdNode.description,
                //                    rdNode.scienceCost,
                //                    rdNode.hideIfNoParts,
                //                    rdNode.nodeName,
                //                    rdNode.AnyParentToUnlock,
                //                    rdNode.iconRef,
                //                    rdNode.pos,
                //                    rdNode.posGroup,
                //                    rdNode.scale,
                //                    rdNode.PartsInTotal,
                //                    rdNode.parents.Length);
            }

            // Give error here if start node was not found.
            if (!startExists)
            {
                HETTNSettings.LogError("Failed to find start node. Exiting plugin...");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = defaultTechTreeUrl;
                return;
            }

            // Preload parent nodes.
            for (int i = 0; i < configRDNodes.Length; i++)
            {
                rdNodesDefaultList[i].LoadLinks(configRDNodes[i], rdNodesDefaultList);
            }

            // Shift nodes to remove empty space if option is enabled.
            if (hettnSettings.shiftVertically || hettnSettings.shiftHorizontally)
            {
                ShiftNodes(rdNodesDefaultList);
            }

            // Bring start node back to beginning of list to center camera on the start node.
            int startIndex = rdNodesDefaultList.FindIndex(n => n.techID == startNodeID);
            rdNodesDefaultList.RemoveAt(startIndex);
            rdNodesDefaultList.Insert(0, startNode);

            // Add nodes to a dictionary for lookup.
            for (int i = 0; i < rdNodesDefaultList.Count; i++)
            {
                nodesList.Add(rdNodesDefaultList[i].techID, rdNodesDefaultList[i]);
                positionsList.Add(rdNodesDefaultList[i].techID, rdNodesDefaultList[i].pos);
            }

            // Preload children
            for (int i = 0; i < rdNodesDefaultList.Count; i++)
            {
                for (int j = 0; j < rdNodesDefaultList[i].parents.Count(); j++)
                {
                    nodesList[rdNodesDefaultList[i].parents[j].parentID].children.Add(rdNodesDefaultList[i].techID);
                }
            }

            // Debug and log output.
            HETTNSettings.Log2("RDNode config preload successful.");
            if (techRequiredList.Count > 0)
                HETTNSettings.Log("Not hiding {0} unique PARTUPGRADE only {1}.",
                    techRequiredNotHiddenCount,
                    techRequiredNotHiddenCount == 1 ? "node" : "nodes");
            HETTNSettings.Log2("Start node \"{0}\" position is:  x: {1}, y: {2}.", startNodeID, startNode.pos.x, startNode.pos.y);


            // If forceHide is true, carry out parent changes. Else save any current changes to nodes as is.
            if (hettnSettings.forceHideEmpty)
            {
                // -----------------------------------------------------------------------------------------------
                // Logic for rearranging parents starts here.
                // Here, we iterate through EACH node and search for several conditions. The steps are:
                // (0) Create output for debug HNSettings.log.
                // (1) Check if node will be hidden (and transfer science if option is enabled).
                // (2) Preload parents of node to list.
                // (3) Remove any hidden parents, and replace with those parents' parents. Remove duplictaes, etc.
                // (4) Remove and replace parents that cause tech lines to intersect with other parents/nodes.
                // (5) Remove then add all updated parents to fix a problem with tech lines not fully connecting.
                // (6) Assign updated parents to the node, and add to HETTN tech tree.
                // After iteration, we set the current game's tech tree path to the HETTN tech tree and save tree.
                // -----------------------------------------------------------------------------------------------
                HETTNSettings.Log2("Attempting to hide empty nodes and change bad node-parent links...");
                foreach (HENode node in rdNodesDefaultList)
                {
                    // --------------------------------------------------------------------------------------
                    // (0) Debug log output
                    // Output node info to HNSettings.log. Use position from config files. If null, continue.
                    // --------------------------------------------------------------------------------------
                    try
                    {
                        HETTNSettings.Log2("RDNode: \"{0}\" | HideEmpty: {1}, NumOfParts: {2}, NumOfParents: {3}, Position: {4}.",
                                        node.techID,
                                        node.hideIfNoParts,
                                        node.PartsInTotal,
                                        node.parents.Length,
                                        node.pos);

                        string[] partsString = new string[node.partsAssigned.Count];
                        for (int i = 0; i < node.partsAssigned.Count; i++)
                        {
                            partsString[i] = node.partsAssigned[i].name;
                        }
                        if (partsString.Length > 0)
                            HETTNSettings.Log2("RDNode: \"{0}\" | Parts: {1}.",
                                            node.techID,
                                            String.Join(", ", partsString));
                    }
                    catch (Exception e)
                    {
                        HETTNSettings.LogError("All required node keys not found for \"{0}\". Exiting.\n" + e, node.techID);
                        return;
                    }


                    // ------------------------------------------------------------------------------------------
                    // (1) Check if node will be hidden.
                    // If node is hidden (i.e. there are no parts and set to hide if no parts), propagate science
                    // (if enabled) and then just skip the rest.
                    // ------------------------------------------------------------------------------------------
                    if (node.PartsInTotal < 1 && node.hideIfNoParts == true)
                    {
                        // Propagate science points
                        bool propagateScience = false;
                        if (hettnSettings.propagateScience == Localizer.Format("#autoLOC_HETTN_01502")) //"Science Wall (Transfer to children)"
                        {
                            propagateScience = true;

                            bool isChildAdded = true;

                            // Add hidden childrens' children
                            while (isChildAdded)
                            {
                                isChildAdded = false;
                                List<string> childrenToAdd = new List<string>();
                                List<string> childrenToRemove = new List<string>();

                                foreach (string child in node.children)
                                {
                                    HENode childNode = nodesList[child];
                                    if (childNode.PartsInTotal < 1 && childNode.hideIfNoParts == true)
                                    {
                                        childrenToRemove.Add(childNode.techID);
                                        foreach (string child2 in childNode.children)
                                        {
                                            if (!childrenToAdd.Contains(child2) && !node.children.Contains(child2))
                                            {
                                                childrenToAdd.Add(child2);
                                                isChildAdded = true;
                                            }
                                        }
                                    }
                                }

                                foreach (string child in childrenToRemove)
                                    node.children.Remove(child);
                                foreach (string child in childrenToAdd)
                                    node.children.Add(child);
                            }
                        }
                        else if (hettnSettings.propagateScience == Localizer.Format("#autoLOC_HETTN_01503")) //"Propagate Science (Transfer to all descendants)"
                        {
                            propagateScience = true;

                            bool isChildAdded = true;
                            List<string> childrenToRemove = new List<string>();

                            // Add all descendants, initially, and mark and remove hidden descendants
                            while (isChildAdded)
                            {
                                isChildAdded = false;
                                List<string> childrenToAdd = new List<string>();

                                foreach (string child in node.children)
                                {
                                    HENode childNode = nodesList[child];
                                    if (!childrenToRemove.Contains(childNode.techID) && childNode.PartsInTotal < 1 && childNode.hideIfNoParts == true)
                                    {
                                        childrenToRemove.Add(childNode.techID);
                                    }
                                    foreach (string child2 in childNode.children)
                                    {
                                        if (!childrenToAdd.Contains(child2) && !node.children.Contains(child2))
                                        {
                                            childrenToAdd.Add(child2);
                                            isChildAdded = true;
                                        }
                                    }
                                }

                                foreach (string child in childrenToAdd)
                                    node.children.Add(child);
                            }

                            // Remove hidden descendants
                            foreach (string child in childrenToRemove)
                                node.children.Remove(child);
                        }

                        // Transfer science points to children (minimum 5 points)
                        int numChildren = node.children.Count;
                        if (propagateScience && numChildren > 0)
                        {
                            int scienceCost = Math.Max((int)Math.Ceiling((double)node.scienceCost / numChildren), 5);
                            foreach (string child in node.children)
                            {
                                nodesList[child].scienceCost = 5 * (int)Math.Round((double)(nodesList[child].scienceCost + scienceCost) / 5.0);
                            }
                        }

                        // Continue to next node in loop
                        hiddenNodesCount++;
                        continue;
                    }


                    // ------------------------------------------------------------
                    // (2) Preload parents to list.
                    // Create lists and preload parents of node to tempParentsList.
                    // ------------------------------------------------------------
                    // Lists.
                    List<HENode.Parent> tempParentsList = new List<HENode.Parent>();
                    List<HENode.Parent> parentsToRemove = new List<HENode.Parent>();
                    List<HENode.Parent> parentsToAdd = new List<HENode.Parent>();

                    // Initilize parameters to track number of added/removed parents.
                    addedParentsCount = 0;
                    removedParentsCount = 0;
                    removedParents = false;

                    // Preload parents.
                    foreach (HENode.Parent parent in node.parents)
                    {
                        tempParentsList.Add(parent);
                        HETTNSettings.Log2("Parent({0}/{1}): \"{2}\", HideEmpty: {3}, Parts: {4}, Anchors: from {5} to {6}.",
                            ++addedParentsCount,
                            node.parents.Length,
                            parent.parentID,
                            parent.hideIfNoParts,
                            parent.PartsInTotal,
                            parent.lineFrom,
                            parent.lineTo);
                    }


                    // -----------------------------------------------------------------------------------------------------
                    // (3) Remove and replace.
                    // For each parent, if parent is not hidden then continue.
                    // If parent is hidden, remove that parent and add parent's parents to the new parents list, and repeat.
                    // If no parents were ever removed, add node to HETTN tech tree then continue to next node.
                    // -----------------------------------------------------------------------------------------------------
                    HETTNSettings.Log2("Checking for hidden parents...");
                    removedParentsCount = -1;
                    while (removedParentsCount != 0)
                    {
                        removedParentsCount = 0;
                        foreach (HENode.Parent parent in tempParentsList)
                        {
                            if (parent.PartsInTotal > 0 || parent.hideIfNoParts == false)
                                continue;
                            else
                            {
                                parentsToRemove.Add(parent);
                                removedParentsCount++;
                                removedParents = true;
                                removedAnyParents = true;
                                foreach (HENode.Parent parent2 in nodesList[parent.parentID].parents)
                                    parentsToAdd.Add(parent2);
                            }
                        }
                        if (removedParentsCount > 0)
                            tempParentsList = UpdateParents(node, parentsToRemove, parentsToAdd, tempParentsList, nodesList, positionsList, "HIDDEN");
                    }
                    // If no parents were ever removed, these parents have placements that are assumed to be correct (i.e. the default parents).
                    // So, we can add this node to the HETTN tech tree and simply continue to the next node.
                    if (!removedParents)
                    {
                        rdNodesNewList.Add(node);
                        HETTNSettings.Log2("Final: Node \"{0}\" already has GOOD parents. Continuing to next node...", node.techID);
                        continue;
                    }
                    // Total broken parent fixes.
                    changedParentsCount++;


                    // --------------------------------------------------------------------------------------------------------------------
                    // (4) (steps 1-3 should have connected unhidden nodes with unhidden parents)
                    // Here, remove parents that cause tech line intersects with other nodes, and add those nodes as new parents.
                    //
                    // For each node's parents:
                    //
                    // First, make a box around the node and its parent:
                    // (a) Determine if current node is below or above, right or left of its parent.
                    // (b) Find all nodes with a box created by the node and parent.
                    //
                    // Next, remove parents that cause tech line intersections with other nodes in the box. Add those nodes as new parents:
                    // (c) Remove/Exchange parents that cause intersections on horizontal/vertical tech lines (non-bending tech lines).
                    // (d) Remove/Exchange parents that cause intersections at bend, before bend, or after bend (bending tech lines).
                    // --------------------------------------------------------------------------------------------------------------------
                    HETTNSettings.Log2("Checking for nodes between the current node and its parents...");
                    removedParentsCount = -1;
                    while (removedParentsCount != 0)
                    {
                        removedParentsCount = 0;
                        foreach (HENode.Parent parent in tempParentsList)
                        {
                            // New parents list.
                            List<HENode.Parent> newParentsList = new List<HENode.Parent>();

                            // Position of current node and current parent.
                            double posXNode = positionsList[node.techID].x;
                            double posYNode = positionsList[node.techID].y;
                            double posXParent = positionsList[parent.parentID].x;
                            double posYParent = positionsList[parent.parentID].y;

                            // Position of the tech line bend.
                            int bendAt = 2;
                            double posXBend = (posXNode + posXParent) / bendAt;
                            double posYBend = (posYNode + posYParent) / bendAt;

                            // Used to determine relative positon of current node and current parent.
                            double rightX;
                            double leftX;
                            double upperY;
                            double lowerY;

                            // (a) Determine if current node is right, left, below or above parent.
                            if (posXNode >= posXParent)
                            {
                                leftX = posXParent;
                                rightX = posXNode;
                            }
                            else
                            {
                                leftX = posXNode;
                                rightX = posXParent;
                            }
                            if (posYNode <= posYParent)
                            {
                                upperY = posYParent;
                                lowerY = posYNode;
                            }
                            else
                            {
                                upperY = posYNode;
                                lowerY = posYParent;
                            }

                            // (b) Find unhidden(1) nodes in box(2-5), not including current node and current parent(6)
                            HETTNSettings.Log2("New box made by: \"{0}\" and \"{1}\".", node.techID, parent.parentID);
                            foreach (HENode nodeInBox in rdNodesDefaultList.FindAll(n =>
                                (n.PartsInTotal > 0 || n.hideIfNoParts == false) &&
                                positionsList[n.techID].x >= (leftX - posXTolerance) &&
                                positionsList[n.techID].x <= (rightX + posXTolerance) &&
                                positionsList[n.techID].y <= (upperY + posYTolerance) &&
                                positionsList[n.techID].y >= (lowerY - posYTolerance) &&
                                !(new[] { node.techID, parent.parentID }.Contains(n.techID))))
                            {
                                // Position of current node in box.
                                double posXNodeInBox = positionsList[nodeInBox.techID].x;
                                double posYNodeInBox = positionsList[nodeInBox.techID].y;

                                HETTNSettings.Log2("Found in box: \"{0}\" at ({1},{2})  (Tech line bends at ({3},{4})).",
                                    nodeInBox.techID,
                                    posXNodeInBox, posYNodeInBox,
                                    posXBend, posYBend);

                                // Check if node in box is on top of current node or current parent (useful for duplicates)
                                if (IsClose(posXNodeInBox, posXParent, posXTolerance) && IsClose(posYNodeInBox, posYParent, posYTolerance))
                                {
                                    HETTNSettings.LogWarning("Nodes \"{0}\" (ID: {1}) and \"{2}\" (ID: {3}) are on top of each other (key 1) (pos: {4}, {5}). Will not attempt to fix. Please fix manually",
                                        nodeInBox.title,
                                        nodeInBox.techID,
                                        nodesList[parent.parentID].title,
                                        nodesList[parent.parentID].techID,
                                        nodeInBox.pos,
                                        nodesList[parent.parentID].pos);
                                    continue;
                                }
                                else if (IsClose(posXNodeInBox, posXNode, posXTolerance) && IsClose(posYNodeInBox, posYNode, posYTolerance))
                                {
                                    HETTNSettings.LogWarning("Nodes \"{0}\" (ID: {1}) and \"{2}\" (ID: {3}) are on top of each other (key 2) (pos: {4}, {5}). Will not attempt to fix. Please fix manually",
                                        nodeInBox.title,
                                        nodeInBox.techID,
                                        node.title,
                                        node.techID,
                                        nodeInBox.pos,
                                        node.pos);
                                    continue;
                                }

                                // (c) Remove parents that cause any intersections at NON-BENDING intersections.
                                // Determine if that node is not already set to be removed(1), and is also(2) either(7)
                                // in between the tech line horizontally(3-6) or(7) vertically(8-11).
                                if (!parentsToRemove.Exists(p => p.parentID == nodeInBox.techID)
                                    &&
                                    ((((parent.lineFrom == HENode.Anchor.RIGHT && parent.lineTo == HENode.Anchor.LEFT) ||
                                    (parent.lineFrom == HENode.Anchor.LEFT && parent.lineTo == HENode.Anchor.RIGHT)) &&
                                    IsClose(posYNodeInBox, posYParent, posYTolerance) &&
                                    IsClose(posYNodeInBox, posYNode, posYTolerance))
                                    ||
                                    (((parent.lineFrom == HENode.Anchor.TOP && parent.lineTo == HENode.Anchor.BOTTOM) ||
                                    (parent.lineFrom == HENode.Anchor.BOTTOM && parent.lineTo == HENode.Anchor.TOP)) &&
                                    IsClose(posXNodeInBox, posXParent, posXTolerance) &&
                                    IsClose(posXNodeInBox, posXNode, posXTolerance))))
                                {
                                    HETTNSettings.Log2("Update: Parent: \"{0}\" causes a {1}->{2} NON-BENDING intersect at Node: \"{3}\"."
                                        + " Will remove Parent, and add Node if not currently a parent.",
                                        parent.parentID,
                                        parent.lineFrom,
                                        parent.lineTo,
                                        nodeInBox.techID);
                                    parentsToRemove.Add(parent);
                                    removedParentsCount++;
                                    // Add node as a new parent if not already a parent.
                                    if (!tempParentsList.Exists(p => p.parentID == nodeInBox.techID))
                                    {
                                        HENode.Parent newParent = NewParentNode(nodeInBox.techID, parent.lineFrom, parent.lineTo);
                                        newParentsList.Add(newParent);
                                    }
                                    //continue;
                                }

                                // (d) Remove parents that cause any intersections on BENDING intersections.
                                // (Not all connections are possible, such as bottom->top or top->right(?).)
                                // (Directions are shown by anchor points, not tech line arrow direction.)
                                // Determine if that node is not already set to be removed(1), and(2) is also either(8,14,19,25)
                                // intersecting a right->left bend(3-7) or(8)
                                // intersecting a left->right bend(9-13) or(14)
                                // intersecting a top->left bend(15-18) or(19)
                                // intersecting a top->bottom bend(20-24) or(25)
                                // intersecting a right->bottom bend(26-29).
                                if (!parentsToRemove.Exists(p => p.parentID == nodeInBox.techID)
                                    &&
                                    (((parent.lineFrom == HENode.Anchor.RIGHT && parent.lineTo == HENode.Anchor.LEFT) &&
                                    !IsClose(posYNode, posYParent, posYTolerance) &&
                                    (IsClose(posXNodeInBox, posXBend, posXTolerance) ||
                                    (posXNodeInBox < posXBend && IsClose(posYNodeInBox, posYParent, posYTolerance)) ||
                                    (posXNodeInBox > posXBend && IsClose(posYNodeInBox, posYNode, posYTolerance))))
                                    ||
                                    ((parent.lineFrom == HENode.Anchor.LEFT && parent.lineTo == HENode.Anchor.RIGHT) &&
                                    !IsClose(posYNode, posYParent, posYTolerance) &&
                                    (IsClose(posXNodeInBox, posXBend, posXTolerance) ||
                                    (posXNodeInBox < posXBend && IsClose(posYNodeInBox, posYNode, posYTolerance)) ||
                                    (posXNodeInBox > posXBend && IsClose(posYNodeInBox, posYParent, posYTolerance))))
                                    ||
                                    ((parent.lineFrom == HENode.Anchor.TOP && parent.lineTo == HENode.Anchor.LEFT) &&
                                    !IsClose(posYNode, posYParent, posYTolerance) &&
                                    (IsClose(posXNodeInBox, posXParent, posXTolerance) ||
                                    (IsClose(posYNodeInBox, posYNode, posYTolerance))))
                                    ||
                                    ((parent.lineFrom == HENode.Anchor.TOP && parent.lineTo == HENode.Anchor.BOTTOM) &&
                                    !IsClose(posXNode, posXParent, posXTolerance) &&
                                    (IsClose(posYNodeInBox, posYBend, posYTolerance) ||
                                    (posYNodeInBox < posYBend && IsClose(posXNodeInBox, posXParent, posXTolerance)) ||
                                    (posYNodeInBox > posYBend && IsClose(posXNodeInBox, posXNode, posXTolerance))))
                                    ||
                                    ((parent.lineFrom == HENode.Anchor.RIGHT && parent.lineTo == HENode.Anchor.BOTTOM) &&
                                    !IsClose(posYNode, posYParent, posYTolerance) &&
                                    (IsClose(posYNodeInBox, posYParent, posYTolerance) ||
                                    (IsClose(posXNodeInBox, posXNode, posXTolerance))))))
                                {
                                    HETTNSettings.Log2("Update: Parent: \"{0}\" causes a {1}->{2} BENDING intersect at Node: \"{3}\"."
                                        + " Will remove Parent, and add Node if not currently a parent.",
                                        parent.parentID,
                                        parent.lineFrom,
                                        parent.lineTo,
                                        nodeInBox.techID);
                                    parentsToRemove.Add(parent);
                                    removedParentsCount++;
                                    // Add node as a parent if not already a parent.
                                    if (!tempParentsList.Exists(p => p.parentID == nodeInBox.techID))
                                    {
                                        HENode.Parent newParent = NewParentNode(nodeInBox.techID, parent.lineFrom, parent.lineTo);
                                        newParentsList.Add(newParent);
                                    }
                                    // continue;
                                }
                            }
                            // Here we would have only removed one parent so far, so keep only one added parent (closest to parent).
                            if (newParentsList.Count > 1)
                                parentsToAdd.Add(GetClosestParent(parent, newParentsList, positionsList));
                            else if (newParentsList.Count == 1)
                                parentsToAdd.Add(newParentsList[0]);
                        }
                        if (removedParentsCount > 0)
                            tempParentsList = UpdateParents(node, parentsToRemove, parentsToAdd, tempParentsList, nodesList, positionsList);
                    }


                    // --------------------------------------------------------------------------------------------------
                    // (5) Reload new parents.
                    // To fix broken tech lines, remove all (currently good) parents and reload them as new parent nodes.
                    // --------------------------------------------------------------------------------------------------
                    foreach (HENode.Parent parent in tempParentsList)
                    {
                        parentsToRemove.Add(parent);
                        HENode.Parent reloadParent = NewParentNode(parent.parentID, parent.lineFrom, parent.lineTo);
                        parentsToAdd.Add(reloadParent);
                    }
                    HETTNSettings.Log2("Reconnecting tech lines to new parents...");
                    tempParentsList = UpdateParents(node, parentsToRemove, parentsToAdd, tempParentsList, nodesList, positionsList);


                    // --------------------------------------------------------------------
                    // (6) Assign new parents
                    // FINALLY, set node parents to our temp array, add to HETTN tech tree.
                    // Then move on to next node.
                    // --------------------------------------------------------------------
                    node.parents = tempParentsList.ToArray();
                    rdNodesNewList.Add(node);
                    if (removedParents)
                        HETTNSettings.Log2("Final: Node \"{0}\" now has GOOD parents. Finding next bad node...", node.techID);
                }


                // -------------------
                // Log output message.
                // -------------------
                if (removedAnyParents)
                    HETTNSettings.Log("Successfully hid {0} empty tech tree {1} and repaired {2} bad node-parent {3}.",
                        hiddenNodesCount,
                        hiddenNodesCount == 1 ? "node" : "nodes",
                        changedParentsCount,
                        changedParentsCount == 1 ? "link" : "links");
                else
                    HETTNSettings.Log("Successfully hid {0} empty tech tree {1}. All default node-parent links are good. No repairs made.",
                        hiddenNodesCount,
                        hiddenNodesCount == 1 ? "node" : "nodes");
            }
            else
            {
                // Set new list to default list (modified in preload) if no nodes are hidden.
                HETTNSettings.Log2("Hide Empty Nodes setting is disabled");
                rdNodesNewList = rdNodesDefaultList;
            }


            // --------------------------
            // Log error display message.
            // --------------------------
            if (duplicateIDCount > 0)
            {
                HETTNSettings.LogError("Duplicate tech nodes exists with the following IDs:\n   {0}\nCheck your mod files for the duplicate nodes and consider contacting the modder(s) to resolve issue. This is for the default ModuleManager tree, not the new HETTN tree with hidden empty nodes. The duplicate nodes are not added to the new tree, but may have modified node position, cost, Parent links, etc.",
                    String.Join("\n   ", duplicateIDList.ToArray()));
                ScreenMessages.PostScreenMessage("<color=orange>Error: Tech tree has " + duplicateIDCount + " nodes with duplicate IDs\n\nSee debug log (Alt+F12) to see list of IDs.\n\nThen check your mod files for the duplicate nodes and consider contacting the modder(s) to resolve the issue.</color>", 20f, ScreenMessageStyle.UPPER_CENTER);
            }


            // -----------------------------------------------
            // Save the tech tree and apply tech tree path the
            // new HETTN.TechTree file.
            // -----------------------------------------------
            HETTNSettings.Log2("Saving nodes...");

            string fileFullName = KSPUtil.ApplicationRootPath + hettnTechTreeUrl;
            ConfigNode newConfigNode = new ConfigNode();
            ConfigNode newConfigNode2 = newConfigNode.AddNode("TechTree");
            for (int i = 0; i < rdNodesNewList.Count; i++)
            {
                HETTNSettings.Log2("Saving {0}/{1}: {2}...", i + 1, rdNodesNewList.Count, rdNodesNewList[i].techID);
                ConfigNode node = newConfigNode2.AddNode("RDNode");
                rdNodesNewList[i].Save(node);
            }
            newConfigNode.Save(fileFullName);

            HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = hettnTechTreeUrl;
            IsHettnUrlCreated = true;
            HETTNSettings.Log("New tech tree saved to: {0}.", HighLogic.CurrentGame.Parameters.Career.TechTreeUrl);
        }
        #endregion


        // ----------------------------------------------------------------------
        // Methods to update parents list. Recursive to remove duplicate parents.
        // ----------------------------------------------------------------------
        #region UPDATE PARENTS
        // New Parent method to reload parents as nodes (fixes broken anchor links (aka tech lines) as well)
        public HENode.Parent NewParentNode(string parentID, HENode.Anchor lineFrom, HENode.Anchor lineTo)
        {
            ConfigNode newParentNode = new ConfigNode();
            newParentNode.AddValue("parentID", parentID);
            newParentNode.AddValue("lineFrom", lineFrom);
            newParentNode.AddValue("lineTo", lineTo);
            HENode.Parent newParent = new HENode.Parent(newParentNode);
            return newParent;
        }

        // Update parents. Used when encountered a bad parent. Recursive to remove duplicate parents.
        public List<HENode.Parent> UpdateParents(HENode node, List<HENode.Parent> remove, List<HENode.Parent> add, List<HENode.Parent> update, Dictionary<string, HENode> nodesList, Dictionary<string, Vector3> positionsList, string message = "none")
        {
            if (message != "none")
                HETTNSettings.Log2("Update: Node \"{0}\" has some {1} parents. Fixing parents...", node.techID, message);
            // Remove and add parents to updated parents list.
            foreach (HENode.Parent p in remove)
                update.Remove(p);
            foreach (HENode.Parent p in add)
                update.Add(p);
            remove.Clear();
            add.Clear();
            if (message != "none")
            {
                foreach (HENode.Parent p in update)
                {
                    HETTNSettings.Log2("New Parent: \"{0}\", HideEmpty: {1}, Parts: {2}, Anchors: from {3} to {4}.",
                        p.parentID,
                        p.hideIfNoParts,
                        p.PartsInTotal,
                        p.lineTo,
                        p.lineTo);
                }
            }
            // Remove duplicate parents. Calls method recursively. update.Distinct().ToList() does not seem to work,
            // so check for duplication manually by "id" string. For loop avoids checking nodes against themselves.
            int removedParentsCount = -1;
            while (removedParentsCount != 0)
            {
                removedParentsCount = 0;
                for (int i = 0; i < update.Count - 1; i++)
                {
                    for (int j = i + 1; j < update.Count; j++)
                    {
                        if (update[i].parentID == update[j].parentID)
                        {
                            remove.Add(update[j]);
                            removedParentsCount++;
                        }
                    }
                }
                if (removedParentsCount > 0)
                    update = UpdateParents(node, remove, add, update, nodesList, positionsList, "DUPLICATE");
            }
            // Remove parents that are the ONLY ancestors of other parents (requires ANY, ALL, etc.).
            if (update.Count > 1)
                update = RemoveAncestors(update, nodesList);
            // Update anchors.
            update = UpdateAnchors(node, update, positionsList);
            // Remove parents if exceed max allowable.
            if (update.Count > maxParents)
                update = MaintainMaxParents(node, update, positionsList);
            // Return final updated parents list.
            remove.Clear();
            add.Clear();
            return update;
        }


        // Remove parents that are the ONLY ancestors of ANY other parents (requires ANY, ALL, etc.).
        public List<HENode.Parent> RemoveAncestors(List<HENode.Parent> update, Dictionary<string, HENode> nodesList)
        {
            // List of ancestors to remove.
            List<HENode.Parent> removeAncestors = new List<HENode.Parent>();
            // Get minimum science cost of current parents.
            int minScienceCost = Int32.MaxValue;
            foreach (HENode.Parent p in update)
            {
                if (nodesList[p.parentID].scienceCost < minScienceCost)
                    minScienceCost = nodesList[p.parentID].scienceCost;
            }
            // Remove ancestors.
            foreach (HENode.Parent p in update)
            {
                // Ignore the parent with minimum science cost.
                if (nodesList[p.parentID].scienceCost <= minScienceCost)
                    continue;
                // List of nodes to compare against parents.
                List<HENode> nodesToCompare = new List<HENode>();
                // Add the current parent to the compare list as a node.
                nodesToCompare.Add(nodesList[p.parentID]);
                // Remove all ancestors of the parent that are parents of the update list.
                int nodeIndex = 0;
                var idComparer = new IDComparer();
                while (nodeIndex <= nodesToCompare.Count - 1)
                {
                    // Remove ancestors if node being compared requires ALL to unlock, or the number of common ancestors equals its number of parents.
                    List<HENode.Parent> ancestors = update.Intersect(nodesToCompare[nodeIndex].parents, idComparer).ToList();
                    if (nodesToCompare[nodeIndex].AnyParentToUnlock == false || (ancestors.Count >= nodesToCompare[nodeIndex].parents.Length))
                    {
                        HETTNSettings.Log2("Update: Removed parents that are direct ancestors of other parents.");
                        removeAncestors.AddRange(ancestors);
                    }
                    // Add parents of current parent to compare list as nodes, if they have greater science cost than minimum science cost.
                    foreach (HENode.Parent p1 in nodesToCompare[nodeIndex].parents)
                    {
                        if (nodesList[p1.parentID].scienceCost > minScienceCost)
                            nodesToCompare.Add(nodesList[p1.parentID]);
                    }
                    nodeIndex++;
                }
            }
            // Update parents.
            foreach (HENode.Parent p in removeAncestors)
                update.Remove(p);
            return update;
        }

        // Fix anchor problems (pAnchor->nAnchor) if they exist. Some anchor connections are not possible (ex. BOTTOM->TOP, TOP->RIGHT(?))
        private List<HENode.Parent> UpdateAnchors(HENode node, List<HENode.Parent> update, Dictionary<string, Vector3> positionsList)
        {
            foreach (HENode.Parent parent in update)
            {
                // If not RIGHT->LEFT or TOP->LEFT, but
                // node is 1) right of parent, 2) *not directly* above of parent, then
                // fix to RIGHT->LEFT
                if (((parent.lineFrom != HENode.Anchor.RIGHT || parent.lineFrom != HENode.Anchor.TOP) && parent.lineTo != HENode.Anchor.LEFT) &&
                    positionsList[node.techID].x > (positionsList[parent.parentID].x + posXTolerance) &&
                    !IsClose(positionsList[node.techID].x, positionsList[parent.parentID].x, posXTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 1)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        HENode.Anchor.RIGHT,
                        HENode.Anchor.LEFT);
                    parent.lineFrom = HENode.Anchor.RIGHT;
                    parent.lineTo = HENode.Anchor.LEFT;
                }
                // If not LEFT->RIGHT or TOP->RIGHT, but
                // node is 1) left of parent, 2) *not directly* above of parent, then
                // fix to LEFT->RIGHT
                if (((parent.lineFrom != HENode.Anchor.LEFT || parent.lineFrom != HENode.Anchor.TOP) && parent.lineTo != HENode.Anchor.RIGHT) &&
                    positionsList[node.techID].x < (positionsList[parent.parentID].x - posXTolerance) &&
                    !IsClose(positionsList[node.techID].x, positionsList[parent.parentID].x, posXTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 2)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        HENode.Anchor.LEFT,
                        HENode.Anchor.RIGHT);
                    parent.lineFrom = HENode.Anchor.LEFT;
                    parent.lineTo = HENode.Anchor.RIGHT;
                }
                // If TOP->BOTTOM, but
                // node is 1) right of parent, 2) *not directly* above of parent, then
                // fix to TOP->LEFT
                if ((parent.lineFrom == HENode.Anchor.TOP && parent.lineTo == HENode.Anchor.BOTTOM) &&
                    positionsList[node.techID].x > (positionsList[parent.parentID].x + posXTolerance) &&
                    !IsClose(positionsList[node.techID].x, positionsList[parent.parentID].x, posXTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 3)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        parent.lineFrom,
                        HENode.Anchor.LEFT);
                    parent.lineTo = HENode.Anchor.LEFT;
                }
                // If TOP->?, but
                // node is below parent, then
                // fix to RIGHT->?
                if (parent.lineFrom == HENode.Anchor.TOP &&
                    positionsList[node.techID].y < (positionsList[parent.parentID].y - posYTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 4)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        HENode.Anchor.RIGHT,
                        parent.lineTo);
                    parent.lineFrom = HENode.Anchor.RIGHT;
                }
                // If ?->?, but
                // node is *directly* above parent, then
                // fix to TOP->BOTTOM
                if ((parent.lineFrom != HENode.Anchor.TOP || parent.lineTo != HENode.Anchor.BOTTOM) &&
                    positionsList[node.techID].y > (positionsList[parent.parentID].y + posYTolerance) &&
                    IsClose(positionsList[node.techID].x, positionsList[parent.parentID].x, posXTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 5)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        HENode.Anchor.TOP,
                        HENode.Anchor.BOTTOM);
                    parent.lineFrom = HENode.Anchor.TOP;
                    parent.lineTo = HENode.Anchor.BOTTOM;
                }
                // If ?->?, but
                // node is *directly* right of parent, then
                // fix to RIGHT->LEFT
                if ((parent.lineFrom != HENode.Anchor.RIGHT || parent.lineTo != HENode.Anchor.LEFT) &&
                    positionsList[node.techID].x > (positionsList[parent.parentID].x + posYTolerance) &&
                    IsClose(positionsList[node.techID].y, positionsList[parent.parentID].y, posYTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 6)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        HENode.Anchor.RIGHT,
                        HENode.Anchor.LEFT);
                    parent.lineFrom = HENode.Anchor.RIGHT;
                    parent.lineTo = HENode.Anchor.LEFT;
                }
                // If ?->?, but
                // node is *directly* left of parent, then
                // fix to RIGHT->LEFT
                if ((parent.lineFrom != HENode.Anchor.LEFT || parent.lineTo != HENode.Anchor.RIGHT) &&
                    positionsList[node.techID].x < (positionsList[parent.parentID].x - posYTolerance) &&
                    IsClose(positionsList[node.techID].y, positionsList[parent.parentID].y, posYTolerance))
                {
                    HETTNSettings.Log2("Update: Changing {0}->{1} anchors from {2}->{3} to {4}->{5} (key 6)...",
                        parent.parentID,
                        node.techID,
                        parent.lineFrom,
                        parent.lineTo,
                        HENode.Anchor.LEFT,
                        HENode.Anchor.RIGHT);
                    parent.lineFrom = HENode.Anchor.LEFT;
                    parent.lineTo = HENode.Anchor.RIGHT;
                }
            }
            return update;
        }

        // Maintain maximum number of parents. Choose parents closest to node (very sensitive to node placement. I don't
        // know where I can use IsClose to mitigate this, unless I don't use a lamda expression).
        private List<HENode.Parent> MaintainMaxParents(HENode node, List<HENode.Parent> update, Dictionary<string, Vector3> positionsList)
        {
            HETTNSettings.Log2("Number of parents exceeds max. Removing some parents...");
            // Absolute value (key) and positions (value) list for vertical closeness (same tech branch).
            var closestYList = new List<KeyValuePair<double, double>>();
            foreach (HENode.Parent parent in update)
            {
                double absYNear = Math.Abs(positionsList[parent.parentID].y - positionsList[node.techID].y);
                double posYNear = positionsList[parent.parentID].y;
                closestYList.Add(new KeyValuePair<double, double>(absYNear, posYNear));
            }
            closestYList.Sort(CompareAbs);
            closestYList.RemoveRange(maxParents, update.Count - maxParents);
            // Put values to new list for query.
            List<double> posYList = new List<double>();
            foreach (var y in closestYList)
                posYList.Add(y.Value);
            // Remove all unkept parents.
            update.RemoveAll(p => !posYList.Contains(positionsList[p.parentID].y));

            // Check for horizontal closeness if there are still too many parents.
            if (update.Count > maxParents)
            {
                // Absolute value (key) and positions (value) list for horizontal closeness (near node).
                var closestXList = new List<KeyValuePair<double, double>>();
                foreach (HENode.Parent parent in update)
                {
                    double absXNear = Math.Abs(positionsList[parent.parentID].x - positionsList[node.techID].x);
                    double posXNear = positionsList[parent.parentID].x;
                    closestXList.Add(new KeyValuePair<double, double>(absXNear, posXNear));
                }
                closestXList.Sort(CompareAbs);
                closestXList.RemoveRange(maxParents, update.Count - maxParents);
                // Put values to new list for query.
                List<double> posXList = new List<double>();
                foreach (var x in closestXList)
                    posXList.Add(x.Value);
                // Remove all unkept parents.
                update.RemoveAll(p => !posXList.Contains(positionsList[p.parentID].x));

                // If there are still too many nodes, then just remove range.
                if (update.Count > maxParents)
                    update.RemoveRange(maxParents, update.Count - maxParents);

            }
            return update;
        }

        // Method to find the closest parent to the current node.
        private HENode.Parent GetClosestParent(HENode.Parent parentOld, List<HENode.Parent> parentsNew, Dictionary<string, Vector3> positions)
        {
            HENode.Parent closestNewParent;
            var nodesDist = new List<KeyValuePair<double, HENode.Parent>>();

            foreach (HENode.Parent parent in parentsNew)
            {
                double oldX = positions[parentOld.parentID].x;
                double oldY = positions[parentOld.parentID].y;

                double newX = positions[parent.parentID].x;
                double newY = positions[parent.parentID].y;

                double nodeDist = Math.Sqrt(Math.Pow((oldX - newX), 2) + Math.Pow((oldY - newY), 2));

                nodesDist.Add(new KeyValuePair<double, HENode.Parent>(nodeDist, parent));
            }

            nodesDist.Sort(CompareSqrt);
            closestNewParent = nodesDist.First().Value;

            return closestNewParent;
        }
        #endregion


        // --------------------------------
        // Methods to shift node positions.
        // --------------------------------
        #region SHIFT NODES
        public List<HENode> ShiftNodes(List<HENode> nodesList)
        {
            // Arrays for x and y positons.
            float[] posXList = new float[nodesList.Count];
            float[] posYList = new float[nodesList.Count];

            // Get all x and y positions into an array.
            for (int i = 0; i < nodesList.Count; i++)
            {
                posXList[i] = nodesList[i].pos.x;
                posYList[i] = nodesList[i].pos.y;
            }

            // Get mode of all node positions.
            float modeX = ModeDiff(posXList);
            float modeY = ModeDiff(posYList);

            // Counts for position groups.
            int posGroupXCount = 1;
            int posGroupYCount = 1;

            // Sort and group horizontally by half of mode value.
            nodesList.Sort((a, b) => a.pos.x.CompareTo(b.pos.x));
            nodesList[0].posGroup.x = posGroupXCount;
            for (int i = 1; i < nodesList.Count; i++)
            {
                if (nodesList[i].pos.x - nodesList[i - 1].pos.x > modeX / 2)
                    posGroupXCount++;

                nodesList[i].posGroup.x = posGroupXCount;

                HETTNSettings.Log2("    {0} | {1} | {2}", nodesList[i].title, nodesList[i].posGroup, nodesList[i].pos);
            }

            // Sort and group vertically by half of mode value.
            nodesList.Sort((a, b) => a.pos.y.CompareTo(b.pos.y));
            nodesList[0].posGroup.y = posGroupYCount;
            for (int i = 1; i < nodesList.Count; i++)
            {
                if (nodesList[i].pos.y - nodesList[i - 1].pos.y > modeY / 2)
                    posGroupYCount++;

                nodesList[i].posGroup.y = posGroupYCount;

                HETTNSettings.Log2("    {0} | {1} | {2}", nodesList[i].title, nodesList[i].posGroup, nodesList[i].pos);
            }

            // Debug.
            HETTNSettings.Log2("\nNode Shift Values\n" +
                "  Horizontal (by row):\n    Count: {0}\n    Mean: {1}\n    Mode: {2}\n    From inner: {3}\n" +
                "  Vertical (by column):\n    Count: {4}\n    Mean: {5}\n    Mode: {6}\n    From inner: {7}",
                posGroupYCount, posYList.Average(), modeY, (int)(posGroupYCount / dividePosGroupCount),
                posGroupXCount, posXList.Average(), modeX, (int)(posGroupXCount / dividePosGroupCount));

            // Section to shift nodes vertically then horizontally, based on 2D tech tree quadrants 1-4 with start node at origin.
            for (int quadrant = 1; quadrant < 5; quadrant++)
            {
                HETTNSettings.Log2("\n   Shifting nodes in quadrant {0}...", quadrant);
                switch (quadrant)
                {
                    case 1:
                        if (hettnSettings.shiftVertically)
                            for (int i = posGroupYCount; i > startNode.posGroup.y; i--)
                                FindEmptyGroupsY(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        if (hettnSettings.shiftHorizontally)
                            for (int i = posGroupXCount; i > startNode.posGroup.x; i--)
                                FindEmptyGroupsX(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        break;
                    case 2:
                        if (hettnSettings.shiftVertically)
                            for (int i = posGroupYCount; i > startNode.posGroup.y; i--)
                                FindEmptyGroupsY(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        if (hettnSettings.shiftHorizontally)
                            for (int i = 1; i < startNode.posGroup.x; i++)
                                FindEmptyGroupsX(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        break;
                    case 3:
                        if (hettnSettings.shiftVertically)
                            for (int i = 1; i < startNode.posGroup.y; i++)
                                FindEmptyGroupsY(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        if (hettnSettings.shiftHorizontally)
                            for (int i = 1; i < startNode.posGroup.x; i++)
                                FindEmptyGroupsX(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        break;
                    case 4:
                        if (hettnSettings.shiftVertically)
                            for (int i = 1; i < startNode.posGroup.y; i++)
                                FindEmptyGroupsY(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        if (hettnSettings.shiftHorizontally)
                            for (int i = posGroupXCount; i > startNode.posGroup.x; i--)
                                FindEmptyGroupsX(nodesList, i, quadrant, posGroupXCount, posGroupYCount);
                        break;
                }
            }
            return nodesList;
        }

        // -------------------------------------------------------------------------------------------------------------------------
        // Method to find all empty groups (X-rows, Y-columns) specifically in a given quadrant.
        // First, check for any unhidden nodes in a group. Second, check if there are any unhidden nodes nearby in the next group.
        // If nearby nodes exist, do nothing. If nearby nodes do not exist, check for any hidden nodes in nearest existing group.
        // If still no nodes exist, assign first group to next group. If nodes do exist, then assign first group to next group and
        // shift nodes to new group's average position, shifting previous groups well, UNLESS it is too close to the FOLLOWING group
        // or the start node's group.
        // -------------------------------------------------------------------------------------------------------------------------
        public List<HENode> FindEmptyGroupsX(List<HENode> nodesList, int i, int quadrant, int posGroupXCount, int posGroupYCount)
        {
            GetSigns sign = new GetSigns(quadrant);
            int innerGroupY = (sign.y) * 1000000;
            float oldPosition = (sign.x) * 1000000;
            float newPosition = (sign.x) * 1000000;
            float nextPosition = 0;
            float diffPosition = 0;
            float diffPosition2 = 1000000;

            List<HENode> nodeGroupX = nodesList.FindAll(n =>
                n.posGroup.x == i &&
                FromStartNode(n, quadrant) &&
                !(n.hideIfNoParts && n.PartsInTotal < 1));
            HETTNSettings.Log2("\n All Unhidden Nodes in FIRST group {0}:", i);
            foreach (HENode node in nodeGroupX)
            {
                HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
            }
            if (nodeGroupX.Count > 0)
            {
                foreach (HENode node in nodeGroupX)
                {
                    if (sign.y == 1)
                        innerGroupY = Math.Min(innerGroupY, node.posGroup.y);
                    else if (sign.y == -1)
                        innerGroupY = Math.Max(innerGroupY, node.posGroup.y);
                    if (sign.x == 1)
                        oldPosition = Math.Min(oldPosition, node.pos.x);
                    else if (sign.x == -1)
                        oldPosition = Math.Max(oldPosition, node.pos.x);
                }

                List<HENode> nodeGroupX2 = nodesList.FindAll(n =>
                    n.posGroup.x == i - sign.x &&
                    FromStartNode(n, quadrant) &&
                    (sign.y) * n.posGroup.y > (sign.y) * innerGroupY - (int)(posGroupYCount / dividePosGroupCount) &&
                    !(n.hideIfNoParts && n.PartsInTotal < 1));
                //Math.Abs(innerGroupY - n.posGroup.y) < (int)(posGroupYCount / dividePosGroupCount) &&
                HETTNSettings.Log2("\n Nearest Unhidden Nodes in SECOND group {0}:", i - sign.x);
                foreach (HENode node in nodeGroupX2)
                {
                    HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
                }
                if (nodeGroupX2.Count == 0)
                {
                    List<HENode> nodeGroupX3 = nodesList.FindAll(n =>
                        n.posGroup.x == i - sign.x &&
                        FromStartNode(n, quadrant));
                    HETTNSettings.Log2("\n All Nodes in SECOND group {0}:", i - sign.x);
                    foreach (HENode node in nodeGroupX3)
                    {
                        HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
                    }
                    if (nodeGroupX3.Count == 0)
                    {
                        ReplaceGroupsX(nodesList, i, quadrant, 0);
                    }
                    else
                    {
                        List<float> positionsList = new List<float>();
                        for (int j = 0; j < nodeGroupX3.Count; j++)
                        {
                            positionsList.Add(nodeGroupX3[j].pos.x);
                        }
                        newPosition = GetAveragePos(positionsList);

                        List<HENode> nodeGroupX4 = new List<HENode>();
                        int thirdGroup = -1;
                        if (sign.x == 1)
                        {
                            for (int j = i - 2 * sign.x; j > startNode.posGroup.x; j--)
                            {
                                nodeGroupX4 = nodesList.FindAll(n =>
                                    n.posGroup.x == j &&
                                    FromStartNode(n, quadrant));
                                if (nodeGroupX4.Count > 0)
                                {
                                    thirdGroup = j;
                                    break;
                                }
                            }
                        }
                        else if (sign.x == -1)
                        {
                            for (int j = i - 2 * sign.x; j < startNode.posGroup.x; j++)
                            {
                                nodeGroupX4 = nodesList.FindAll(n =>
                                    n.posGroup.x == j &&
                                    FromStartNode(n, quadrant));
                                if (nodeGroupX4.Count > 0)
                                {
                                    thirdGroup = j;
                                    break;
                                }
                            }
                        }
                        HETTNSettings.Log2("\n All Nodes in THIRD group {0}:", thirdGroup);
                        foreach (HENode node in nodeGroupX4)
                        {
                            HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
                        }
                        if (nodeGroupX4.Count > 0)
                        {
                            List<float> positionsList2 = new List<float>();
                            for (int j = 0; j < nodeGroupX4.Count; j++)
                            {
                                positionsList2.Add(nodeGroupX4[j].pos.x);
                            }
                            nextPosition = GetAveragePos(positionsList2);
                        }

                        diffPosition = newPosition - oldPosition;
                        diffPosition2 = Math.Min(Math.Abs(nextPosition - newPosition), Math.Abs(newPosition - startNode.pos.x));
                        HETTNSettings.Log2("\n Group {0}: Diff Poss = ({1}, {2})", i, diffPosition, diffPosition2);

                        if (sign.x == 1 && Math.Abs(diffPosition2) > 4 * posXTolerance)
                        {
                            for (int j = i; j < posGroupXCount; j++)
                            {
                                ReplaceGroupsX(nodesList, j, quadrant, diffPosition);
                            }
                        }
                        else if (sign.x == -1 && Math.Abs(diffPosition2) > 4 * posXTolerance)
                        {
                            for (int j = i; j > 0; j--)
                            {
                                ReplaceGroupsX(nodesList, j, quadrant, diffPosition);
                            }
                        }
                        else
                        {
                            ReplaceGroupsX(nodesList, i, quadrant, 0);
                        }
                    }
                }
            }
            return nodesList;
        }

        public List<HENode> FindEmptyGroupsY(List<HENode> nodesList, int i, int quadrant, int posGroupXCount, int posGroupYCount)
        {
            GetSigns sign = new GetSigns(quadrant);
            int innerGroupX = (sign.x) * 1000000;
            float oldPosition = (sign.y) * 1000000;
            float newPosition = (sign.y) * 1000000;
            float nextPosition = 0;
            float diffPosition = 0;
            float diffPosition2 = 1000000;

            List<HENode> nodeGroupY = nodesList.FindAll(n =>
                n.posGroup.y == i &&
                FromStartNode(n, quadrant) &&
                !(n.hideIfNoParts && n.PartsInTotal < 1));
            HETTNSettings.Log2("\n All Unhidden Nodes in FIRST group {0}:", i);
            foreach (HENode node in nodeGroupY)
            {
                HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
            }
            if (nodeGroupY.Count > 0)
            {
                foreach (HENode node in nodeGroupY)
                {
                    if (sign.x == 1)
                        innerGroupX = Math.Min(innerGroupX, node.posGroup.x);
                    else if (sign.x == -1)
                        innerGroupX = Math.Max(innerGroupX, node.posGroup.x);
                    if (sign.y == 1)
                        oldPosition = Math.Min(oldPosition, node.pos.y);
                    else if (sign.y == -1)
                        oldPosition = Math.Max(oldPosition, node.pos.y);
                }

                List<HENode> nodeGroupY2 = nodesList.FindAll(n =>
                    n.posGroup.y == i - sign.y &&
                    FromStartNode(n, quadrant) &&
                    (sign.x) * n.posGroup.x > (sign.x) * innerGroupX - (int)(posGroupYCount / dividePosGroupCount) &&
                    !(n.hideIfNoParts && n.PartsInTotal < 1));
                //Math.Abs(innerGroupX - n.posGroup.x) < (int)(posGroupXCount / dividePosGroupCount) &&
                HETTNSettings.Log2("\n Nearest Unhidden Nodes in SECOND group {0}:", i - sign.y);
                foreach (HENode node in nodeGroupY2)
                {
                    HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
                }
                if (nodeGroupY2.Count == 0)
                {
                    List<HENode> nodeGroupY3 = nodesList.FindAll(n =>
                        n.posGroup.y == i - sign.y &&
                        FromStartNode(n, quadrant));
                    HETTNSettings.Log2("\n All Nodes in SECOND group {0}:", i - sign.y);
                    foreach (HENode node in nodeGroupY3)
                    {
                        HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
                    }
                    if (nodeGroupY3.Count == 0)
                    {
                        ReplaceGroupsY(nodesList, i, quadrant, 0);
                    }
                    else
                    {
                        List<float> positionsList = new List<float>();
                        for (int j = 0; j < nodeGroupY3.Count; j++)
                        {
                            positionsList.Add(nodeGroupY3[j].pos.y);
                        }
                        newPosition = GetAveragePos(positionsList);

                        List<HENode> nodeGroupY4 = new List<HENode>();
                        int thirdGroup = -1;
                        if (sign.y == 1)
                        {
                            for (int j = i - 2 * sign.y; j > startNode.posGroup.y; j--)
                            {
                                nodeGroupY4 = nodesList.FindAll(n =>
                                    n.posGroup.y == j &&
                                    FromStartNode(n, quadrant));
                                if (nodeGroupY4.Count > 0)
                                {
                                    thirdGroup = j;
                                    break;
                                }
                            }
                        }
                        else if (sign.y == -1)
                        {
                            for (int j = i - 2 * sign.y; j < startNode.posGroup.y; j++)
                            {
                                nodeGroupY4 = nodesList.FindAll(n =>
                                    n.posGroup.y == j &&
                                    FromStartNode(n, quadrant));
                                if (nodeGroupY4.Count > 0)
                                {
                                    thirdGroup = j;
                                    break;
                                }
                            }
                        }
                        HETTNSettings.Log2("\n All Nodes in THIRD group {0}:", thirdGroup);
                        foreach (HENode node in nodeGroupY4)
                        {
                            HETTNSettings.Log2("    {0} | {1} | {2}", node.title, node.pos, node.posGroup);
                        }
                        //List<HENode> nodeGroupY4 = nodesList.FindAll(n =>
                        //       n.posGroup.y == i - 2 * sign.y &&
                        //       FromStartNode(n, quadrant));
                        if (nodeGroupY4.Count > 0)
                        {
                            List<float> positionsList2 = new List<float>();
                            for (int j = 0; j < nodeGroupY4.Count; j++)
                            {
                                positionsList2.Add(nodeGroupY4[j].pos.y);
                            }
                            nextPosition = GetAveragePos(positionsList2);
                        }

                        diffPosition = newPosition - oldPosition;
                        diffPosition2 = Math.Min(Math.Abs(nextPosition - newPosition), Math.Abs(newPosition - startNode.pos.y));

                        if (sign.y == 1 && Math.Abs(diffPosition2) > 2 * posYTolerance)
                        {
                            for (int j = i; j < posGroupYCount; j++)
                            {
                                ReplaceGroupsY(nodesList, j, quadrant, diffPosition);
                            }
                        }
                        else if (sign.y == -1 && Math.Abs(diffPosition2) > 2 * posYTolerance)
                        {
                            for (int j = i; j > 0; j--)
                            {
                                ReplaceGroupsY(nodesList, j, quadrant, diffPosition);
                            }
                        }
                        else
                        {
                            ReplaceGroupsY(nodesList, i, quadrant, 0);
                        }
                    }
                }
            }
            return nodesList;
        }

        // ----------------------------------------------------
        // Replace/shift nodes to next group by a given amount.
        // ----------------------------------------------------
        public List<HENode> ReplaceGroupsX(List<HENode> nodesList, int i, int quadrant, float diffPosition)
        {
            //if (diffPosition != 0) ;
            HETTNSettings.Log2("    Shifting group {1} by {0}...", i, diffPosition);

            GetSigns sign = new GetSigns(quadrant);
            foreach (HENode node in nodesList.FindAll(n => (n.posGroup.x == i && FromStartNode(n, quadrant) && !(n.hideIfNoParts && n.PartsInTotal < 1))))
            {
                node.pos.x = node.pos.x + diffPosition;
                node.posGroup.x = i - sign.x;
            }
            return nodesList;
        }

        public List<HENode> ReplaceGroupsY(List<HENode> nodesList, int i, int quadrant, float diffPosition)
        {
            //if (diffPosition != 0) ;
            HETTNSettings.Log2("    Shifting group {1} by {0}...", i, diffPosition);

            GetSigns sign = new GetSigns(quadrant);
            foreach (HENode node in nodesList.FindAll(n => (n.posGroup.y == i && FromStartNode(n, quadrant) && !(n.hideIfNoParts && n.PartsInTotal < 1))))
            {
                node.pos.y = node.pos.y + diffPosition;
                node.posGroup.y = i - sign.y;
            }
            return nodesList;
        }

        // -----------------------------------
        // Get x,y signs for a given quadrant.
        // -----------------------------------
        public struct GetSigns
        {
            public int x;
            public int y;

            public GetSigns(int quadrant)
            {
                switch (quadrant)
                {
                    case 1:
                        this.x = 1; this.y = 1;
                        break;
                    case 2:
                        this.x = -1; this.y = 1;
                        break;
                    case 3:
                        this.x = -1; this.y = -1;
                        break;
                    case 4:
                        this.x = 1; this.y = -1;
                        break;
                    default:
                        this.x = 0; this.y = 0;
                        break;
                }
            }
        }

        // --------------------------------------------
        // Return true for nodes in the given quadrant.
        // --------------------------------------------
        public static bool FromStartNode(HENode node, int quadrant)
        {
            switch (quadrant)
            {
                case 1:
                    return node.pos.x > startNode.pos.x && node.pos.y > startNode.pos.y;
                case 2:
                    return node.pos.x < startNode.pos.x && node.pos.y > startNode.pos.y;
                case 3:
                    return node.pos.x < startNode.pos.x && node.pos.y < startNode.pos.y;
                case 4:
                    return node.pos.x > startNode.pos.x && node.pos.y < startNode.pos.y;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // Get the average of unique positions (within epsilon) in a list.
        // ---------------------------------------------------------------
        public float GetAveragePos(List<float> positions)
        {
            HETTNSettings.Log2("    Getting average position...");

            List<float> uniquePositions = new List<float>();
            foreach (float pos in positions)
            {
                bool addPos = true;
                foreach (float pos2 in uniquePositions)
                {
                    if (IsClose(pos, pos2, nodeSizeEpsilon))
                    {
                        addPos = false;
                        break;
                    }
                }
                if (addPos)
                {
                    uniquePositions.Add(pos);
                }
            }
            return uniquePositions.Average();
        }

        // -------------------------------------------------------------
        // Return mode of non-zero difference between adjacent elements.
        // -------------------------------------------------------------
        public static float ModeDiff(float[] a)
        {
            // Sort array and find difference between elements.
            Array.Sort(a);
            float[] a2 = new float[a.Length - 1];
            for (int i = 0; i < a2.Length; i++)
            {
                a2[i] = a[i + 1] - a[i];
            }

            // Take differences greater than epsilon value, and find value that repeats the most within tolerance.
            // Lastly check if final elements repeat the most.
            var a3 = Array.FindAll(a2, element => Math.Abs(element) > nodeSizeEpsilon);
            if (a3.Length == 1)
                return a3[0];

            int modeCount = 1;
            int modeCountMax = 1;
            int modeCountMaxIndex = 0;

            Array.Sort(a3);
            for (int i = 1; i < a3.Length; i++)
            {
                if (IsClose(a3[i], a3[i - 1], nodeSizeEpsilon))
                {
                    modeCount++;
                }
                else
                {
                    if (modeCount > modeCountMax)
                    {
                        modeCountMax = modeCount;
                        modeCountMaxIndex = i - 1;
                    }
                    modeCount = 1;
                }
            }
            if (modeCount > modeCountMax)
            {
                modeCountMaxIndex = a3.Length - 1;
            }
            return a3[modeCountMaxIndex];
        }
        #endregion


        // ------------------
        // Tolerance checker.
        // ------------------
        #region TOLERANCE CHECKER
        // Return true if two values are within tolerance of each other.
        public static bool IsClose(double a, double b, double posTolerance)
        {
            return Math.Abs(a - b) < posTolerance;
        }
        #endregion


        // ----------
        // Comparers.
        // ----------
        #region COMPARERS
        public static int CompareAbs(KeyValuePair<double, double> a, KeyValuePair<double, double> b)
        {
            return a.Key.CompareTo(b.Key);
        }

        public static int CompareSqrt(KeyValuePair<double, HENode.Parent> a, KeyValuePair<double, HENode.Parent> b)
        {
            return a.Key.CompareTo(b.Key);
        }

        public class IDComparer : IEqualityComparer<HENode.Parent>
        {
            public bool Equals(HENode.Parent a, HENode.Parent b)
            {
                return a.parentID == b.parentID;
            }

            public int GetHashCode(HENode.Parent a)
            {
                return a.parentID.GetHashCode();
            }
        }
        #endregion
    }
}
