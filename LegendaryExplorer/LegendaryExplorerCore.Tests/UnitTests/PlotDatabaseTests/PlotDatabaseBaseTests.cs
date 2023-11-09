using System;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests.UnitTests.PlotDatabaseTests
{
    /// <summary>
    /// An implementation of PlotDatabaseBase used for testing the base class
    /// </summary>
    public class FakePDB : PlotDatabaseBase
    {
        public override bool IsBioware => false;

        public void SetRoot(PlotElement r) => Root = r;
    }
    
    [TestClass]
    public class PlotDatabaseBaseTests
    {
        private PlotDatabaseBase _plotDb;
        private PlotElement _category;
        private PlotBool _bool;
        private PlotConditional _conditional;
        private PlotElement _integer;
        private PlotElement _float;
        private PlotTransition _transition;

        [TestInitialize]
        public void Setup()
        {
            var pdb = new FakePDB();
            var root = new PlotElement(-1, 1, "Root", PlotElementType.Region, null);
            pdb.SetRoot(root);
            pdb.Organizational.Add(1, root);

            _plotDb = pdb;
            
            
            _category = new PlotElement(0, 2, "Category", PlotElementType.Category, null);
            _bool = new PlotBool(1, 3, "Bool", PlotElementType.State, null);
            _conditional = new PlotConditional(1, 4, "Conditional", PlotElementType.Conditional, _bool);
            _integer = new PlotElement(2, 5, "Integer", PlotElementType.Integer, null);
            _float = new PlotElement(3, 6, "Float", PlotElementType.Float, null);
            _transition = new PlotTransition(4, 7, "Transition", PlotElementType.Transition, null);
        }

        private void AddAllTestElementsToDb()
        {
            _plotDb.AddElement(_category, _plotDb.Root);
            _plotDb.AddElement(_bool, _plotDb.Root);
            _plotDb.AddElement(_conditional, _bool);
            _plotDb.AddElement(_float, _bool);
            _plotDb.AddElement(_transition, _bool);
            _plotDb.AddElement(_integer, _bool);
        }


        [TestMethod]
        public void AddElement_ElementsGoInCorrectCategories()
        {
            AddAllTestElementsToDb();
            
            Assert.IsTrue(_category.Parent == _plotDb.Root);
            Assert.IsTrue(_plotDb.Organizational.ContainsValue(_category));
            Assert.IsTrue(_plotDb.Organizational.ContainsKey(_category.ElementId));

            Assert.IsTrue(_plotDb.Bools.ContainsValue(_bool));
            Assert.IsTrue(_plotDb.Bools.ContainsKey(_bool.PlotId));
            
            Assert.IsTrue(_plotDb.Conditionals.ContainsValue(_conditional));
            Assert.IsTrue(_plotDb.Conditionals.ContainsKey(_conditional.PlotId));

            Assert.IsTrue(_plotDb.Ints.ContainsValue(_integer));
            Assert.IsTrue(_plotDb.Ints.ContainsKey(_integer.PlotId));

            Assert.IsTrue(_plotDb.Floats.ContainsValue(_float));
            Assert.IsTrue(_plotDb.Floats.ContainsKey(_float.PlotId));

            Assert.IsTrue(_plotDb.Transitions.ContainsValue(_transition));
            Assert.IsTrue(_plotDb.Transitions.ContainsKey(_transition.PlotId));
        }

        [TestMethod]
        public void AddElement_ElementWithNoParent_ThrowsException()
        {
            Assert.ThrowsException<Exception>(() => _plotDb.AddElement(_bool, null));
        }
        
        [TestMethod]
        public void AddElement_NullParentParameter_UsesExistingParent()
        {
            _plotDb.AddElement(_bool, _plotDb.Root);
            _plotDb.AddElement(_conditional, null);
            
            Assert.AreEqual(_bool, _conditional.Parent);
            Assert.IsTrue(_plotDb.Conditionals.ContainsValue(_conditional));
            
        }

        [TestMethod]
        public void RemoveElement_CannotRemoveRoot()
        {
            Assert.ThrowsException<ArgumentException>(() => _plotDb.RemoveElement(_plotDb.Root)); // Cannot remove root
        }

        [TestMethod]
        public void RemoveElement_RemovesFromDatabase()
        {
            _plotDb.AddElement(_category, _plotDb.Root);
            
            _plotDb.RemoveElement(_category);
            Assert.IsFalse(_plotDb.Organizational.ContainsKey(_category.RelevantId));
            Assert.IsFalse(_plotDb.Organizational.ContainsValue(_category));
        }

        [TestMethod]
        public void RemoveElement_RemoveAllChildren_RemovesFromCorrectCategories()
        {
            AddAllTestElementsToDb();
            
            Assert.ThrowsException<ArgumentException>(() => _plotDb.RemoveElement(_bool, removeAllChildren: false)); // If element has children, error is thrown by default
            
            _plotDb.RemoveElement(_bool, removeAllChildren:true);
            Assert.IsFalse(_plotDb.Bools.ContainsValue(_bool));
            Assert.IsFalse(_plotDb.Conditionals.ContainsValue(_conditional));
            Assert.IsFalse(_plotDb.Ints.ContainsValue(_integer));
            Assert.IsFalse(_plotDb.Floats.ContainsValue(_float));
            Assert.IsFalse(_plotDb.Transitions.ContainsValue(_transition));
        }

        [TestMethod]
        public void GetNextElementId_ProvidesNextAvailableId()
        {
            Assert.AreEqual(2, _plotDb.GetNextElementId());
            
            AddAllTestElementsToDb();
            Assert.AreEqual(8, _plotDb.GetNextElementId());
        }

        [TestMethod]
        public void GetMasterDictionary_ReturnsAllElements()
        {
            AddAllTestElementsToDb();
            var elements = new [] { _category, _bool, _conditional, _float, _transition, _integer };

            var masterDictionary = _plotDb.GetMasterDictionary();
            foreach (var e in elements)
            {
                Assert.IsTrue(masterDictionary.ContainsKey(e.ElementId));
                Assert.AreEqual(e, masterDictionary[e.ElementId]);
            }

        }

        [TestMethod]
        public void GetMasterDictionary_DuplicateElementIds_ReturnsEmpty()
        {
            var dup = new PlotBool(16, 3, "BoolDuplicate", PlotElementType.State, null);
            _plotDb.AddElement(_bool, _plotDb.Root);
            _plotDb.AddElement(dup, _plotDb.Root);

            var masterDictionary = _plotDb.GetMasterDictionary();
            Assert.IsNotNull(masterDictionary);
            Assert.AreEqual(0, masterDictionary.Count);
        }

        [TestMethod]
        public void GetElementById_ReturnsCorrectElements()
        {
            AddAllTestElementsToDb();
            Assert.AreEqual(_bool, _plotDb.GetElementById(_bool.ElementId));
            Assert.AreEqual(_transition, _plotDb.GetElementById(_transition.ElementId));
            
            Assert.IsNull(_plotDb.GetElementById(60));
        }
    }
}