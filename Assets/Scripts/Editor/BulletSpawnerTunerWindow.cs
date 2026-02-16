#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BulletSpawnerTunerWindow : EditorWindow
{
    private BulletSpawnerV3[] spawners = Array.Empty<BulletSpawnerV3>();
    private int selectedIndex = 0;
    private Vector2 scroll;

    private bool autoRefresh = true;
    private float refreshInterval = 1.0f;
    private double nextRefreshTime;

    // Test controls
    private Transform testTargetOverride;
    private int testGroupIndex = 0;
    private bool testResetGroup = true;
    private bool testFireImmediately = true;

    // UI
    private bool showRuntimeState = false;

    [MenuItem("Tools/Bullet Spawner Tuner")]
    public static void Open()
    {
        GetWindow<BulletSpawnerTunerWindow>("Bullet Spawner Tuner");
    }

    private void OnEnable()
    {
        RefreshSpawnerList();
        nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    private void EditorUpdate()
    {
        if (!autoRefresh) return;

        if (EditorApplication.timeSinceStartup >= nextRefreshTime)
        {
            nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
            RefreshSpawnerList();
            Repaint();
        }
    }

    private void RefreshSpawnerList()
    {
        spawners = UnityEngine.Object.FindObjectsByType<BulletSpawnerV3>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (spawners.Length == 0) selectedIndex = 0;
        else selectedIndex = Mathf.Clamp(selectedIndex, 0, spawners.Length - 1);

        testGroupIndex = Mathf.Clamp(testGroupIndex, 0, 9999);
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshSpawnerList();

            autoRefresh = GUILayout.Toggle(autoRefresh, "Auto", GUILayout.Width(55));
            EditorGUILayout.LabelField("Every", GUILayout.Width(35));
            refreshInterval = EditorGUILayout.FloatField(refreshInterval, GUILayout.Width(50));
            EditorGUILayout.LabelField("s", GUILayout.Width(15));

            GUILayout.FlexibleSpace();

            showRuntimeState = GUILayout.Toggle(showRuntimeState, "Show Runtime State", GUILayout.Width(150));
        }

        EditorGUILayout.Space(6);

        if (spawners.Length == 0)
        {
            EditorGUILayout.HelpBox("No BulletSpawnerV3 found in open scenes.", MessageType.Info);
            return;
        }

        // Spawner selector
        string[] names = new string[spawners.Length];
        for (int i = 0; i < spawners.Length; i++)
            names[i] = spawners[i] ? spawners[i].name : "<missing>";

        selectedIndex = EditorGUILayout.Popup("Spawner", selectedIndex, names);
        var spawner = spawners[selectedIndex];

        if (spawner == null)
        {
            EditorGUILayout.HelpBox("Selected spawner reference is missing.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping"))
                EditorGUIUtility.PingObject(spawner);

            if (GUILayout.Button("Select"))
                Selection.activeObject = spawner.gameObject;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save Preset...", GUILayout.Width(120)))
                SavePreset(spawner);

            if (GUILayout.Button("Load Preset...", GUILayout.Width(120)))
                LoadPreset(spawner);
        }

        EditorGUILayout.Space(8);
        DrawTestControls(spawner);
        EditorGUILayout.Space(10);

        // Draw full serialized config (groups + patterns)
        SerializedObject so = new SerializedObject(spawner);
        so.Update();

        SerializedProperty groupsProp = so.FindProperty("groups");
        if (groupsProp == null)
        {
            EditorGUILayout.HelpBox("Could not find 'groups' on BulletSpawnerV3. Field name mismatch?", MessageType.Warning);
        }
        else
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int g = 0; g < groupsProp.arraySize; g++)
            {
                SerializedProperty group = groupsProp.GetArrayElementAtIndex(g);
                var nameProp = group.FindPropertyRelative("name");
                var patternsProp = group.FindPropertyRelative("patterns");

                string groupName = nameProp != null ? nameProp.stringValue : $"Group {g}";
                group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, $"[{g}] {groupName}", true);

                if (group.isExpanded)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        if (nameProp != null) EditorGUILayout.PropertyField(nameProp);

                        if (patternsProp == null || !patternsProp.isArray)
                        {
                            EditorGUILayout.HelpBox("Group patterns missing or not an array.", MessageType.Warning);
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"Patterns: {patternsProp.arraySize}", EditorStyles.miniBoldLabel);

                            for (int p = 0; p < patternsProp.arraySize; p++)
                            {
                                SerializedProperty pat = patternsProp.GetArrayElementAtIndex(p);
                                var bulletPattern = pat.FindPropertyRelative("bulletPattern");

                                string patLabel = bulletPattern != null && bulletPattern.objectReferenceValue != null
                                    ? bulletPattern.objectReferenceValue.name
                                    : "None";

                                pat.isExpanded = EditorGUILayout.Foldout(pat.isExpanded, $"({p}) {patLabel}", true);
                                if (!pat.isExpanded) continue;

                                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                                {
                                    DrawRel(pat, "bulletPattern");

                                    DrawRel(pat, "attackCooldown");
                                    DrawRel(pat, "attackDuration");
                                    DrawRel(pat, "numberOfTimesToFire");
                                    DrawRel(pat, "timeBetweenFires");

                                    if (showRuntimeState)
                                    {
                                        EditorGUILayout.Space(4);
                                        EditorGUILayout.LabelField("Runtime (read-only)", EditorStyles.miniBoldLabel);

                                        DrawRelReadOnly(pat, "cooldownTimer");
                                        DrawRelReadOnly(pat, "activeTimer");
                                        DrawRelReadOnly(pat, "currentFireCount");
                                        DrawRelReadOnly(pat, "timeBetweenFireTimer");
                                        DrawRelReadOnly(pat, "isPatternActive");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(spawner);
        }
    }

    private void DrawTestControls(BulletSpawnerV3 spawner)
    {
        EditorGUILayout.LabelField("Test Controls (Play Mode)", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            testTargetOverride = (Transform)EditorGUILayout.ObjectField("Target Override", testTargetOverride, typeof(Transform), true);
            testGroupIndex = EditorGUILayout.IntField("Group Index", testGroupIndex);
            testResetGroup = EditorGUILayout.Toggle("Reset Group", testResetGroup);
            testFireImmediately = EditorGUILayout.Toggle("Fire Immediately", testFireImmediately);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = Application.isPlaying;

                if (GUILayout.Button("Play Group"))
                {
                    spawner.PlayGroup(testGroupIndex, testTargetOverride, testResetGroup, testFireImmediately);
                }

                if (GUILayout.Button("Stop (no reset)"))
                {
                    spawner.Stop(resetActiveGroup: false);
                }

                if (GUILayout.Button("Stop (reset active)"))
                {
                    spawner.Stop(resetActiveGroup: true);
                }

                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = Application.isPlaying;

                if (GUILayout.Button("Reset Group (no fire)"))
                {
                    spawner.ResetGroup(testGroupIndex, fireImmediately: false);
                }

                if (GUILayout.Button("Reset All (no fire)"))
                {
                    spawner.ResetAllGroups(fireImmediately: false);
                }

                GUI.enabled = true;
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Test controls require Play Mode so PlayGroup/Stop/Reset affect runtime state.", MessageType.Info);
        }
    }

    private void DrawRel(SerializedProperty parent, string relName)
    {
        var p = parent.FindPropertyRelative(relName);
        if (p != null) EditorGUILayout.PropertyField(p, true);
    }

    private void DrawRelReadOnly(SerializedProperty parent, string relName)
    {
        var p = parent.FindPropertyRelative(relName);
        if (p == null) return;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(p, true);
        }
    }

    // -------------------------
    // Preset Save / Load (JSON)
    // -------------------------

    [Serializable]
    private class SpawnerPreset
    {
        public int version = 1;
        public string spawnerName;
        public GroupPreset[] groups;
    }

    [Serializable]
    private class GroupPreset
    {
        public string name;
        public PatternPreset[] patterns;
    }

    [Serializable]
    private class PatternPreset
    {
        public string bulletPatternGuid; // stored by GUID so references survive renames/moves
        public float attackCooldown;
        public float attackDuration;
        public int numberOfTimesToFire;
        public float timeBetweenFires;
    }

    private void SavePreset(BulletSpawnerV3 spawner)
    {
        string defaultDir = Application.dataPath;
        string path = EditorUtility.SaveFilePanel(
            "Save Bullet Spawner Preset",
            defaultDir,
            $"{spawner.name}_spawner_preset.json",
            "json"
        );

        if (string.IsNullOrEmpty(path)) return;

        var preset = BuildPreset(spawner);
        string json = JsonUtility.ToJson(preset, prettyPrint: true);

        File.WriteAllText(path, json);
        Debug.Log($"[BulletSpawnerTuner] Preset saved: {path}", spawner);
    }

    private void LoadPreset(BulletSpawnerV3 spawner)
    {
        string defaultDir = Application.dataPath;
        string path = EditorUtility.OpenFilePanel(
            "Load Bullet Spawner Preset",
            defaultDir,
            "json"
        );

        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path))
        {
            Debug.LogError($"[BulletSpawnerTuner] File not found: {path}", spawner);
            return;
        }

        string json = File.ReadAllText(path);
        SpawnerPreset preset;

        try
        {
            preset = JsonUtility.FromJson<SpawnerPreset>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BulletSpawnerTuner] Failed to parse JSON: {e.Message}", spawner);
            return;
        }

        if (preset == null)
        {
            Debug.LogError("[BulletSpawnerTuner] Preset is null after parsing.", spawner);
            return;
        }

        ApplyPreset(spawner, preset);
        Debug.Log($"[BulletSpawnerTuner] Preset loaded: {path}", spawner);
    }

    private SpawnerPreset BuildPreset(BulletSpawnerV3 spawner)
    {
        var preset = new SpawnerPreset
        {
            spawnerName = spawner.name
        };

        var groups = spawner.groups;
        if (groups == null) groups = Array.Empty<PatternGroup>();

        preset.groups = new GroupPreset[groups.Length];

        for (int g = 0; g < groups.Length; g++)
        {
            var group = groups[g];
            var gp = new GroupPreset
            {
                name = group != null ? group.name : $"Group {g}"
            };

            var patterns = group != null && group.patterns != null ? group.patterns : Array.Empty<PatternWithCooldown>();
            gp.patterns = new PatternPreset[patterns.Length];

            for (int p = 0; p < patterns.Length; p++)
            {
                var pat = patterns[p];
                string guid = "";

                if (pat != null && pat.bulletPattern != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(pat.bulletPattern);
                    if (!string.IsNullOrEmpty(assetPath))
                        guid = AssetDatabase.AssetPathToGUID(assetPath);
                }

                gp.patterns[p] = new PatternPreset
                {
                    bulletPatternGuid = guid,
                    attackCooldown = pat != null ? pat.attackCooldown : 2f,
                    attackDuration = pat != null ? pat.attackDuration : 1f,
                    numberOfTimesToFire = pat != null ? pat.numberOfTimesToFire : 1,
                    timeBetweenFires = pat != null ? pat.timeBetweenFires : 0.5f,
                };
            }

            preset.groups[g] = gp;
        }

        return preset;
    }

    private void ApplyPreset(BulletSpawnerV3 spawner, SpawnerPreset preset)
    {
        Undo.RecordObject(spawner, "Apply Bullet Spawner Preset");

        if (spawner.groups == null) spawner.groups = Array.Empty<PatternGroup>();
        if (preset.groups == null) return;

        // Match by group name when possible, else by index
        var groupIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int g = 0; g < spawner.groups.Length; g++)
        {
            if (spawner.groups[g] == null) continue;
            if (!string.IsNullOrEmpty(spawner.groups[g].name) && !groupIndexByName.ContainsKey(spawner.groups[g].name))
                groupIndexByName.Add(spawner.groups[g].name, g);
        }

        for (int pg = 0; pg < preset.groups.Length; pg++)
        {
            var srcGroup = preset.groups[pg];
            if (srcGroup == null) continue;

            int g;
            if (!string.IsNullOrEmpty(srcGroup.name) && groupIndexByName.TryGetValue(srcGroup.name, out int found))
                g = found;
            else
                g = pg;

            if (g < 0 || g >= spawner.groups.Length) continue;
            if (spawner.groups[g] == null) continue;
            if (spawner.groups[g].patterns == null) continue;
            if (srcGroup.patterns == null) continue;

            // Optionally apply group name
            if (!string.IsNullOrEmpty(srcGroup.name))
                spawner.groups[g].name = srcGroup.name;

            int count = Mathf.Min(spawner.groups[g].patterns.Length, srcGroup.patterns.Length);
            for (int p = 0; p < count; p++)
            {
                var dst = spawner.groups[g].patterns[p];
                var src = srcGroup.patterns[p];
                if (dst == null || src == null) continue;

                // Resolve bullet pattern by GUID
                if (!string.IsNullOrEmpty(src.bulletPatternGuid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(src.bulletPatternGuid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<BulletPatternBase>(path);
                        if (asset != null) dst.bulletPattern = asset;
                    }
                }

                dst.attackCooldown = src.attackCooldown;
                dst.attackDuration = src.attackDuration;
                dst.numberOfTimesToFire = src.numberOfTimesToFire;
                dst.timeBetweenFires = src.timeBetweenFires;
            }
        }

        EditorUtility.SetDirty(spawner);
    }
}
#endif
