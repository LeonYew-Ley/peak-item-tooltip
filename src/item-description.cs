namespace PeakItemTooltip
{
    public static class ItemDescription
    {
        public static string Format(Item item, string baseDescription)
        {
            int cooked = item != null && item.cooking != null ? item.cooking.timesCookedLocal : 0;
            if (cooked <= 0) return baseDescription;

            // Authored descriptions describe the base item. Cooking can remove or change those
            // effects, so do not present raw-food warnings as facts about this cooked instance.
            return Zh.IsActive()
                ? $"已烹饪 {cooked} 次。效果随烹饪状态变化。"
                : $"Cooked {cooked} time(s). Effects depend on the current cooking state.";
        }
    }
}
