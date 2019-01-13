using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace HideEmptyTechTreeNodes
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class ChangeTechTreeVisuals : MonoBehaviour
    {
        // -----------------------------
        // Setup parameters and methods.
        // -----------------------------
        #region SETUP
        // Tech tree and settings.
        public RDTechTree rdTechTree;
        internal HETTNSettings hettnSettings;

        #endregion


        // -----------------------
        // Load the plugin events.
        // -----------------------
        #region LOADING
        // Unity method.
        private void Start()
        {
            HETTNSettings.Log2("Loading events for visual changes...");
            RDTechTree.OnTechTreeSpawn.Add(new EventData<RDTechTree>.OnEvent(this.onTechTreeSpawn));
            RDTechTree.OnTechTreeDespawn.Add(new EventData<RDTechTree>.OnEvent(this.onTechTreeDespawn));
            HETTNSettings.Log2("Finished loading events for visual changes.");
        }

        // Unity method.
        private void OnDestroy()
        {
            HETTNSettings.Log2("Destroying events for visual changes...");
            RDTechTree.OnTechTreeSpawn.Remove(new EventData<RDTechTree>.OnEvent(this.onTechTreeSpawn));
            RDTechTree.OnTechTreeDespawn.Remove(new EventData<RDTechTree>.OnEvent(this.onTechTreeDespawn));
            HETTNSettings.Log2("Finished destroying events for visual changes.");
        }

        // Event.
        private void onTechTreeSpawn(RDTechTree rdTechTree)
        {
            HETTNSettings.Log("Using tech tree path: {0}", HighLogic.CurrentGame.Parameters.Career.TechTreeUrl);
            Preload();
            HETTNSettings.Log2("Applying visual changes...");
            ChangeZoomScroll();
            ChangeViewable();
            HETTNSettings.Log2("Finished applying visual changes.");
        }

        // Event.
        private void onTechTreeDespawn(RDTechTree rdTechTree)
        {
            HETTNSettings.Log2("Despawning tech tree visual changes...");
            // Not sure if neccessary... or maybe do I need to delete tech line and arrow gameObjects as well??
            this.rdTechTree = rdTechTree;
            int count = this.rdTechTree.controller.nodes.Count;
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object.Destroy(this.rdTechTree.controller.nodes[count].gameObject);
            }
            this.rdTechTree.controller.nodes.Clear();
            this.rdTechTree = null;
            HETTNSettings.Log2("Finished despawning tech tree visual changes.");
        }
        #endregion


        // ---------------------------------------------------------
        // Preload the instances for RDNode and RDTech. Add listener
        // for Hide Unresearchable Nodes option. (Testing shows a
        // ".Remove" isn't necessary).
        // ---------------------------------------------------------
        #region PRELOAD
        public void Preload()
        {
            // Apply current settings.
            this.hettnSettings = new HETTNSettings();

            // Initilize and get current RDTechTree stuff so we can change visual settings.
            this.rdTechTree = new RDTechTree();
            this.rdTechTree = AssetBase.RnDTechTree;
            //this.rdTechTree.controller.actionButton.onClickState.RemoveAllListeners();
            this.rdTechTree.controller = RDController.Instance;
            this.rdTechTree.controller.actionButton.onClickState.AddListener(new UnityAction<string>(this.ActionButtonClick));
            foreach (RDNode node in this.rdTechTree.controller.nodes)
            {
                node.tech.Start();
                node.Start();
                node.UpdateGraphics();
            }
        }
        #endregion


        // ---------------------------------------------------------
        // Hide nodes whose parents are not yet researched.
        // I disable some graphic gameObjects, so it's feels kind of
        // hackey. But the normal Squad methods (SetViewable, etc.)
        // don't seem to work.
        // ---------------------------------------------------------
        #region CHANGE VIEWABLE
        public void ChangeViewable()
        {
            // Option is disabled. Return right away.
            if (hettnSettings.forceHideUnresearchable == false)
            {
                HETTNSettings.Log2("Hide Unresearchable Nodes option is disabled.");
                return;
            }
            HETTNSettings.Log2("Attempting to hide any unresearchable nodes...");

            // Hide unresearchable nodes.
            foreach (RDNode node in this.rdTechTree.controller.nodes)
            {
                HETTNSettings.Log2("Node: \"{0}\" | SciCost: {1}({2}), State: {3}, Available: {4}, IsResearched: {5}, IsGraphicActive: {6}.",
                        node.tech.techID,
                        node.tech.scienceCost,
                        this.rdTechTree.controller.ScienceCap,
                        node.state,
                        node.tech.state,
                        node.IsResearched,
                        node.graphics.isActiveAndEnabled);

                // Keep node active if researched or researchable, but remove their tech lines and arrows to unresearched parents.
                // Note: using "if (node.IsResearched == true)" in addition would create less warnings below. For some reason 
                // "node.state" isn't always reliable.
                if (new[] { RDNode.State.RESEARCHED, RDNode.State.RESEARCHABLE }.Contains(node.state))
                {
                    foreach (RDNode.Parent parent in node.parents)
                    {
                        if (!parent.parent.node.IsResearched)
                        {
                            parent.line.active = false;
                            parent.arrowHead.gameObject.SetActive(false);
                        }
                    }
                    continue;
                }
                // Remove parent tech lines and arrows, but also repair nodes that are below science cap but have 
                // visible parents. Also fix an occasional problem where nodes that should be visible are not 
                // ("node.state" problem). Keep both these kinds of nodes active.
                else
                {
                    bool flagUnhide = false;
                    List<RDNode.Parent> list = new List<RDNode.Parent>();
                    foreach (RDNode.Parent parent in node.parents)
                    {
                        parent.line.active = false;
                        parent.arrowHead.gameObject.SetActive(false);
                        if (parent.parent.node.IsResearched)
                        {
                            list.Add(parent);
                            flagUnhide = true;
                        }
                    }
                    if (flagUnhide)
                    {
                        foreach (RDNode.Parent parent in list)
                        {
                            parent.line.active = true;
                            parent.arrowHead.gameObject.SetActive(true);
                            parent.parent.node.UpdateGraphics();
                        }
                        if (node.tech.scienceCost < this.rdTechTree.controller.ScienceCap || node.state != RDNode.State.HIDDEN)
                        {
                            HETTNSettings.LogWarning("Something is wrong with node \"{0}\", it should be viewable. Forcing node graphics to active. Ignore message if you can see the node.",
                                node.tech.title,
                                node.IsResearched);
                        }
                        continue;
                    }
                }
                // Deactive the remaining node graphics.
                node.graphics.gameObject.SetActive(false);
            }
            HETTNSettings.Log2("Finished hiding unresearchable nodes.");
        }

        // Unhide children when node is researched.
        public void ActionButtonClick(string state)
        {
            if (hettnSettings.forceHideUnresearchable == false)
                return;
            if (state == "research")
            {
                if (this.rdTechTree.controller.node_selected.IsResearched)
                {
                    HETTNSettings.Log2("Attempting to unhide new researchable nodes...");
                    int count = this.rdTechTree.controller.node_selected.children.Count;
                    for (int i = 0; i < count; i++)
                    {
                        this.rdTechTree.controller.node_selected.children[i].graphics.gameObject.SetActive(true);
                        this.rdTechTree.controller.node_selected.children[i].UpdateGraphics();
                        foreach (RDNode.Parent parent in this.rdTechTree.controller.node_selected.children[i].parents)
                        {
                            if (parent.parent.node.tech.techID == this.rdTechTree.controller.node_selected.tech.techID)
                            {
                                parent.line.active = true;
                                parent.arrowHead.gameObject.SetActive(true);
                                parent.parent.node.UpdateGraphics();
                            }
                        }
                        HETTNSettings.Log("Node: \"{0}\" | Unlocked child: \"{1}\".",
                            this.rdTechTree.controller.node_selected.tech.techID,
                            this.rdTechTree.controller.node_selected.children[i].tech.techID);
                    }
                    HETTNSettings.Log2("Successfully unhid new researchable nodes.");
                }
                else
                {
                    HETTNSettings.Log2("Not enough science. Not showing children.");
                }
            }
        }
        #endregion


        // ---------------------------------------------------
        // Change the max, min, and speed zoom scroll settings
        // ---------------------------------------------------
        #region ZOOM SCROLL CHANGES
        public void ChangeZoomScroll()
        {
            HETTNSettings.Log2("Attempting to change the tech tree zoom settings...");
            try
            {
                RDGridArea gridChanges = this.rdTechTree.controller.gridArea;

                gridChanges.zoomMax = hettnSettings.zoomMax;
                gridChanges.zoomMin = hettnSettings.zoomMin;
                gridChanges.zoomSpeed = hettnSettings.zoomSpeed;

                HETTNSettings.Log2("The new zoom settings are - Max: {0:P0}, Min: {1:P0}, Speed: {2:P0}.",
                    gridChanges.zoomMax,
                    gridChanges.zoomMin,
                    gridChanges.zoomSpeed);
            }
            catch (Exception e)
            {
                HETTNSettings.LogError("Error changing zoom settings. Using defaults.\n" + e);
                return;
            }
            HETTNSettings.Log2("Successfully changed the tech tree zoom settings.");
        }
        #endregion
    }
}
