using System;
using PeakItemTooltip;
using UnityEngine;

static class Program
{
    private static int checks;

    static void Main()
    {
        // The same instance is refreshed as the game mutates it during cooking.
        var berry = new Item();
        var hunger = berry.Add(new Action_RestoreHunger { restorationAmount = .2f });
        var poison = berry.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f });
        Has(ItemModifiers.Format(berry), "-20 Hunger", "raw hunger");
        Has(ItemModifiers.Format(berry), "+10 Poison", "raw poison");
        Equal(ItemDescription.Format(berry, "Raw warning"), "Raw warning", "raw description");

        berry.cooking = new ItemCooking { timesCookedLocal = 1 };
        hunger.restorationAmount = .4f;
        poison.enabled = false;
        var stamina = berry.Add(new Action_GiveExtraStamina { amount = .1f });
        Has(ItemModifiers.Format(berry), "-40 Hunger", "cooking uses mutated hunger");
        Has(ItemModifiers.Format(berry), "+10 Extra Stamina", "cooking adds stamina");
        Lacks(ItemModifiers.Format(berry), "Poison", "cooking disables poison");
        Lacks(ItemDescription.Format(berry, "Raw warning"), "Raw warning", "cooked description suppresses raw warning");
        Has(ItemDescription.Format(berry, "Raw warning"), "Cooked 1", "cooked description reports count");

        berry.cooking.timesCookedLocal = 4;
        hunger.restorationAmount = .3f;
        stamina.amount = 0f;
        berry.Add(new Action_ModifyStatus { statusType = CharacterAfflictions.STATUSTYPE.Poison, changeAmount = .1f });
        Has(ItemModifiers.Format(berry), "+10 Poison", "burnt poison remains visible");
        Lacks(ItemModifiers.Format(berry), "Extra Stamina", "burnt zero stamina is omitted");
        Has(ItemDescription.Format(berry, "Raw warning"), "Cooked 4", "repeated cooking updates description");

        // Not all foods lose poison when cooked: obey the live component, not a blanket rule.
        var stillPoisonous = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        stillPoisonous.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f });
        Has(ItemModifiers.Format(stillPoisonous), "+10 Poison", "enabled cooked poison remains visible");

        var disabled = new Item();
        disabled.Add(new Action_RestoreHunger { restorationAmount = .2f, enabled = false });
        disabled.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f, enabled = false });
        disabled.Add(new Action_ModifyStatus { statusType = CharacterAfflictions.STATUSTYPE.Weight, changeAmount = .1f, enabled = false });
        disabled.Add(new Action_GiveExtraStamina { amount = .1f, enabled = false });
        disabled.Add(new PetrifySource { petrifyPerJump = 10, enabled = false });
        disabled.Add(new Peak.Action_HealingGem { minPetrify = .1f, maxPetrify = .2f, enabled = false });
        Equal(ItemModifiers.Format(disabled), "", "all disabled effect families omitted");

        var hierarchy = new Item();
        var parent = new GameObject(hierarchy.transform);
        var child = new GameObject(parent.transform);
        hierarchy.Add(new Action_RestoreHunger { restorationAmount = .2f }, child.transform);
        Has(ItemModifiers.Format(hierarchy), "-20 Hunger", "active nested branch");
        parent.activeSelf = false;
        Equal(ItemModifiers.Format(hierarchy), "", "inactive ancestor suppresses child effect");
        parent.activeSelf = true;
        child.activeSelf = false;
        Equal(ItemModifiers.Format(hierarchy), "", "inactive child suppresses effect");
        child.activeSelf = true;
        hierarchy.transform.gameObject.activeSelf = false;
        Has(ItemModifiers.Format(hierarchy), "-20 Hunger", "inactive inventory root retains valid effects");

        var duration = new Item();
        var timed = duration.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 0f });
        Equal(ItemModifiers.Format(duration), "", "zero poison duration");
        timed.inflictionTime = -1f;
        Equal(ItemModifiers.Format(duration), "", "negative duration does not become poison recovery");
        timed.inflictionTime = 4f;
        Has(ItemModifiers.Format(duration), "+20 Poison", "live poison duration");

        var amulet = new Item();
        amulet.Add(new PetrifySource { petrifyPerJump = 10 });
        amulet.Add(new Peak.Action_HealingGem { minPetrify = .1f, maxPetrify = .2f });
        Has(ItemModifiers.Format(amulet), "+10 Petrify (per jump)", "enabled petrify unchanged");
        Has(ItemModifiers.Format(amulet), "+10–20 Petrify", "enabled healing gem unchanged");

        Zh.Active = true;
        Has(ItemModifiers.Format(berry), "+10 中毒", "localized live effects");
        Has(ItemDescription.Format(berry, "生食会中毒"), "已烹饪 4 次", "localized cooked description");
        Lacks(ItemDescription.Format(berry, "生食会中毒"), "中毒", "localized raw warning suppressed");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking() }, "自定义描述"), "自定义描述", "custom raw description preserved");
        Equal(ItemModifiers.Format(null), "", "null item modifiers");
        Equal(ItemDescription.Format(null, "Fallback"), "Fallback", "null item description");

        Console.WriteLine($"PASS: {checks} regression checks");
    }

    private static void Equal(string actual, string expected, string label)
    {
        if (actual != expected) throw new Exception($"{label}: expected '{expected}', got '{actual}'");
        checks++;
    }
    private static void Has(string actual, string expected, string label)
    {
        if (!actual.Contains(expected)) throw new Exception($"{label}: missing '{expected}' in '{actual}'");
        checks++;
    }
    private static void Lacks(string actual, string excluded, string label)
    {
        if (actual.Contains(excluded)) throw new Exception($"{label}: unexpected '{excluded}' in '{actual}'");
        checks++;
    }
}
