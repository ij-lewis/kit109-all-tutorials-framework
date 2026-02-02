using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/*
namespace Unity.Tutorials.Core.Editor
{
    [CustomPropertyDrawer(typeof(TutorialParagraph.CriterionErrorPair))]
    class CriterionErrorPairDrawer : CollectionWrapperDrawer
    {
        const string k_errorTutorialTextPath = "errorTutorialText";
        const string k_CriterionPropertyPath = "Criterion";

        protected override void OnReorderableListCreated(ReorderableList list)
        {
            base.OnReorderableListCreated(list);
            list.onAddCallback = delegate(ReorderableList lst) {
                ++lst.serializedProperty.arraySize;
                lst.serializedProperty.serializedObject.ApplyModifiedProperties();
                var lastElement = lst.serializedProperty.GetArrayElementAtIndex(lst.serializedProperty.arraySize - 1);
                lastElement.FindPropertyRelative(k_errorTutorialTextPath).stringValue = "";
                lastElement.FindPropertyRelative(k_CriterionPropertyPath).objectReferenceValue = null;
                list.serializedProperty.serializedObject.ApplyModifiedProperties();
            };
        }
    }
}
*/