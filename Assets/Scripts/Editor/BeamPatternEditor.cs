using UnityEditor;

[CustomEditor(typeof(BeamPattern))]
public class BeamPatternEditor : Editor
{
    // Hide inherited modes that BeamPattern does not use.
    private static readonly string[] Excluded =
    {
        "m_Script",

        // Sine
        "enableSineWave",
        "sineAmplitude",
        "sineFrequency",

        // Cosine
        "enableCosineWave",
        "cosineAmplitude",
        "cosineFrequency",

        // Spiral
        "enableSpiral",
        "spiralSpeed",
        "spiralClockwise",

        // Homing
        "enableHoming",
        "maxStops",
        "stopDuration",
        "initialMovementTime",
        "curveDuration",
        "homingStyleAllPhases"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, Excluded);
        serializedObject.ApplyModifiedProperties();
    }
}
