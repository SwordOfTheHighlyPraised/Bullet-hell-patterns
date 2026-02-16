#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class BossTunerWindow : EditorWindow
{
    private BossController[] bosses = Array.Empty<BossController>();
    private int selectedBossIndex = 0;
    private Vector2 scroll;
    private double nextRefreshTime;

    private bool autoRefresh = true;
    private float refreshInterval = 1.0f;

    [MenuItem("Tools/Boss Tuner")]
    public static void Open()
    {
        GetWindow<BossTunerWindow>("Boss Tuner");
    }

    private void OnEnable()
    {
        RefreshBossList();
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
            RefreshBossList();
            Repaint();
        }
    }

    private void RefreshBossList()
    {
        bosses = UnityEngine.Object.FindObjectsByType<BossController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (bosses.Length == 0) selectedBossIndex = 0;
        else selectedBossIndex = Mathf.Clamp(selectedBossIndex, 0, bosses.Length - 1);
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                RefreshBossList();

            autoRefresh = GUILayout.Toggle(autoRefresh, "Auto", GUILayout.Width(55));
            EditorGUILayout.LabelField("Every", GUILayout.Width(35));
            refreshInterval = EditorGUILayout.FloatField(refreshInterval, GUILayout.Width(50));
            EditorGUILayout.LabelField("s", GUILayout.Width(15));

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("TimeScale", GUILayout.Width(70));
            Time.timeScale = EditorGUILayout.Slider(Time.timeScale, 0f, 2f, GUILayout.Width(220));
            EditorGUILayout.LabelField(Time.timeScale.ToString("0.00"), GUILayout.Width(40));
        }

        EditorGUILayout.Space(6);

        if (bosses.Length == 0)
        {
            EditorGUILayout.HelpBox("No BossController found in open scenes.", MessageType.Info);
            return;
        }

        // Boss selector
        string[] names = new string[bosses.Length];
        for (int i = 0; i < bosses.Length; i++)
            names[i] = bosses[i] ? bosses[i].name : "<missing>";

        selectedBossIndex = EditorGUILayout.Popup("Boss", selectedBossIndex, names);
        var boss = bosses[selectedBossIndex];

        if (boss == null)
        {
            EditorGUILayout.HelpBox("Selected boss reference is missing.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping"))
                EditorGUIUtility.PingObject(boss);

            if (GUILayout.Button("Select"))
                Selection.activeObject = boss.gameObject;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save Preset...", GUILayout.Width(120)))
                SavePresetForBoss(boss);

            if (GUILayout.Button("Load Preset...", GUILayout.Width(120)))
                LoadPresetIntoBoss(boss);
        }

        EditorGUILayout.Space(8);

        // Draw boss + attacks via SerializedObject (safe in play mode too)
        SerializedObject so = new SerializedObject(boss);
        so.Update();

        DrawIfExists(so, "currentState");
        DrawIfExists(so, "canPatrol");
        DrawIfExists(so, "detectionDelay");
        DrawIfExists(so, "playerLostDelay");
        DrawIfExists(so, "rotationSpeed");

        // Optional fields (only if you added them)
        DrawIfExists(so, "selectMode");
        DrawIfExists(so, "resetSequenceOnEngage");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Attacks", EditorStyles.boldLabel);

        SerializedProperty attacksProp = so.FindProperty("attacks");
        if (attacksProp == null || !attacksProp.isArray)
        {
            EditorGUILayout.HelpBox("Could not find 'attacks' list on BossController.", MessageType.Warning);
        }
        else
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < attacksProp.arraySize; i++)
            {
                SerializedProperty entry = attacksProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = entry.FindPropertyRelative("name");

                string title = nameProp != null ? nameProp.stringValue : $"Attack {i}";
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, $"[{i}] {title}", true);

                if (entry.isExpanded)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        DrawRel(entry, "name");
                        DrawRel(entry, "attack");
                        DrawRel(entry, "weight");
                        DrawRel(entry, "duration");

                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Gates", EditorStyles.miniBoldLabel);
                        DrawRel(entry, "minDistance");
                        DrawRel(entry, "maxDistance");
                        DrawRel(entry, "minHpFraction");
                        DrawRel(entry, "maxHpFraction");
                        DrawRel(entry, "allowRepeat");

                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Bullet Settings (if using BossBulletAttack)", EditorStyles.miniBoldLabel);
                        DrawRel(entry, "patternGroupIndex");
                        DrawRel(entry, "resetOnBegin");
                        DrawRel(entry, "fireImmediately");
                        DrawRel(entry, "resetOnEnd");

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Weight=1"))
                                SetRelFloat(entry, "weight", 1f);

                            if (GUILayout.Button("Disable (weight=0)"))
                                SetRelFloat(entry, "weight", 0f);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(boss);
        }
    }

    private void DrawIfExists(SerializedObject so, string propertyName)
    {
        var p = so.FindProperty(propertyName);
        if (p != null) EditorGUILayout.PropertyField(p, true);
    }

    private void DrawRel(SerializedProperty parent, string relName)
    {
        var p = parent.FindPropertyRelative(relName);
        if (p != null) EditorGUILayout.PropertyField(p, true);
    }

    private void SetRelFloat(SerializedProperty parent, string relName, float v)
    {
        var p = parent.FindPropertyRelative(relName);
        if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
    }

    // -------------------------
    // Preset Save / Load
    // -------------------------

    [Serializable]
    private class BossPreset
    {
        public int version = 1;

        public string bossName;

        public bool canPatrol;
        public float detectionDelay;
        public float playerLostDelay;
        public float rotationSpeed;

        public int selectMode;              // optional
        public bool resetSequenceOnEngage;  // optional

        public AttackPreset[] attacks;
    }

    [Serializable]
    private class AttackPreset
    {
        public string name;

        public float weight;
        public float duration;

        public float minDistance;
        public float maxDistance;

        public float minHpFraction;
        public float maxHpFraction;

        public bool allowRepeat;

        // bullet-per-entry fields (optional)
        public int patternGroupIndex;
        public bool resetOnBegin;
        public bool fireImmediately;
        public bool resetOnEnd;
    }

    private void SavePresetForBoss(BossController boss)
    {
        string defaultDir = Application.dataPath;
        string path = EditorUtility.SaveFilePanel(
            "Save Boss Preset",
            defaultDir,
            $"{boss.name}_preset.json",
            "json"
        );

        if (string.IsNullOrEmpty(path)) return;

        var preset = BuildPresetFromBoss(boss);
        string json = JsonUtility.ToJson(preset, prettyPrint: true);

        File.WriteAllText(path, json);
        Debug.Log($"[BossTuner] Preset saved: {path}", boss);
    }

    private void LoadPresetIntoBoss(BossController boss)
    {
        string defaultDir = Application.dataPath;
        string path = EditorUtility.OpenFilePanel(
            "Load Boss Preset",
            defaultDir,
            "json"
        );

        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path))
        {
            Debug.LogError($"[BossTuner] File not found: {path}", boss);
            return;
        }

        string json = File.ReadAllText(path);
        BossPreset preset;

        try
        {
            preset = JsonUtility.FromJson<BossPreset>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BossTuner] Failed to parse preset JSON: {e.Message}", boss);
            return;
        }

        if (preset == null)
        {
            Debug.LogError("[BossTuner] Preset is null after parsing.", boss);
            return;
        }

        ApplyPresetToBoss(boss, preset);
        Debug.Log($"[BossTuner] Preset loaded: {path}", boss);
    }

    private BossPreset BuildPresetFromBoss(BossController boss)
    {
        var preset = new BossPreset
        {
            bossName = boss.name,
            canPatrol = boss.canPatrol,
            detectionDelay = boss.detectionDelay,
            playerLostDelay = boss.playerLostDelay,
            rotationSpeed = boss.rotationSpeed,
        };

        // optional fields via reflection (won't break if not present)
        preset.selectMode = GetIntOptional(boss, "selectMode", 0);
        preset.resetSequenceOnEngage = GetBoolOptional(boss, "resetSequenceOnEngage", true);

        var list = boss.attacks;
        if (list == null) list = new List<BossController.AttackEntry>();

        preset.attacks = new AttackPreset[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            preset.attacks[i] = new AttackPreset
            {
                name = a.name,

                weight = a.weight,
                duration = a.duration,

                minDistance = a.minDistance,
                maxDistance = a.maxDistance,

                minHpFraction = a.minHpFraction,
                maxHpFraction = a.maxHpFraction,

                allowRepeat = a.allowRepeat,

                patternGroupIndex = GetIntOptional(a, "patternGroupIndex", 0),
                resetOnBegin = GetBoolOptional(a, "resetOnBegin", true),
                fireImmediately = GetBoolOptional(a, "fireImmediately", true),
                resetOnEnd = GetBoolOptional(a, "resetOnEnd", true),
            };
        }

        return preset;
    }

    private void ApplyPresetToBoss(BossController boss, BossPreset preset)
    {
        Undo.RecordObject(boss, "Apply Boss Preset");

        boss.canPatrol = preset.canPatrol;
        boss.detectionDelay = preset.detectionDelay;
        boss.playerLostDelay = preset.playerLostDelay;
        boss.rotationSpeed = preset.rotationSpeed;

        // optional fields via reflection
        SetIntOptional(boss, "selectMode", preset.selectMode);
        SetBoolOptional(boss, "resetSequenceOnEngage", preset.resetSequenceOnEngage);

        if (boss.attacks == null) boss.attacks = new List<BossController.AttackEntry>();

        // Apply attacks: prefer name match when counts differ, otherwise index
        var byName = new Dictionary<string, AttackPreset>(StringComparer.Ordinal);
        if (preset.attacks != null)
        {
            for (int i = 0; i < preset.attacks.Length; i++)
            {
                var p = preset.attacks[i];
                if (!string.IsNullOrEmpty(p.name) && !byName.ContainsKey(p.name))
                    byName.Add(p.name, p);
            }
        }

        for (int i = 0; i < boss.attacks.Count; i++)
        {
            var entry = boss.attacks[i];

            AttackPreset p = null;

            if (preset.attacks != null && preset.attacks.Length == boss.attacks.Count)
                p = preset.attacks[i];
            else if (!string.IsNullOrEmpty(entry.name) && byName.TryGetValue(entry.name, out var found))
                p = found;

            if (p == null) continue;

            entry.weight = p.weight;
            entry.duration = p.duration;

            entry.minDistance = p.minDistance;
            entry.maxDistance = p.maxDistance;

            entry.minHpFraction = p.minHpFraction;
            entry.maxHpFraction = p.maxHpFraction;

            entry.allowRepeat = p.allowRepeat;

            SetIntOptional(entry, "patternGroupIndex", p.patternGroupIndex);
            SetBoolOptional(entry, "resetOnBegin", p.resetOnBegin);
            SetBoolOptional(entry, "fireImmediately", p.fireImmediately);
            SetBoolOptional(entry, "resetOnEnd", p.resetOnEnd);

            boss.attacks[i] = entry;
        }

        EditorUtility.SetDirty(boss);
    }

    // -------------------------
    // Reflection helpers (skip if field doesn’t exist)
    // -------------------------

    private static int GetIntOptional(object obj, string name, int fallback)
    {
        if (obj == null) return fallback;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(int) && p.CanRead) return (int)p.GetValue(obj);
        return fallback;
    }

    private static bool GetBoolOptional(object obj, string name, bool fallback)
    {
        if (obj == null) return fallback;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(obj);
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(bool) && p.CanRead) return (bool)p.GetValue(obj);
        return fallback;
    }

    private static void SetIntOptional(object obj, string name, int value)
    {
        if (obj == null) return;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int)) { f.SetValue(obj, value); return; }
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(int) && p.CanWrite) { p.SetValue(obj, value); }
    }

    private static void SetBoolOptional(object obj, string name, bool value)
    {
        if (obj == null) return;
        var t = obj.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(bool)) { f.SetValue(obj, value); return; }
        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(bool) && p.CanWrite) { p.SetValue(obj, value); }
    }
}
#endif
