using System;
using PeakItemTooltip;
using UnityEngine;

static class Program
{
    private static int checks;

    static void Main()
    {
        var states = new System.Collections.Generic.Dictionary<string, string>
        {
            ["raw"] = "Raw poison starts later.",
            ["cooked"] = "Removes poison and grants stamina.",
            ["burnt"] = "Stamina is gone.",
            ["incinerated"] = "Poison returns.",
            ["unusable"] = "Destroyed."
        };
        var cookedPoisonCheck = new System.Collections.Generic.Dictionary<string, StateCheck>
        {
            ["cooked"] = new StateCheck { MustNotHaveActive = new System.Collections.Generic.List<string> { "Poison" } }
        };

        // The same instance is refreshed as the game mutates it during cooking.
        var berry = new Item();
        var hunger = berry.Add(new Action_RestoreHunger { restorationAmount = .2f });
        var poison = berry.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f });
        Has(ItemModifiers.Format(berry), "-20 Hunger", "raw hunger");
        Has(ItemModifiers.Format(berry), "+10 Poison", "raw poison");
        Equal(ItemDescription.Format(berry, "Found here.", "Found here.", states), "Found here. Raw poison starts later.", "raw context and state description");

        berry.cooking = new ItemCooking { timesCookedLocal = 1 };
        hunger.restorationAmount = .4f;
        poison.enabled = false;
        var stamina = berry.Add(new Action_GiveExtraStamina { amount = .1f });
        Has(ItemModifiers.Format(berry), "-40 Hunger", "cooking uses mutated hunger");
        Has(ItemModifiers.Format(berry), "+10 Extra Stamina", "cooking adds stamina");
        Lacks(ItemModifiers.Format(berry), "Poison", "cooking disables poison");
        string onceCookedDescription = ItemDescription.Format(berry, "Raw warning", "Found here.", states);
        Equal(onceCookedDescription, "Found here. Removes poison and grants stamina.", "cooked description uses current state, not stale raw warning or count");
        Lacks(onceCookedDescription, "Raw warning", "cooked description suppresses stale raw warning");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "Raw warning", "", states),
            "Removes poison and grants stamina.", "cooked description without neutral context never falls back to raw warning");
        berry.cooking.timesCookedLocal = 2;
        Equal(ItemDescription.Format(berry, "Raw warning", "Found here.", states), onceCookedDescription, "cooked stages with identical effects use identical description");

        var refresh = TooltipItemSnapshot.Capture(berry);
        berry.cooking.timesCookedLocal = 4;
        hunger.restorationAmount = .3f;
        Equal(ItemDescription.FormatSnapshot(refresh, "Raw warning", "Found here.", states, cookedPoisonCheck), onceCookedDescription, "description uses the captured cooking state");
        Has(ItemModifiers.FormatSnapshot(refresh), "-40 Hunger", "numeric rows use the same captured refresh");

        stamina.amount = 0f;
        berry.Add(new Action_ModifyStatus { statusType = CharacterAfflictions.STATUSTYPE.Poison, changeAmount = .1f });
        Has(ItemModifiers.Format(berry), "+10 Poison", "burnt poison remains visible");
        Lacks(ItemModifiers.Format(berry), "Extra Stamina", "burnt zero stamina is omitted");
        Equal(ItemDescription.Format(berry, "Raw warning", "Found here.", states), "Found here. Poison returns.", "overcooking selects its own description");

        var wrecked = new Item { cooking = new ItemCooking { timesCookedLocal = 1, wreckWhenCooked = true } };
        Equal(ItemDescription.Format(wrecked, "Raw warning", "Found here.", states), "Found here. Destroyed.", "wrecked item does not retain raw or cooked benefits");

        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking() }), "raw", "zero cook count selects raw state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }), "cooked", "first cook selects cooked state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 2 } }), "cooked", "second cook shares cooked state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 3 } }), "burnt", "third cook selects burnt state");
        Equal(ItemDescription.StateFor(new Item { cooking = new ItemCooking { timesCookedLocal = 4 } }), "incinerated", "fourth cook selects incinerated state");

        // Not all foods lose poison when cooked: obey the live component, not a blanket rule.
        var stillPoisonous = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        stillPoisonous.Add(new Action_InflictPoison { poisonPerSecond = .05f, inflictionTime = 2f });
        Has(ItemModifiers.Format(stillPoisonous), "+10 Poison", "enabled cooked poison remains visible");
        var conflictingSnapshot = TooltipItemSnapshot.Capture(stillPoisonous);
        Equal(ItemDescription.FormatSnapshot(conflictingSnapshot, "Raw poison", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here.", "a Wiki safety claim is suppressed when live poison remains active");

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
        Has(ItemDescription.Format(berry, "生食会中毒", "在雨林发现。", new System.Collections.Generic.Dictionary<string, string> { ["incinerated"] = "烧毁后再次中毒。" }), "烧毁后再次中毒", "localized state description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking() }, "自定义描述", "", states), "自定义描述 Raw poison starts later.", "custom raw description preserved with the selected state description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "旧版条目描述", "", null), "旧版条目描述", "unreviewed item retains its legacy description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "生食会中毒", "", new System.Collections.Generic.Dictionary<string, string> { ["raw"] = "Raw poison." }), "", "curated item without a cooked rule hides its unclassified raw warning");
        Equal(ItemModifiers.Format(null), "", "null item modifiers");
        Equal(ItemDescription.Format(null, "Fallback", "", states), "Fallback", "null item description");

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
