using NUnit.Framework;
using Agent.Utilities; // Assuming ObjectMerger is in this namespace
using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Agent.Tests
{
    [TestFixture]
    public class ObjectMergerTests
    {
        public class SubObject
        {
            public double DoubleField;
            public decimal DecimalProperty { get; set; }
            // Add other scalar types as needed
        }

        public class TestObject
        {
            public int IntField;
            public string StringField { get; set; }
            public bool BoolField;
            public DateTime DateTimeField { get; set; }
            public SubObject SubObjectField { get; set; }
            public List<int> IntList { get; set; }
            public List<int> IntList2 { get; set; }
            public List<SubObject> SubObjectList { get; set; }

            // Add other scalar types as needed
        }

        [Test]
        public void Merge_WithSubObject_NonDefaultValuesShouldOverwrite()
        {
            var left = new TestObject
            {
                IntField = 10,
                StringField = "Left",
                BoolField = true,
                DateTimeField = new DateTime(2022, 1, 1),
                SubObjectField = new SubObject { DoubleField = 10.5, DecimalProperty = 100m },
                IntList = new List<int> { 1, 2, 3 },
                IntList2 = null,
                SubObjectList = new List<SubObject> { new SubObject { DoubleField = 1.1, DecimalProperty = 11m } }
            };

            var right = new TestObject
            {
                IntField = 0,
                StringField = null,
                BoolField = false,
                DateTimeField = DateTime.MinValue,
                SubObjectField = new SubObject { DoubleField = 0, DecimalProperty = 0m },
                IntList = new List<int> { 4, 5 },
                IntList2 = new List<int> { 6, 7 },
                SubObjectList = new List<SubObject> { new SubObject { DoubleField = 2.2, DecimalProperty = 22m } }
            };

            ObjectMerger.Merge(left, right);

            Assert.AreEqual(left.IntField, right.IntField);
            Assert.AreEqual(left.StringField, right.StringField);
            Assert.AreEqual(left.BoolField, right.BoolField);
            Assert.AreEqual(left.DateTimeField, right.DateTimeField);
            Assert.AreEqual(left.SubObjectField.DoubleField, right.SubObjectField.DoubleField);
            Assert.AreEqual(left.SubObjectField.DecimalProperty, right.SubObjectField.DecimalProperty);

            // Assertions for lists
            Assert.AreEqual(left.IntList, right.IntList);
            Assert.AreEqual(left.SubObjectList[0].DoubleField, right.SubObjectList[0].DoubleField);
            Assert.AreEqual(left.SubObjectList[0].DecimalProperty, right.SubObjectList[0].DecimalProperty);
            Assert.AreEqual(6, right.IntList2[0]);
            Assert.AreEqual(7, right.IntList2[1]);
        }

        [Test]
        public void Merge_WithSubObject_DefaultValuesShouldNotOverwrite()
        {
            var left = new TestObject
            {
                IntField = 0,
                StringField = null,
                BoolField = false,
                DateTimeField = DateTime.MinValue,
                SubObjectField = null,
                IntList = new List<int>(), // Empty list
                SubObjectList = new List<SubObject>() // Empty list
            };

            var right = new TestObject
            {
                IntField = 20,
                StringField = "Right",
                BoolField = true,
                DateTimeField = new DateTime(2023, 1, 1),
                SubObjectField = new SubObject { DoubleField = 5.5, DecimalProperty = 50m },
                IntList = new List<int> { 6, 7 },
                SubObjectList = new List<SubObject> { new SubObject { DoubleField = 3.3, DecimalProperty = 33m } }
            };

            ObjectMerger.Merge(left, right);

            Assert.AreEqual(20, right.IntField); // Should retain original value
            Assert.AreEqual("Right", right.StringField);
            Assert.AreEqual(true, right.BoolField);
            Assert.AreEqual(new DateTime(2023, 1, 1), right.DateTimeField);
            Assert.AreEqual(5.5, right.SubObjectField.DoubleField); // Should retain original value
            Assert.AreEqual(50m, right.SubObjectField.DecimalProperty); // Should retain original value

            // Assertions for lists
            Assert.AreEqual(2, right.IntList.Count); // Should retain original list
            Assert.AreEqual(1, right.SubObjectList.Count); // Should retain original list
            Assert.AreEqual(3.3, right.SubObjectList[0].DoubleField);
            Assert.AreEqual(33m, right.SubObjectList[0].DecimalProperty);
        }

        // Add more tests as necessary to cover all scenarios
    }
}