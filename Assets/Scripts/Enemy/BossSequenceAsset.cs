using System;
using System.Collections.Generic;
using UnityEngine;

public enum SequenceActionType
{
    Attack,
    Movement,
    Downtime,
    Retreat,
    Wait
}

public enum HealthGateMode
{
    None,
    AtOrBelow,   // hp% <= a
    AtOrAbove,   // hp% >= a
    Between      // b <= hp% <= a (a=max, b=min)
}

public enum OnGateFail
{
    SkipStep,
    StopSequence
}

[Serializable]
public class BossSequenceStep
{
    [Header("Action")]
    public SequenceActionType action = SequenceActionType.Attack;

    [Header("Attack")]
    public int attackPatternIndex = 0;

    [Header("Movement/Retreat")]
    public int movementPresetIndex = 0; // if you use movement presets

    [Header("Timing Overrides (-1 = use controller defaults)")]
    public float durationOverride = -1f;

    [Header("Gate")]
    public HealthGateMode healthGate = HealthGateMode.None;
    [Range(0f, 1f)] public float gateA = 1f;
    [Range(0f, 1f)] public float gateB = 0f;
    public OnGateFail onGateFail = OnGateFail.SkipStep;
}

[CreateAssetMenu(fileName = "BossSequence", menuName = "Boss/Boss Sequence")]
public class BossSequenceAsset : ScriptableObject
{
    public bool loop = true;
    public List<BossSequenceStep> steps = new();
}
