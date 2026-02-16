#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BulletPatternLabWindow : EditorWindow
{
    private BulletPatternBase[] patterns = Array.Empty<BulletPatternBase>();
    private string[] patternNames = Array.Empty<string>();
    private int selectedIndex = 0;

    private Editor patternEditor;
    private Vector2 scroll;

    // Test controls
    private Transform testFirePoint;
    private Transform testTarget;

    private bool autoRevertAfterTest = true;
    private string snapshotJson; // used to undo runtime mutations (spin/fireAngle changes, etc.)

    [MenuItem("Tools/Bullet Pattern Lab")]
    public static void Open() => GetWindow<BulletPatternLabWindow>("Bullet Pattern Lab");

    private void OnEnable()
    {
        RefreshList();
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
        SafeDestroyEditor(patternEditor);
        patternEditor = null;
    }

    private void RefreshList()
    {
        // Robust: find any ScriptableObject that inherits BulletPatternBase
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
        patterns = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g)))
            .OfType<BulletPatternBase>()
            .OrderBy(p => p.name)
            .ToArray();

        patternNames = patterns.Select(p => p.name).ToArray();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, patterns.Length - 1));
        RebuildEditor();
    }

    private BulletPatternBase Selected => (patterns != null && patterns.Length > 0 && selectedIndex >= 0 && selectedIndex < patterns.Length)
        ? patterns[selectedIndex]
        : null;

    private void RebuildEditor()
    {
        SafeDestroyEditor(patternEditor);
        patternEditor = null;

        var p = Selected;
        if (p != null)
            patternEditor = Editor.CreateEditor(p);
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshList();

            GUILayout.FlexibleSpace();

            autoRevertAfterTest = GUILayout.Toggle(autoRevertAfterTest, "Auto Revert After Test", GUILayout.Width(170));
        }

        EditorGUILayout.Space(6);

        if (patterns.Length == 0)
        {
            EditorGUILayout.HelpBox("No BulletPatternBase assets found.", MessageType.Info);
            return;
        }

        int newIndex = EditorGUILayout.Popup("Pattern", selectedIndex, patternNames);
        if (newIndex != selectedIndex)
        {
            selectedIndex = newIndex;
            RebuildEditor();
            snapshotJson = null;
        }

        var pattern = Selected;
        if (pattern == null) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping", GUILayout.Width(60)))
                EditorGUIUtility.PingObject(pattern);

            if (GUILayout.Button("Select Asset", GUILayout.Width(95)))
                Selection.activeObject = pattern;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save Preset...", GUILayout.Width(120)))
                SavePreset(pattern);

            if (GUILayout.Button("Load Preset...", GUILayout.Width(120)))
                LoadPreset(pattern);
        }

        EditorGUILayout.Space(8);

        DrawTestArea(pattern);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Pattern Inspector", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (patternEditor == null) RebuildEditor();
        patternEditor?.OnInspectorGUI();
        EditorGUILayout.EndScrollView();

        // In edit mode, allow saving changes to the asset
        if (!Application.isPlaying && GUI.changed)
        {
            EditorUtility.SetDirty(pattern);
        }
    }

    private void DrawTestArea(BulletPatternBase pattern)
    {
        EditorGUILayout.LabelField("Test (Play Mode)", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            testFirePoint = (Transform)EditorGUILayout.ObjectField("Fire Point", testFirePoint, typeof(Transform), true);
            testTarget = (Transform)EditorGUILayout.ObjectField("Target", testTarget, typeof(Transform), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = Application.isPlaying;

                if (GUILayout.Button("Snapshot"))
                {
                    snapshotJson = EditorJsonUtility.ToJson(pattern, true);
                    Debug.Log($"[PatternLab] Snapshot captured for {pattern.name}", pattern);
                }

                if (GUILayout.Button("Revert Snapshot"))
                {
                    RevertSnapshot(pattern);
                }

                GUI.enabled = Application.isPlaying && testFirePoint != null;

                if (GUILayout.Button("Fire Once"))
                {
                    // Capture snapshot if we want to auto-revert
                    if (autoRevertAfterTest && string.IsNullOrEmpty(snapshotJson))
                        snapshotJson = EditorJsonUtility.ToJson(pattern, true);

                    pattern.Fire(testFirePoint, testTarget);

                    if (autoRevertAfterTest)
                        RevertSnapshot(pattern);
                }

                GUI.enabled = true;
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode to test-fire patterns.", MessageType.Info);

            if (Application.isPlaying && testFirePoint == null)
                EditorGUILayout.HelpBox("Assign a Fire Point Transform (usually the enemy/spawner transform).", MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Note: your current patterns mutate asset fields like fireAngle/currentSpinSpeed while firing. " +
                "Use Snapshot/Revert (or Auto Revert) to avoid permanently changing the asset during tests.",
                MessageType.None
            );
        }
    }

    private void RevertSnapshot(BulletPatternBase pattern)
    {
        if (string.IsNullOrEmpty(snapshotJson)) return;
        EditorJsonUtility.FromJsonOverwrite(snapshotJson, pattern);
        EditorUtility.SetDirty(pattern);
        snapshotJson = null;
        Debug.Log($"[PatternLab] Snapshot reverted for {pattern.name}", pattern);
    }

    private void SavePreset(BulletPatternBase pattern)
    {
        string path = EditorUtility.SaveFilePanel(
            "Save Bullet Pattern Preset",
            Application.dataPath,
            $"{pattern.name}_pattern_preset.json",
            "json"
        );
        if (string.IsNullOrEmpty(path)) return;

        string json = EditorJsonUtility.ToJson(pattern, true);
        File.WriteAllText(path, json);
        Debug.Log($"[PatternLab] Preset saved: {path}", pattern);
    }

    private void LoadPreset(BulletPatternBase pattern)
    {
        string path = EditorUtility.OpenFilePanel(
            "Load Bullet Pattern Preset",
            Application.dataPath,
            "json"
        );
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        string json = File.ReadAllText(path);
        Undo.RecordObject(pattern, "Load Pattern Preset");
        EditorJsonUtility.FromJsonOverwrite(json, pattern);
        EditorUtility.SetDirty(pattern);
        Debug.Log($"[PatternLab] Preset loaded: {path}", pattern);
    }

    private static void SafeDestroyEditor(Editor ed)
    {
        if (ed != null)
            UnityEngine.Object.DestroyImmediate(ed);
    }

}
#endif
