/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    item-modifiers.cs
 * @brief   reads an item's effect components and formats them as signed, colour-coded stat modifier lines
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PeakItemTooltip
{
    /**
     * @brief reads a consumable item's effect components and formats them as signed, colour-coded lines, scaling fractions to points
     */
    public static class ItemModifiers
    {
        // status display colours
        private static readonly Dictionary<CharacterAfflictions.STATUSTYPE, Color> Colours = new Dictionary<CharacterAfflictions.STATUSTYPE, Color>
        {
            { CharacterAfflictions.STATUSTYPE.Injury,  Hex("#E4463B") },
            { CharacterAfflictions.STATUSTYPE.Hunger,  Hex("#F2C43D") },
            { CharacterAfflictions.STATUSTYPE.Cold,    Hex("#4FA3FF") },
            { CharacterAfflictions.STATUSTYPE.Poison,  Hex("#A65AD8") },
            { CharacterAfflictions.STATUSTYPE.Crab,    Hex("#FF7043") },
            { CharacterAfflictions.STATUSTYPE.Curse,   Hex("#B5179E") },
            { CharacterAfflictions.STATUSTYPE.Drowsy,  Hex("#5AD1C4") },
            { CharacterAfflictions.STATUSTYPE.Weight,  Hex("#B0A08A") },
            { CharacterAfflictions.STATUSTYPE.Hot,     Hex("#FF8C1A") },
            { CharacterAfflictions.STATUSTYPE.Thorns,  Hex("#6FBF4B") },
            { CharacterAfflictions.STATUSTYPE.Spores,  Hex("#9CCC65") },
            { CharacterAfflictions.STATUSTYPE.Web,     Hex("#D0D0D0") },
            { CharacterAfflictions.STATUSTYPE.Arrow,   Hex("#C9A66B") },
            { CharacterAfflictions.STATUSTYPE.Petrify, Hex("#9E9E9E") },
            { CharacterAfflictions.STATUSTYPE.FlyTrap, Hex("#4CAF50") },
        };

        // since extra stamina is not a STATUSTYPE, assign its own colour
        private static readonly Color StaminaColour = Hex("#7CD64B");
        private static readonly Color Fallback = Color.white;
        // These actions are either rendered below or cannot add an adverse item-use effect.
        // Any other enabled Action_* source makes categorical safety claims unverified.
        private static readonly HashSet<string> AccountedActionSources = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Action_RestoreHunger),
            nameof(Action_InflictPoison),
            nameof(Action_ModifyStatus),
            nameof(Action_GiveExtraStamina),
            "Action_AddOrRemoveThorns",
            "Action_ClearAllStatus",
            "Action_Consume",
            "Action_ReduceUses",
            "Action_PlayAnimation",
            "Action_PlayItemAnimation",
            "Action_IncrementSteamStat",
            "Action_TryHotDogAchievement",
        };
        private static readonly string[] NonConsumeTriggerFields =
        {
            "OnPressed", "OnHeld", "OnCastFinished", "OnCancelled",
            "OnSecondaryCastFinished", "OnSecondaryPressed", "OnSecondaryHeld", "OnSecondaryCancelled"
        };
        private static readonly string[] TriggerFields =
        {
            "OnPressed", "OnHeld", "OnCastFinished", "OnCancelled",
            "OnSecondaryCastFinished", "OnSecondaryPressed", "OnSecondaryHeld", "OnSecondaryCancelled", "OnConsumed"
        };

        /**
         * @brief builds a rich-text block of the item's stat modifiers, one coloured line per modifier, or an empty string if it has none
         */
        public static string Format(Item item)
        {
            if (item == null) return "";

            StringBuilder sb = new StringBuilder();
            bool first = true;

            // order from top to bottom is as such: [1] hunger, [2] poison, [3] weight/other, [4] stamina, [5] petrify

            // SUBTRACTED hunger: negative net change in hunger
            foreach (Action_RestoreHunger a in item.GetComponentsInChildren<Action_RestoreHunger>(true))
            {
                if (!EffectEnabled(a, item)) continue;
                AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Hunger), -a.restorationAmount,
                    QualifyActionLabel(a, Zh.IsActive() ? "饥饿" : "Hunger"));
            }

            // ADDED poision over time: total applied = poison per second * time
            foreach (Action_InflictPoison a in item.GetComponentsInChildren<Action_InflictPoison>(true))
            {
                if (!EffectEnabled(a, item) || a.inflictionTime <= 0f) continue;
                AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Poison), a.poisonPerSecond * a.inflictionTime,
                    QualifyActionLabel(a, PoisonLabel(a.delay, a.inflictionTime)));
            }

            // signed generic status changes: covers weight and anything else)t.
            foreach (Action_ModifyStatus a in item.GetComponentsInChildren<Action_ModifyStatus>(true))
            {
                if (!EffectEnabled(a, item)) continue;
                AppendLine(sb, ref first, ColourFor(a.statusType), a.changeAmount,
                    QualifyActionLabel(a, StatusName(a.statusType)));
            }

            // ADDED extra stamina
            foreach (Action_GiveExtraStamina a in item.GetComponentsInChildren<Action_GiveExtraStamina>(true))
            {
                if (!EffectEnabled(a, item)) continue;
                AppendLine(sb, ref first, StaminaColour, a.amount,
                    QualifyActionLabel(a, Zh.IsActive() ? "额外耐力" : "Extra Stamina"));
            }

            AppendActionAfflictions(sb, ref first, item);

            // thorns are applied directly to the character rather than through a STATUSTYPE component
            foreach (MonoBehaviour action in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!EffectEnabled(action, item) || action.GetType().Name != "Action_AddOrRemoveThorns") continue;
                if (TryReadIntField(action, "thornCount", out int thornCount))
                    AppendPoints(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Thorns), thornCount,
                        QualifyActionLabel(action, Zh.IsActive() ? "荆棘" : "Thorns"));
            }

            // ADDED petrify: unlike other statuses, petrify is never applied through Action_ModifyStatus.
            // amulets call AddPetrify(int)/AddStatus(Petrify, float) directly, storing the cost in fields
            // named petrify* on their components (e.g. Action_CloneSelectedItem.petrify, DoubleJumpAmulet.
            // petrifyPerJump). read those fields off the item's components so every petrify source shows up
            AppendPetrify(sb, ref first, item);

            return sb.ToString();
        }

        /// <summary>
        /// Returns the modifier rows captured for the same refresh as the item's description.
        /// </summary>
        public static string FormatSnapshot(TooltipItemSnapshot snapshot)
        {
            return snapshot != null ? snapshot.Modifiers : "";
        }

        /// <summary>
        /// Returns positive statuses supported by the modifier formatter. State checks use these
        /// semantic names to avoid showing a Wiki claim that contradicts the live modifier rows.
        /// </summary>
        public static HashSet<string> ActivePositiveStatuses(Item item)
        {
            var statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (item == null) return statuses;

            foreach (Action_InflictPoison action in item.GetComponentsInChildren<Action_InflictPoison>(true))
            {
                if (EffectEnabled(action, item) && action.inflictionTime > 0f
                    && action.poisonPerSecond * action.inflictionTime > 0f)
                    statuses.Add(CharacterAfflictions.STATUSTYPE.Poison.ToString());
            }

            foreach (Action_ModifyStatus action in item.GetComponentsInChildren<Action_ModifyStatus>(true))
            {
                if (EffectEnabled(action, item) && action.changeAmount > 0f)
                    statuses.Add(action.statusType.ToString());
            }

            foreach (Action_GiveExtraStamina action in item.GetComponentsInChildren<Action_GiveExtraStamina>(true))
            {
                if (EffectEnabled(action, item) && action.amount > 0f)
                    statuses.Add("ExtraStamina");
            }

            foreach (MonoBehaviour action in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (EffectEnabled(action, item) && action.GetType().Name == "Action_AddOrRemoveThorns"
                    && TryReadIntField(action, "thornCount", out int thornCount) && thornCount > 0)
                    statuses.Add(CharacterAfflictions.STATUSTYPE.Thorns.ToString());

                if (EffectEnabled(action, item) && IsApplyAfflictionAction(action) && !IsMassAfflictionAction(action)
                    && TryGetAppliedAfflictions(action, out List<object> afflictions))
                {
                    foreach (object affliction in afflictions)
                        AddPositiveAfflictionStatus(statuses, affliction);
                }

                if (EffectEnabled(action, item) && HasPositivePetrifyEffect(action))
                    statuses.Add(CharacterAfflictions.STATUSTYPE.Petrify.ToString());
            }

            return statuses;
        }

        /// <summary>
        /// Returns false when an enabled Action_* component can affect use but is not accounted for
        /// by the live effect reader. This gates Wiki safety claims without guessing what an unknown
        /// action does.
        /// </summary>
        public static bool HasCompleteSafetyCoverage(Item item)
        {
            if (item == null) return true;

            foreach (MonoBehaviour action in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!EffectEnabled(action, item)) continue;
                string typeName = action.GetType().Name;
                if (!(action is ItemActionBase) && !typeName.StartsWith("Action_", StringComparison.Ordinal)) continue;
                if (IsApplyAfflictionAction(action))
                {
                    if (!IsMassAfflictionAction(action)
                        && (typeName == "Action_ApplyAffliction" || typeName == "Action_SuperJumpAmulet")
                        && HasCompleteApplyAfflictionCoverage(action)) continue;
                    return false;
                }
                if (!AccountedActionSources.Contains(typeName)) return false;

                // A status action conditioned on character type is not a universal safety guarantee.
                if (typeName == nameof(Action_ModifyStatus)
                    && TryReadBoolField(action, "ifSkeleton", out bool ifSkeleton) && ifSkeleton)
                    return false;
            }

            return true;
        }

        private static void AppendActionAfflictions(StringBuilder sb, ref bool first, Item item)
        {
            foreach (MonoBehaviour action in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!EffectEnabled(action, item) || !IsApplyAfflictionAction(action)
                    || !TryGetAppliedAfflictions(action, out List<object> afflictions)) continue;
                // Area payload values are useful only when their target condition can be shown.
                // Do not turn a conditional nearby-player effect into an unconditional self effect.
                if (IsMassAfflictionAction(action) && !TryReadMassTarget(action, out float _, out bool _)) continue;

                foreach (object affliction in afflictions)
                {
                    if (affliction == null) continue;
                    string typeName = affliction.GetType().Name;
                    if (typeName == "Affliction_AdjustStatus"
                        && TryReadStatusField(affliction, "statusType", out CharacterAfflictions.STATUSTYPE status)
                        && TryReadFloatField(affliction, "statusAmount", out float amount))
                    {
                        AppendLine(sb, ref first, ColourFor(status), amount,
                            QualifyActionLabel(action, StatusName(status)));
                    }
                    else if (typeName == "Affliction_PoisonOverTime"
                        && TryReadFloatField(affliction, "totalTime", out float totalTime)
                        && TryReadFloatField(affliction, "delayBeforeEffect", out float delay)
                        && TryReadFloatField(affliction, "statusPerSecond", out float perSecond))
                    {
                        float duration = Math.Max(0f, totalTime - delay);
                        AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Poison), perSecond * duration,
                            QualifyActionLabel(action, PoisonLabel(delay, duration)));
                    }
                    else if (typeName == "Affliction_AdjustColdOverTime"
                        && TryReadFloatField(affliction, "statusPerSecond", out float coldPerSecond)
                        && TryReadFloatField(affliction, "totalTime", out float coldDuration))
                    {
                        AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Cold), coldPerSecond * coldDuration,
                            QualifyActionLabel(action, TimedStatusLabel(CharacterAfflictions.STATUSTYPE.Cold, coldDuration)));
                    }
                    else if (typeName == "Affliction_AdjustDrowsyOverTime"
                        && TryReadFloatField(affliction, "statusPerSecond", out float drowsyPerSecond)
                        && TryReadFloatField(affliction, "totalTime", out float drowsyDuration))
                    {
                        AppendLine(sb, ref first, ColourFor(CharacterAfflictions.STATUSTYPE.Drowsy), drowsyPerSecond * drowsyDuration,
                            QualifyActionLabel(action, TimedStatusLabel(CharacterAfflictions.STATUSTYPE.Drowsy, drowsyDuration)));
                    }
                    else if (typeName == "Affliction_AddBonusStamina"
                        && TryReadFloatField(affliction, "staminaAmount", out float staminaAmount)
                        && TryReadFloatField(affliction, "totalTime", out float staminaDuration))
                    {
                        string label = Zh.IsActive() ? "额外耐力" : "Extra Stamina";
                        if (staminaDuration > 0f) label = TimedStatusLabel(label, staminaDuration);
                        AppendLine(sb, ref first, StaminaColour, staminaAmount, QualifyActionLabel(action, label));
                    }
                }
            }
        }

        private static bool HasCompleteApplyAfflictionCoverage(MonoBehaviour action)
        {
            if (action == null || !IsApplyAfflictionAction(action)
                || !TryGetAppliedAfflictions(action, out List<object> afflictions)) return false;

            foreach (object affliction in afflictions)
            {
                if (affliction == null) continue;
                string typeName = affliction.GetType().Name;
                if (typeName == "Affliction_AdjustStatus"
                    && TryReadStatusField(affliction, "statusType", out CharacterAfflictions.STATUSTYPE _)
                    && TryReadFloatField(affliction, "statusAmount", out float _)) continue;
                if (typeName == "Affliction_PoisonOverTime"
                    && TryReadFloatField(affliction, "totalTime", out float _)
                    && TryReadFloatField(affliction, "delayBeforeEffect", out float _)
                    && TryReadFloatField(affliction, "statusPerSecond", out float _)) continue;
                if (typeName == "Affliction_AdjustColdOverTime"
                    && TryReadFloatField(affliction, "totalTime", out float _)
                    && TryReadFloatField(affliction, "statusPerSecond", out float _)) continue;
                if (typeName == "Affliction_AdjustDrowsyOverTime"
                    && TryReadFloatField(affliction, "totalTime", out float _)
                    && TryReadFloatField(affliction, "statusPerSecond", out float _)) continue;
                if (typeName == "Affliction_AddBonusStamina"
                    && TryReadFloatField(affliction, "totalTime", out float _)
                    && TryReadFloatField(affliction, "staminaAmount", out float _)) continue;
                return false;
            }

            return true;
        }

        private static bool TryGetAppliedAfflictions(MonoBehaviour action, out List<object> afflictions)
        {
            afflictions = new List<object>();
            if (action == null || !IsApplyAfflictionAction(action)) return false;

            FieldInfo primaryField = action.GetType().GetField("affliction", BindingFlags.Public | BindingFlags.Instance);
            if (primaryField == null) return false;
            object primary = primaryField.GetValue(action);
            // The game returns before applying extras when the primary affliction is null.
            if (primary == null) return true;
            afflictions.Add(primary);

            FieldInfo extrasField = action.GetType().GetField("extraAfflictions", BindingFlags.Public | BindingFlags.Instance);
            if (extrasField == null) return false;
            if (extrasField.GetValue(action) is Array extras)
            {
                foreach (object extra in extras)
                    if (extra != null) afflictions.Add(extra);
            }

            return true;
        }

        private static bool IsApplyAfflictionAction(MonoBehaviour action)
        {
            for (Type type = action?.GetType(); type != null; type = type.BaseType)
                if (type.Name == "Action_ApplyAffliction") return true;
            return false;
        }

        private static bool IsMassAfflictionAction(MonoBehaviour action)
        {
            for (Type type = action?.GetType(); type != null; type = type.BaseType)
                if (type.Name == "Action_ApplyMassAffliction") return true;
            return false;
        }

        private static bool TryReadMassTarget(MonoBehaviour action, out float radius, out bool ignoreCaster)
        {
            radius = 0f;
            ignoreCaster = false;
            return TryReadFloatField(action, "radius", out radius)
                && !float.IsNaN(radius) && !float.IsInfinity(radius) && radius >= 0f
                && TryReadBoolField(action, "ignoreCaster", out ignoreCaster);
        }

        private static bool HasPositivePetrifyEffect(MonoBehaviour action)
        {
            foreach (FieldInfo field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!field.Name.StartsWith("petrify", StringComparison.OrdinalIgnoreCase)
                    && !field.Name.EndsWith("Petrify", StringComparison.OrdinalIgnoreCase)) continue;
                if (field.FieldType == typeof(int) && (int)field.GetValue(action) > 0) return true;
                if (field.FieldType == typeof(float) && (float)field.GetValue(action) > 0f) return true;
            }

            return false;
        }

        private static void AddPositiveAfflictionStatus(HashSet<string> statuses, object affliction)
        {
            if (affliction == null) return;
            string typeName = affliction.GetType().Name;
            if (typeName == "Affliction_AdjustStatus"
                && TryReadStatusField(affliction, "statusType", out CharacterAfflictions.STATUSTYPE status)
                && TryReadFloatField(affliction, "statusAmount", out float amount) && amount > 0f)
                statuses.Add(status.ToString());
            else if (typeName == "Affliction_PoisonOverTime"
                && TryReadFloatField(affliction, "totalTime", out float totalTime)
                && TryReadFloatField(affliction, "delayBeforeEffect", out float delay)
                && TryReadFloatField(affliction, "statusPerSecond", out float perSecond)
                && perSecond * Math.Max(0f, totalTime - delay) > 0f)
                statuses.Add(CharacterAfflictions.STATUSTYPE.Poison.ToString());
            else if (typeName == "Affliction_AdjustColdOverTime"
                && TryReadFloatField(affliction, "totalTime", out float coldDuration)
                && TryReadFloatField(affliction, "statusPerSecond", out float coldPerSecond)
                && coldDuration > 0f && coldPerSecond > 0f)
                statuses.Add(CharacterAfflictions.STATUSTYPE.Cold.ToString());
            else if (typeName == "Affliction_AdjustDrowsyOverTime"
                && TryReadFloatField(affliction, "totalTime", out float drowsyDuration)
                && TryReadFloatField(affliction, "statusPerSecond", out float drowsyPerSecond)
                && drowsyDuration > 0f && drowsyPerSecond > 0f)
                statuses.Add(CharacterAfflictions.STATUSTYPE.Drowsy.ToString());
            else if (typeName == "Affliction_AddBonusStamina"
                && TryReadFloatField(affliction, "staminaAmount", out float staminaAmount) && staminaAmount > 0f)
                statuses.Add("ExtraStamina");
        }

        /**
         * @brief scans the item's components for petrify cost fields and appends one line per field found
         * @param sb the builder the lines are appended to
         * @param first whether the next line is the first; updated as lines are appended
         * @param item the item whose components are scanned
         */
        private static void AppendPetrify(StringBuilder sb, ref bool first, Item item)
        {
            Color colour = ColourFor(CharacterAfflictions.STATUSTYPE.Petrify);

            foreach (MonoBehaviour comp in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!EffectEnabled(comp, item)) continue;

                foreach (FieldInfo f in comp.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!f.Name.StartsWith("petrify", StringComparison.OrdinalIgnoreCase)) continue;

                    // int petrify fields are already in 0-100 points; float fields are on the 0-1 status
                    // scale (AddStatus floors amount*100 into points), so bring both to points
                    int points;
                    if (f.FieldType == typeof(int)) points = (int)f.GetValue(comp);
                    else if (f.FieldType == typeof(float)) points = Mathf.RoundToInt((float)f.GetValue(comp) * 100f);
                    else continue;

                    AppendPoints(sb, ref first, colour, points, QualifyActionLabel(comp, PetrifyLabel(f.Name)));
                }
            }

            // the healing amulet (scout's tenacity) petrifies by clamp(heal * ratio, minPetrify, maxPetrify),
            // so the actual cost varies with how much is healed; a range rather than a flat cost, so handle it
            // on its own and show the min-max bounds
            foreach (Peak.Action_HealingGem a in item.GetComponentsInChildren<Peak.Action_HealingGem>(true))
            {
                if (!EffectEnabled(a, item)) continue;
                int low = Mathf.RoundToInt(a.minPetrify * 100f);
                int high = Mathf.RoundToInt(a.maxPetrify * 100f);
                AppendPointsRange(sb, ref first, colour, low, high,
                    QualifyActionLabel(a, Zh.IsActive() ? "石化" : "Petrify"));
            }
        }

        // CookingBehavior_DisableScripts keeps components attached but unsubscribes their actions.
        // Check each component and its child branch, rather than the item's activeInHierarchy:
        // an inventory item can have an inactive root and still have usable effects when equipped.
        private static bool EffectEnabled(MonoBehaviour effect, Item item)
        {
            if (effect == null || !effect.enabled) return false;

            for (Transform branch = effect.transform; branch != null && branch != item.transform; branch = branch.parent)
            {
                if (!branch.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static bool TryReadIntField(MonoBehaviour component, string name, out int value)
        {
            FieldInfo field = component.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int))
            {
                value = (int)field.GetValue(component);
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryReadFloatField(object target, string name, out float value)
        {
            FieldInfo field = target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(float))
            {
                value = (float)field.GetValue(target);
                return true;
            }

            value = 0f;
            return false;
        }

        private static bool TryReadStatusField(object target, string name, out CharacterAfflictions.STATUSTYPE value)
        {
            FieldInfo field = target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType.IsEnum
                && Enum.TryParse(field.GetValue(target)?.ToString(), out value)) return true;

            value = default;
            return false;
        }

        private static bool TryReadBoolField(MonoBehaviour component, string name, out bool value)
        {
            FieldInfo field = component.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(component);
                return true;
            }

            value = false;
            return false;
        }

        private static string PoisonLabel(float delay, float duration)
        {
            string label = Zh.IsActive() ? "中毒" : "Poison";
            if (Zh.IsActive())
            {
                if (delay > 0f) label += "（延迟 " + FormatSeconds(delay) + " 秒";
                else if (duration > 0f) label += "（持续 " + FormatSeconds(duration) + " 秒";
                if (delay > 0f && duration > 0f) label += "，持续 " + FormatSeconds(duration) + " 秒";
                if (delay > 0f || duration > 0f) label += "）";
                return label;
            }

            if (delay > 0f) label += " (after " + FormatSeconds(delay) + "s delay";
            else if (duration > 0f) label += " (" + FormatSeconds(duration) + "s";
            if (delay > 0f && duration > 0f) label += ", over " + FormatSeconds(duration) + "s";
            if (delay > 0f || duration > 0f) label += ")";
            return label;
        }

        private static string QualifyActionLabel(MonoBehaviour action, string label)
        {
            List<string> qualifiers = null;
            if (IsMassAfflictionAction(action) && TryReadMassTarget(action, out float radius, out bool ignoreCaster))
            {
                string range = radius.ToString("0.###", CultureInfo.InvariantCulture);
                (qualifiers ??= new List<string>()).Add(Zh.IsActive()
                    ? "物品周围 " + range + " 米内的玩家" : "characters within " + range + "m of item");
                if (ignoreCaster) qualifiers.Add(Zh.IsActive() ? "排除持有者" : "holder excluded");
            }
            if (action != null && TryReadBoolField(action, "ifSkeleton", out bool ifSkeleton) && ifSkeleton)
                (qualifiers ??= new List<string>()).Add(Zh.IsActive() ? "仅骷髅" : "skeleton only");

            if (action is ItemActionBase)
            {
                bool hasNonConsumeTrigger = false;
                foreach (string trigger in NonConsumeTriggerFields)
                {
                    if (TryReadBoolField(action, trigger, out bool enabled) && enabled)
                    {
                        hasNonConsumeTrigger = true;
                        break;
                    }
                }

                if (hasNonConsumeTrigger)
                {
                    foreach (string trigger in TriggerFields)
                    {
                        if (!TryReadBoolField(action, trigger, out bool enabled) || !enabled) continue;
                        (qualifiers ??= new List<string>()).Add(TriggerLabel(trigger));
                    }
                }
            }

            if (qualifiers == null || qualifiers.Count == 0) return label;
            return Zh.IsActive()
                ? label + "【" + string.Join("、", qualifiers) + "】"
                : label + " [" + string.Join(", ", qualifiers) + "]";
        }

        private static string TriggerLabel(string trigger)
        {
            if (Zh.IsActive())
            {
                switch (trigger)
                {
                    case "OnPressed": return "按下时";
                    case "OnHeld": return "按住时";
                    case "OnCastFinished": return "主动作结束时";
                    case "OnCancelled": return "主动作取消时";
                    case "OnSecondaryCastFinished": return "副动作结束时";
                    case "OnSecondaryPressed": return "副动作按下时";
                    case "OnSecondaryHeld": return "副动作按住时";
                    case "OnSecondaryCancelled": return "副动作取消时";
                    case "OnConsumed": return "消耗时";
                }
            }

            switch (trigger)
            {
                case "OnPressed": return "on press";
                case "OnHeld": return "while held";
                case "OnCastFinished": return "on primary cast finish";
                case "OnCancelled": return "on primary cancel";
                case "OnSecondaryCastFinished": return "on secondary cast finish";
                case "OnSecondaryPressed": return "on secondary press";
                case "OnSecondaryHeld": return "while secondary held";
                case "OnSecondaryCancelled": return "on secondary cancel";
                case "OnConsumed": return "on consume";
                default: return trigger;
            }
        }

        private static string TimedStatusLabel(CharacterAfflictions.STATUSTYPE status, float duration)
        {
            return TimedStatusLabel(StatusName(status), duration);
        }

        private static string TimedStatusLabel(string label, float duration)
        {
            if (duration <= 0f) return label;
            return Zh.IsActive()
                ? label + "（持续 " + FormatSeconds(duration) + " 秒）"
                : label + " (over " + FormatSeconds(duration) + "s)";
        }

        private static string FormatSeconds(float seconds)
        {
            return seconds.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /**
         * @brief turns a petrify field name into a display label, e.g. "petrifyPerJump" -> "Petrify (per jump)"
         * @param fieldName the reflected field name, always starting with "petrify"
         * @return the humanised label
         */
        private static string PetrifyLabel(string fieldName)
        {
            string rest = fieldName.Length > "petrify".Length ? fieldName.Substring("petrify".Length) : "";
            if (string.IsNullOrEmpty(rest)) return Zh.IsActive() ? "石化" : "Petrify";

            // split the camel-cased remainder into lowercase words: "PerJump" -> "per jump"
            StringBuilder qualifier = new StringBuilder();
            foreach (char ch in rest)
            {
                if (char.IsUpper(ch) && qualifier.Length > 0) qualifier.Append(' ');
                qualifier.Append(char.ToLowerInvariant(ch));
            }

            return Zh.IsActive() ? "石化（" + QualifierZh(qualifier.ToString()) + "）" : "Petrify (" + qualifier + ")";
        }

        /**
         * @brief localised display name for a status type (zh-CN patch)
         */
        private static string StatusName(CharacterAfflictions.STATUSTYPE type)
        {
            if (!Zh.IsActive()) return type.ToString();
            switch (type)
            {
                case CharacterAfflictions.STATUSTYPE.Injury:  return "受伤";
                case CharacterAfflictions.STATUSTYPE.Hunger:  return "饥饿";
                case CharacterAfflictions.STATUSTYPE.Cold:    return "寒冷";
                case CharacterAfflictions.STATUSTYPE.Poison:  return "中毒";
                case CharacterAfflictions.STATUSTYPE.Crab:    return "螃蟹";
                case CharacterAfflictions.STATUSTYPE.Curse:   return "诅咒";
                case CharacterAfflictions.STATUSTYPE.Drowsy:  return "困倦";
                case CharacterAfflictions.STATUSTYPE.Weight:  return "负重";
                case CharacterAfflictions.STATUSTYPE.Hot:     return "炎热";
                case CharacterAfflictions.STATUSTYPE.Thorns:  return "荆棘";
                case CharacterAfflictions.STATUSTYPE.Spores:  return "孢子";
                case CharacterAfflictions.STATUSTYPE.Web:     return "蛛网";
                case CharacterAfflictions.STATUSTYPE.Arrow:   return "中箭";
                case CharacterAfflictions.STATUSTYPE.Petrify: return "石化";
                case CharacterAfflictions.STATUSTYPE.FlyTrap: return "捕蝇草";
                default: return type.ToString();
            }
        }

        /**
         * @brief localised suffix for petrify field names, e.g. "per jump" -> "每次跳跃" (zh-CN patch)
         */
        private static string QualifierZh(string qualifier)
        {
            if (!Zh.IsActive()) return qualifier;
            switch (qualifier)
            {
                case "per jump":   return "每次跳跃";
                case "per use":    return "每次使用";
                case "per throw":  return "每次投掷";
                case "per second": return "每秒";
                case "per bounce": return "每次弹跳";
                case "on hit":     return "命中时";
                case "on land":    return "落地时";
                case "on heal":    return "治疗时";
                case "on burn":    return "燃烧时";
                case "mystical":   return "神秘";
                default: return qualifier;
            }
        }

        /**
         * @brief appends one signed, coloured modifier line, scaling the 0-1 fraction to display points and skipping zero-point changes
         * @param sb the builder the line is appended to
         * @param first whether this is the first line; set to false after the first append so later lines are newline-separated
         * @param colour the colour applied to the line via a rich-text tag
         * @param signedFraction the net change on the 0-1 scale; its sign sets the leading + or -
         * @param label the status name shown after the value
         */
        private static void AppendLine(StringBuilder sb, ref bool first, Color colour,
            float signedFraction, string label)
        {
            // scale the 0-1 fraction to display points, keeping the sign
            AppendPoints(sb, ref first, colour, Mathf.RoundToInt(signedFraction * 100f), label);
        }

        /**
         * @brief appends one signed, coloured modifier line from an already-scaled points value, skipping zero
         * @param sb the builder the line is appended to
         * @param first whether this is the first line; set to false after the first append so later lines are newline-separated
         * @param colour the colour applied to the line via a rich-text tag
         * @param signedPoints the net change in display points; its sign sets the leading + or -
         * @param label the status name shown after the value
         */
        private static void AppendPoints(StringBuilder sb, ref bool first, Color colour,
            int signedPoints, string label)
        {
            if (signedPoints == 0) return;

            string sign = signedPoints > 0 ? "+" : "-";
            string hex = ColorUtility.ToHtmlStringRGB(colour);

            if (!first) sb.Append('\n');
            first = false;

            sb.Append("<color=#").Append(hex).Append('>')
              .Append(sign).Append(Mathf.Abs(signedPoints)).Append(' ').Append(label)
              .Append("</color>");
        }

        /**
         * @brief appends a coloured modifier line for a value that varies within a range, as "+low–high label",
         *        collapsing to a single value when the bounds are equal and skipping an all-zero range
         * @param sb the builder the line is appended to
         * @param first whether this is the first line; set to false after the first append
         * @param colour the colour applied to the line via a rich-text tag
         * @param lowPoints the lower bound in display points (assumed non-negative)
         * @param highPoints the upper bound in display points (assumed non-negative)
         * @param label the status name shown after the value
         */
        private static void AppendPointsRange(StringBuilder sb, ref bool first, Color colour,
            int lowPoints, int highPoints, string label)
        {
            if (lowPoints == highPoints)
            {
                AppendPoints(sb, ref first, colour, highPoints, label);
                return;
            }
            if (highPoints == 0) return;

            string hex = ColorUtility.ToHtmlStringRGB(colour);

            if (!first) sb.Append('\n');
            first = false;

            sb.Append("<color=#").Append(hex).Append('>')
              .Append('+').Append(lowPoints).Append('–').Append(highPoints).Append(' ').Append(label)
              .Append("</color>");
        }

        /**
         * @brief returns the display colour for a status type, falling back to white if it is not mapped
         * @param type the status type to colour
         * @return the mapped colour, or white when unmapped
         */
        private static Color ColourFor(CharacterAfflictions.STATUSTYPE type)
        {
            return Colours.TryGetValue(type, out Color c) ? c : Fallback;
        }

        /**
         * @brief parses a hex colour string into a colour.
         */
        private static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out Color c);
            return c;
        }
    }
}
