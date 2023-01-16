using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Infohazard.Core.Addressables.Editor {
    [CustomPropertyDrawer(typeof(AddressableSpawnRefBase), true)]
    public class AddressableSpawnRefDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty childProp = property.FindPropertyRelative(AddressableSpawnRefBase.FieldNames.AssetReference);
            SerializedProperty guidProp = childProp.FindPropertyRelative("m_AssetGUID");

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, childProp, label);
            if (!EditorGUI.EndChangeCheck()) {
                return;
            }
            
            property.serializedObject.Update();
            if (string.IsNullOrEmpty(guidProp.stringValue)) return;

            Type curType = fieldInfo.FieldType;

            while (curType.BaseType != typeof(AddressableSpawnRefBase)) {
                curType = curType.BaseType;

                if (curType == null) {
                    throw new Exception($"Invalid type {fieldInfo.FieldType}.");
                }
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guidProp.stringValue));
            
            if (!prefab || !prefab.TryGetComponent(out Spawnable _)) {
                EditorUtility.DisplayDialog("Invalid Prefab",
                                            $"Prefab {prefab} does not contain a {nameof(Spawnable)} component.",
                                            "OK");
                guidProp.stringValue = null;
                return;
            }

            Type genericArg = curType.GetGenericArguments()[0];

            if (typeof(Component).IsAssignableFrom(genericArg) &&
                !prefab.TryGetComponent(genericArg, out _)) {
                EditorUtility.DisplayDialog("Invalid Prefab",
                                            $"Prefab {prefab} does not contain a {genericArg.Name} component.",
                                            "OK");
                guidProp.stringValue = null;
            }
        }
    }
}