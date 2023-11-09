using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests.UnitTests.PlotDatabaseTests
{
    [TestClass]
    public class PlotElementTests
    {
        private PlotElement parent;
        private PlotElement child;
        private PlotElement element3;

        [TestInitialize]
        public void InitializePlotElementTestData()
        {
            parent = new PlotElement()
            {
                PlotId = 0,
                ElementId = 1,
                Label = "parent",
                Type = PlotElementType.None
            };
            child = new PlotElement()
            {
                PlotId = 10,
                ElementId = 2,
                Label = "child",
                Type = PlotElementType.State
            };
            element3 = new PlotElement()
            {
                PlotId = 15,
                ElementId = 3,
                Label = "element3",
                Type = PlotElementType.Conditional
            };
        }

        [TestMethod]
        public void NewElement_WithGivenParent_AssignsParent()
        {
            var newE = new PlotElement(20, 4, "newE", PlotElementType.Float, parent, new List<PlotElement>() { child });
            
            Assert.IsTrue(parent.Children.Contains(newE));
            Assert.AreEqual(parent.ElementId, newE.ParentElementId);
            Assert.AreEqual(parent, newE.Parent);
            
            // declaring children doesn't properly assign parent to children
            Assert.AreNotEqual(newE.ElementId, child.ParentElementId);
        }

        [TestMethod]
        public void AssignParent_AssignsBothParentAndChild()
        {
            child.AssignParent(parent);
            Assert.IsTrue(parent.Children.Contains(child));
            Assert.AreEqual(parent.ElementId, child.ParentElementId);
            Assert.AreEqual(parent, child.Parent);
        }

        [TestMethod]
        public void AssignParent_WithExistingParent_RemovesExistingParent()
        {
            child.AssignParent(parent);
            child.AssignParent(element3);
            Assert.AreNotEqual(parent, child.Parent);
            Assert.AreNotEqual(parent.ElementId, child.ParentElementId);
            Assert.IsFalse(parent.Children.Contains(child));
        }
        
        [TestMethod]
        public void AssignParent_NullParent_RemovesExistingParent()
        {
            child.AssignParent(parent);
            child.AssignParent(null);
            Assert.IsNull(child.Parent);
            Assert.IsFalse(parent.Children.Contains(child));
            Assert.AreEqual(-1, child.ParentElementId);
        }

        [TestMethod]
        public void RemoveFromParent_RemovesFromBothParentAndChild()
        {
            // Test parent removal
            child.AssignParent(parent);
            var result = child.RemoveFromParent();
            Assert.IsTrue(result);
            Assert.IsNull(child.Parent);
            Assert.IsFalse(parent.Children.Contains(child));
            Assert.AreEqual(-1, child.ParentElementId);
        }

        [TestMethod]
        public void RemoveFromParent_WithNoParent_ReturnsFalse()
        {
            var result = child.RemoveFromParent();
            Assert.IsFalse(result);
        }
        
        [TestMethod]
        public void Path_FormatsStringCorrectlyWithParents()
        {
            Assert.AreEqual("child", child.Path);
            child.AssignParent(parent);
            Assert.AreEqual("parent.child", child.Path);
            parent.AssignParent(element3);
            Assert.AreEqual("element3.parent.child", child.Path);
            Assert.AreEqual("element3.parent", parent.Path);
        }

        [TestMethod]
        public void SetElementId_AffectsChildren()
        {
            child.AssignParent(parent);
            parent.SetElementId(50);
            Assert.AreEqual(50, parent.ElementId);
            Assert.AreEqual(50, child.ParentElementId);
        }

        [TestMethod]
        public void Test_IsAGameState_RelevantId()
        {
            Assert.IsTrue(child.IsAGameState);
            Assert.IsFalse(parent.IsAGameState);
            Assert.IsTrue(element3.IsAGameState);
            
            Assert.AreEqual(child.PlotId, child.RelevantId);
            Assert.AreEqual(parent.ElementId, parent.RelevantId);
            Assert.AreEqual(element3.PlotId, element3.RelevantId);
        }
    }
}