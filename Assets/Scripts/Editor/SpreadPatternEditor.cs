using UnityEditor;

[CustomEditor(typeof(SpreadPattern))]
public class SpreadPatternEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Hide inherited "numberOfBulletsPerArray" only for SpreadPattern
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "numberOfBulletsPerArray",
            "individualArraySpread"
        );

        EditorGUILayout.HelpBox(
            "SpreadPattern uses Bullet Count for bullets-per-array.",
            MessageType.Info
        );

        serializedObject.ApplyModifiedProperties();
    }
}
