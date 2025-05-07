#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Elfenlabs.Editor
{
    // Here is where you create the custom PropertyDrawer. The magic happens in the
    // OnGUI method where we create a TextField in the inspector and set it's value
    // to the SerializedProperty's value as a long, read it back as a string and
    // try to parse it to a number again. If the parsing fails at any point, the
    // number is just set to 0.
    [CustomPropertyDrawer(typeof(HexIntAttribute))]
    public class HexIntDrawer : PropertyDrawer
    {
        public HexIntAttribute hexIntAttribute
        {
            get { return ((HexIntAttribute)attribute); }
        }

        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            string hexValue = EditorGUI.TextField(position, label,
                 property.longValue.ToString(hexIntAttribute.FormatString));

            long value = 0;

            if (hexValue.StartsWith("0x"))
            {
                try
                {
                    value = Convert.ToInt64(hexValue, 16);
                }
                catch (FormatException)
                {
                    value = 0;
                }
            }
            else
            {
                bool parsed = long.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber,
                                            null, out value);
                if (!parsed)
                {
                    value = 0;
                }
            }

            if (EditorGUI.EndChangeCheck())
                property.longValue = value;
        }
    }

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
}
#endif