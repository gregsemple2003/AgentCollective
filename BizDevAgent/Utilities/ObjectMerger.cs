using System;
using System.Collections;
using System.Reflection;

namespace BizDevAgent.Utilities
{
    public static class ObjectMerger
    {
        public static void Merge(object left, object right)
        {
            if (left == null || right == null || left.GetType() != right.GetType())
            {
                throw new ArgumentException("Both objects must be non-null and of the same type.");
            }

            Type type = left.GetType();

            // Merge fields
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                MergeFieldOrProperty(field.GetValue(left), field.GetValue(right), val => field.SetValue(right, val));
            }

            // Merge properties
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite && prop.GetIndexParameters().Length == 0) // Check for non-indexed properties
                {
                    MergeFieldOrProperty(prop.GetValue(left), prop.GetValue(right), val => prop.SetValue(right, val));
                }
            }
        }

        private static void MergeFieldOrProperty(object leftVal, object rightVal, Action<object> setRightValue)
        {
            if (IsScalarType(leftVal))
            {
                if (!IsDefaultValue(leftVal))
                {
                    setRightValue(leftVal);
                }
            }
            else if (leftVal is IList leftList && leftList.Count > 0)
            {
                setRightValue(leftVal);
            }
            else if (leftVal != null)
            {
                Merge(leftVal, rightVal); // Recursive call for subobjects
            }
        }

        private static bool IsScalarType(object obj)
        {
            if (obj == null) return false;
            Type type = obj.GetType();
            return type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(decimal);
        }

        private static bool IsDefaultValue(object obj)
        {
            if (obj == null) return true;
            Type type = obj.GetType();
            return obj.Equals(type.IsValueType ? Activator.CreateInstance(type) : null);
        }
    }
}
