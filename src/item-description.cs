using System;
using System.Collections.Generic;

namespace PeakItemTooltip
{
    /// <summary>
    /// Values used to render one tooltip refresh. Capturing the state and modifier text together
    /// prevents the description and numeric rows from observing different cooking updates.
    /// </summary>
    public sealed class TooltipItemSnapshot
    {
        public readonly string CookingState;
        public readonly string Modifiers;
        public readonly HashSet<string> ActivePositiveStatuses;

        private TooltipItemSnapshot(string cookingState, string modifiers, HashSet<string> activePositiveStatuses)
        {
            CookingState = cookingState;
            Modifiers = modifiers;
            ActivePositiveStatuses = activePositiveStatuses;
        }

        public static TooltipItemSnapshot Capture(Item item)
        {
            if (item == null) return new TooltipItemSnapshot("raw", "", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return new TooltipItemSnapshot(ItemDescription.StateFor(item), ItemModifiers.Format(item), ItemModifiers.ActivePositiveStatuses(item));
        }

        public bool HasActivePositiveStatus(string status) =>
            !string.IsNullOrEmpty(status) && ActivePositiveStatuses.Contains(status);
    }

    public static class ItemDescription
    {
        public static string Format(Item item, string baseDescription, string context,
            IDictionary<string, string> stateDescriptions)
        {
            if (item == null) return baseDescription;

            bool hasCuratedProfile = !string.IsNullOrWhiteSpace(context)
                || (stateDescriptions != null && stateDescriptions.Count > 0);
            return FormatForState(StateFor(item), baseDescription, context, stateDescriptions, hasCuratedProfile);
        }

        public static string FormatSnapshot(TooltipItemSnapshot snapshot, string baseDescription, string context,
            IDictionary<string, string> stateDescriptions, IDictionary<string, StateCheck> stateChecks)
        {
            if (snapshot == null) return baseDescription;

            string state = snapshot.CookingState;
            string stateDescription = null;
            if (stateDescriptions != null) stateDescriptions.TryGetValue(state, out stateDescription);

            if (stateChecks != null && stateChecks.TryGetValue(state, out StateCheck check)
                && check != null && check.MustNotHaveActive != null)
            {
                foreach (string status in check.MustNotHaveActive)
                {
                    if (snapshot.HasActivePositiveStatus(status))
                    {
                        stateDescription = null;
                        break;
                    }
                }
            }

            bool hasCuratedProfile = !string.IsNullOrWhiteSpace(context)
                || (stateDescriptions != null && stateDescriptions.Count > 0);
            return FormatForState(state, baseDescription, context, stateDescription, hasCuratedProfile);
        }

        private static string FormatForState(string state, string baseDescription, string context,
            IDictionary<string, string> stateDescriptions, bool hasCuratedProfile)
        {
            string stateDescription = null;
            if (stateDescriptions != null) stateDescriptions.TryGetValue(state, out stateDescription);
            return FormatForState(state, baseDescription, context, stateDescription, hasCuratedProfile);
        }

        private static string FormatForState(string state, string baseDescription, string context,
            string stateDescription, bool hasCuratedProfile)
        {
            if (!hasCuratedProfile) return baseDescription;

            string stableContext = state == "raw" && string.IsNullOrWhiteSpace(context) ? baseDescription : context;
            if (string.IsNullOrWhiteSpace(stateDescription))
            {
                // Preserve legacy text for raw items. For an item with a curated profile, cooked
                // states show only its verified neutral context, never an unclassified raw claim.
                return state == "raw" ? baseDescription : context;
            }

            if (string.IsNullOrWhiteSpace(stableContext)) return stateDescription;
            return stableContext.TrimEnd() + " " + stateDescription.TrimStart();
        }

        public static string StateFor(Item item)
        {
            ItemCooking cooking = item != null ? item.cooking : null;
            if (cooking == null || cooking.timesCookedLocal <= 0) return "raw";
            if (cooking.wreckWhenCooked) return "unusable";
            if (cooking.timesCookedLocal >= 4) return "incinerated";
            if (cooking.timesCookedLocal == 3) return "burnt";
            return "cooked";
        }
    }
}
