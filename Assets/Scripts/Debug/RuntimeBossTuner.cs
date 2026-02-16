using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class RuntimeBossTuner : MonoBehaviour
{
    [Header("UI")]
    public KeyCode toggleKey = KeyCode.F1;
    public bool showOnStart = true;

    [Header("Discovery")]
    public bool autoRefresh = true;
    public float refreshInterval = 1.0f;

    private bool visible;
    private Vector2 scroll;
    private float refreshTimer;

    private BossController[] bosses = Array.Empty<BossController>();
    private int selectedBossIndex = 0;

    private GUIStyle headerStyle;

    private bool collapsed = false;


    private void Awake()
    {
        visible = showOnStart;

        RefreshBossList();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            visible = !visible;

        if (!autoRefresh) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval;
            RefreshBossList();
        }
    }

    private void RefreshBossList()
    {
        bosses = FindObjectsByType<BossController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (bosses.Length == 0) selectedBossIndex = 0;
        else selectedBossIndex = Mathf.Clamp(selectedBossIndex, 0, bosses.Length - 1);
    }


    private void OnGUI()
    {
        if (!visible) return;


        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
        }

        float margin = 12f;
        float w = collapsed ? 60f : 420f;
        float h = Mathf.Min(760f, Screen.height - margin * 2f);

        float x = Screen.width - w - margin; // dock right ✅
        float y = margin;

        GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.window);


        GUILayout.BeginHorizontal();
        if (GUILayout.Button(collapsed ? "▶" : "◀", GUILayout.Width(28))) collapsed = !collapsed;
        GUILayout.Label("Runtime Boss Tuner", headerStyle);
        GUILayout.EndHorizontal();

        if (collapsed)
        {
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("Runtime Boss Tuner", headerStyle);

        // Top bar
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Width(90))) RefreshBossList();
        GUILayout.Label($"Bosses: {bosses.Length}", GUILayout.Width(120));
        GUILayout.FlexibleSpace();
        GUILayout.Label("TimeScale", GUILayout.Width(70));
        Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0f, 2f, GUILayout.Width(120));
        GUILayout.Label($"{Time.timeScale:0.00}", GUILayout.Width(50));
        GUILayout.EndHorizontal();

        if (bosses.Length == 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("No BossController found in scene.");
            GUILayout.EndArea();
            return;
        }

        // Boss selector
        GUILayout.Space(8);
        GUILayout.Label("Select Boss", headerStyle);

        string[] bossNames = new string[bosses.Length];
        for (int i = 0; i < bosses.Length; i++)
            bossNames[i] = bosses[i] ? $"{i}: {bosses[i].name}" : $"{i}: <missing>";

        selectedBossIndex = GUILayout.SelectionGrid(selectedBossIndex, bossNames, 1, GUI.skin.button);
        var boss = bosses[selectedBossIndex];

        if (boss == null)
        {
            GUILayout.Label("Selected boss reference is missing.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label("Boss Settings", headerStyle);

        // Basic boss tuning (public fields)
        boss.canPatrol = GUILayout.Toggle(boss.canPatrol, "Can Patrol");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Detection Delay", GUILayout.Width(130));
        boss.detectionDelay = FloatField(boss.detectionDelay, 90);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Player Lost Delay", GUILayout.Width(130));
        boss.playerLostDelay = FloatField(boss.playerLostDelay, 90);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Rotation Speed", GUILayout.Width(130));
        boss.rotationSpeed = FloatField(boss.rotationSpeed, 90);
        GUILayout.EndHorizontal();

        // If you added these in your controller, they’ll show; if not, you can remove these lines.
        try
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Select Mode", GUILayout.Width(130));
            boss.selectMode = (BossController.AttackSelectMode)GUILayout.SelectionGrid(
                (int)boss.selectMode,
                new[] { "Sequence", "WeightedRandom" },
                2
            );
            GUILayout.EndHorizontal();

            boss.resetSequenceOnEngage = GUILayout.Toggle(boss.resetSequenceOnEngage, "Reset Sequence On Engage");
        }
        catch { /* ignore if your BossController doesn't have these members */ }

        GUILayout.Space(10);
        GUILayout.Label("Attacks", headerStyle);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(430));

        if (boss.attacks == null || boss.attacks.Count == 0)
        {
            GUILayout.Label("No attacks configured.");
        }
        else
        {
            for (int i = 0; i < boss.attacks.Count; i++)
            {
                var a = boss.attacks[i];

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"[{i}] {a.name}", headerStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(a.attack ? a.attack.GetType().Name : "attack=NULL");
                GUILayout.EndHorizontal();

                // Weight + duration
                GUILayout.BeginHorizontal();
                GUILayout.Label("Weight", GUILayout.Width(80));
                a.weight = FloatField(a.weight, 70);
                GUILayout.Label("Duration", GUILayout.Width(70));
                a.duration = FloatField(a.duration, 70);
                GUILayout.EndHorizontal();

                // Distance gates
                GUILayout.BeginHorizontal();
                GUILayout.Label("MinDist", GUILayout.Width(80));
                a.minDistance = FloatField(a.minDistance, 70);
                GUILayout.Label("MaxDist", GUILayout.Width(70));
                a.maxDistance = FloatField(a.maxDistance, 70);
                GUILayout.EndHorizontal();

                // HP gates
                GUILayout.BeginHorizontal();
                GUILayout.Label("MinHP", GUILayout.Width(80));
                a.minHpFraction = Mathf.Clamp01(FloatField(a.minHpFraction, 70));
                GUILayout.Label("MaxHP", GUILayout.Width(70));
                a.maxHpFraction = Mathf.Clamp01(FloatField(a.maxHpFraction, 70));
                GUILayout.EndHorizontal();

                a.allowRepeat = GUILayout.Toggle(a.allowRepeat, "Allow Repeat");

                // Bullet settings (only used if the selected attack is BossBulletAttack)
                GUILayout.Space(6);
                GUILayout.Label("Bullet Settings (per entry)", GUI.skin.label);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Group Index", GUILayout.Width(80));
                a.patternGroupIndex = IntField(a.patternGroupIndex, 70);
                GUILayout.EndHorizontal();

                a.resetOnBegin = GUILayout.Toggle(a.resetOnBegin, "Reset On Begin");
                a.fireImmediately = GUILayout.Toggle(a.fireImmediately, "Fire Immediately");
                a.resetOnEnd = GUILayout.Toggle(a.resetOnEnd, "Reset On End");

                GUILayout.EndVertical();

                // Write back (important because AttackEntry is a class, but good practice anyway)
                boss.attacks[i] = a;
            }
        }

        GUILayout.EndScrollView();

        // Utility
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set All Weights = 1"))
        {
            for (int i = 0; i < boss.attacks.Count; i++)
                boss.attacks[i].weight = 1f;
        }
        if (GUILayout.Button("Disable All (weight=0)"))
        {
            for (int i = 0; i < boss.attacks.Count; i++)
                boss.attacks[i].weight = 0f;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private float FloatField(float value, float width)
    {
        string s = GUILayout.TextField(value.ToString("0.###"), GUILayout.Width(width));
        if (float.TryParse(s, out float v)) return v;
        return value;
    }

    private int IntField(int value, float width)
    {
        string s = GUILayout.TextField(value.ToString(), GUILayout.Width(width));
        if (int.TryParse(s, out int v)) return v;
        return value;
    }
}
#else
public class RuntimeBossTuner : MonoBehaviour { }
#endif
