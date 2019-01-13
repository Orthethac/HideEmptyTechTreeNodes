using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideEmptyTechTreeNodes
{
    // This class  is used to hide empty RDNodes. Very similar to stock RDNode and RDTech
    // classes, but this class mostly just does stuff with config files, plus a bit more.
    public class HENode
    {
        public string techID;

        public string title;

        public string description;

        public int scienceCost;

        public bool hideIfNoParts;

        public string nodeName;

        public bool AnyParentToUnlock;

        public string iconRef;

        public Vector3 pos;

        public PosGroup posGroup = new PosGroup(0, 0);

        public float scale;

        public List<AvailablePart> partsAssigned;

        public int PartsInTotal;

        public Parent[] parents;

        public List<string> children = new List<string>();

        // ETT fix 1 of 5.
        public Unlocks unlocks;

        // Get all parts assigned to RDNode.
        public void GetPartsAssigned()
        {
            List<AvailablePart> partsList = null;
            if (PartLoader.Instance)
            {
                partsList = PartLoader.LoadedPartsList;
            }
            if (partsList == null)
            {
                HETTNSettings.LogWarning("No loaded part lists available!");
                return;
            }
            this.partsAssigned = new List<AvailablePart>();
            int count = partsList.Count;
            for (int i = 0; i < count; i++)
            {
                if (partsList[i].TechRequired == this.techID)
                {
                    this.partsAssigned.Add(partsList[i]);
                }
            }
        }

        // Load all RDNode keys from config node.
        public void Load(ConfigNode node)
        {
            // RDNode keys.
            if (node.HasValue("id"))
                this.techID = node.GetValue("id");

            if (node.HasValue("title"))
                this.title = node.GetValue("title");

            if (node.HasValue("description"))
                this.description = node.GetValue("description");

            if (node.HasValue("cost"))
                this.scienceCost = int.Parse(node.GetValue("cost"));

            if (node.HasValue("hideEmpty"))
                this.hideIfNoParts = bool.Parse(node.GetValue("hideEmpty"));

            if (node.HasValue("nodeName"))
                this.nodeName = node.GetValue("nodeName");
            if (String.IsNullOrEmpty(this.nodeName))
                this.nodeName = this.techID;

            if (node.HasValue("anyToUnlock"))
                this.AnyParentToUnlock = bool.Parse(node.GetValue("anyToUnlock"));

            if (node.HasValue("icon"))
                this.iconRef = node.GetValue("icon");

            if (node.HasValue("pos"))
                this.pos = KSPUtil.ParseVector3(node.GetValue("pos"));

            if (node.HasValue("scale"))
                this.scale = float.Parse(node.GetValue("scale"));

            this.GetPartsAssigned();
            if (this.partsAssigned != null)
                this.PartsInTotal = this.partsAssigned.Count;
            else
                this.PartsInTotal = 0;

            // ETT fix 2 of 5.
            this.LoadUnlocks(node);
        }

        // Load parents.
        public void LoadLinks(ConfigNode node, List<HENode> list)
        {
            List<HENode.Parent> parentsList = new List<HENode.Parent>();
            ConfigNode[] nodes2 = node.GetNodes("Parent");
            int count = nodes2.Length;
            for (int i = 0; i < count; i++)
            {
                parentsList.Add(new HENode.Parent(nodes2[i], list));
            }
            this.parents = parentsList.ToArray();
        }

        // ETT fix 3 of 5.
        // Load part unlocks.
        // *** move to Unlocks class?
        public void LoadUnlocks(ConfigNode node)
        {
            if (node.HasNode("Unlocks"))
            {
                ConfigNode node2 = node.GetNode("Unlocks");
                this.unlocks = new HENode.Unlocks(node2);
            }
        }

        // Save RDNode and parents.
        public void Save(ConfigNode node)
        {
            if (node == null)
                HETTNSettings.Log("test11");
            node.AddValue("id", this.techID);
            node.AddValue("title", this.title);
            node.AddValue("description", this.description);
            node.AddValue("cost", this.scienceCost);
            node.AddValue("hideEmpty", this.hideIfNoParts);
            node.AddValue("nodeName", this.nodeName);
            node.AddValue("anyToUnlock", this.AnyParentToUnlock);
            node.AddValue("icon", this.iconRef);
            node.AddValue("pos", KSPUtil.WriteVector(this.pos));
            node.AddValue("scale", this.scale);
            int count = this.parents.Length;
            for (int i = 0; i < count; i++)
            {
                this.parents[i].Save(node.AddNode("Parent"));
            }
            // ETT fix 4 of 5.
            if (this.unlocks != null)
            {
                this.unlocks.Save(node.AddNode("Unlocks"));
            }
        }

        // -------
        // Parents
        // -------
        // RD node-parent anchors.
        public enum Anchor
        {
            TOP = 1,
            BOTTOM,
            RIGHT,
            LEFT
        }

        // The "Parent" nodes of RDNodes.
        public class Parent
        {
            public string parentID = string.Empty;
            public HENode.Anchor lineFrom = HENode.Anchor.RIGHT;
            public HENode.Anchor lineTo = HENode.Anchor.LEFT;
            public bool hideIfNoParts = true;
            public int PartsInTotal = 0;

            public Parent(ConfigNode node)
            {
                if (node.HasValue("parentID"))
                    this.parentID = node.GetValue("parentID");

                if (node.HasValue("lineFrom"))
                    this.lineFrom = (HENode.Anchor)((int)Enum.Parse(typeof(HENode.Anchor), node.GetValue("lineFrom")));

                if (node.HasValue("lineTo"))
                    this.lineTo = (HENode.Anchor)((int)Enum.Parse(typeof(HENode.Anchor), node.GetValue("lineTo")));
            }

            public Parent(ConfigNode node, List<HENode> list)
            {
                this.Load(node, list);
            }

            public HENode FindNodeByID(string techID, List<HENode> heNodesList)
            {
                int count = heNodesList.Count;
                for (int i = 0; i < count; i++)
                {
                    if (heNodesList[i].techID == techID)
                    {
                        return heNodesList[i];
                    }
                }
                return null;
            }

            public void Load(ConfigNode node, List<HENode> list)
            {
                if (node.HasValue("parentID"))
                    this.parentID = node.GetValue("parentID");

                if (node.HasValue("lineFrom"))
                    this.lineFrom = (HENode.Anchor)((int)Enum.Parse(typeof(HENode.Anchor), node.GetValue("lineFrom")));

                if (node.HasValue("lineTo"))
                    this.lineTo = (HENode.Anchor)((int)Enum.Parse(typeof(HENode.Anchor), node.GetValue("lineTo")));

                HENode heNode = this.FindNodeByID(parentID, list);
                if (heNode != null)
                {
                    this.hideIfNoParts = heNode.hideIfNoParts;
                    this.PartsInTotal = heNode.PartsInTotal;
                }
            }

            public void Save(ConfigNode node)
            {
                node.AddValue("parentID", this.parentID);
                node.AddValue("lineFrom", this.lineFrom);
                node.AddValue("lineTo", this.lineTo);
            }
        }

        // ---------------
        // ETT fix 5 of 5.
        // Unlocks
        // ---------------
        // The "Unlocks" nodes of RDNodes.
        public class Unlocks
        {
            public string[] ettPartsList;

            public Unlocks(ConfigNode node)
            {
                if (node.HasValues("part"))
                    this.ettPartsList = node.GetValues("part");
                else
                    this.ettPartsList = new string[0];
            }

            public void Save(ConfigNode node)
            {
                for (int i = 0; i < this.ettPartsList.Length; i++)
                {
                    node.AddValue("part", this.ettPartsList[i]);
                }
            }
        }

        public class PosGroup
        {
            public int x = 0;
            public int y = 0;

            public PosGroup(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public override string ToString()
            {
                return "(" + this.x + ", " + this.y + ")";//base.ToString();
            }
        }
    }
}