// Minimal managed model for testing the production formatting code without redistributing
// game assemblies or loading Unity native code. Cooking mutations are supplied by the tests.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BepInEx
{
    public static class Paths { public static string ConfigPath = ""; }
}

namespace UnityEngine
{
    public class GameObject
    {
        public bool activeSelf = true;
        public readonly Transform transform;
        public GameObject(Transform parent = null) { transform = new Transform(this, parent); }
    }

    public class Transform
    {
        public readonly GameObject gameObject;
        public readonly Transform parent;
        public Transform(GameObject gameObject, Transform parent) { this.gameObject = gameObject; this.parent = parent; }
    }

    public class MonoBehaviour
    {
        public bool enabled = true;
        public Transform transform;
    }

    public struct Color
    {
        public static Color white => default;
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string value, out Color colour) { colour = default; return true; }
        public static string ToHtmlStringRGB(Color colour) => "FFFFFF";
    }

    public static class Mathf
    {
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static int Abs(int value) => Math.Abs(value);
    }
}

public class Item : UnityEngine.MonoBehaviour
{
    public ItemCooking cooking;
    private Optionable<bool> overrideUsability = new Optionable<bool> { IsNone = true };
    public readonly List<UnityEngine.MonoBehaviour> components = new List<UnityEngine.MonoBehaviour>();
    public Item() { transform = new UnityEngine.GameObject().transform; }
    public T Add<T>(T component, UnityEngine.Transform branch = null) where T : UnityEngine.MonoBehaviour
    {
        component.transform = branch ?? transform;
        components.Add(component);
        return component;
    }
    public T[] GetComponentsInChildren<T>(bool includeInactive) => components.OfType<T>().ToArray();
    public void SetUsabilityOverride(bool canUse) { overrideUsability = new Optionable<bool> { IsNone = false, Value = canUse }; }
}

public class Optionable<T> { public bool IsNone { get; set; } public T Value { get; set; } }

public class ItemCooking { public int timesCookedLocal; public bool wreckWhenCooked; }
public class ItemActionBase : UnityEngine.MonoBehaviour { }
public class ItemAction : ItemActionBase { }
public class CustomUnknownAction : ItemActionBase { }
public class Action_RestoreHunger : ItemAction { public float restorationAmount; }
public class Action_InflictPoison : ItemAction { public float poisonPerSecond, inflictionTime; }
public class Action_ModifyStatus : ItemAction { public CharacterAfflictions.STATUSTYPE statusType; public float changeAmount; public bool ifSkeleton; }
public class Action_GiveExtraStamina : ItemAction { public float amount; }
public class Action_AddOrRemoveThorns : ItemAction { public int thornCount; }
public class Action_ClearAllStatus : ItemAction { }
public class Action_Consume : ItemAction { }
public class Action_ReduceUses : ItemAction { }
public class Action_PlayAnimation : ItemAction { }
public class Action_PlayItemAnimation : ItemAction { }
public class Action_IncrementSteamStat : ItemAction { }
public class Action_TryHotDogAchievement : ItemAction { }
public class Action_ApplyAffliction : ItemAction { }
public class Action_RandomMushroomEffect : ItemAction { }
public class PetrifySource : UnityEngine.MonoBehaviour { public int petrifyPerJump; }
public class CharacterAfflictions
{
    public enum STATUSTYPE { Injury, Hunger, Cold, Poison, Crab, Curse, Drowsy, Weight, Hot, Thorns, Spores, Web, Arrow, Petrify, FlyTrap }
}
namespace Peak
{
    public class Action_HealingGem : ItemAction { public float minPetrify, maxPetrify; }
}
namespace PeakItemTooltip
{
    public static class Zh
    {
        public static bool Active;
        public static bool IsActive() => Active;
    }

    public static class Plugin
    {
        public static readonly TestLogger Log = new TestLogger();
    }

    public sealed class TestLogger
    {
        public void LogInfo(object message) { }
        public void LogWarning(object message) { }
    }
}
