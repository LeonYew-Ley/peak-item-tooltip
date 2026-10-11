using System;
using System.IO;
using PeakItemTooltip;
using Newtonsoft.Json.Linq;
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
        var behaviorWrecked = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        behaviorWrecked.SetUsabilityOverride(false);
        Equal(ItemDescription.StateFor(behaviorWrecked), "unusable", "unusable override from CookingBehavior_Wreck selects unusable state");
        behaviorWrecked.SetUsabilityOverride(true);
        Equal(ItemDescription.StateFor(behaviorWrecked), "cooked", "explicit usable override retains cooked state");

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
            "Found here. Current effects conflict with this item-effect note.", "a Wiki safety claim is suppressed when live poison remains active");

        var coveredConsumable = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        coveredConsumable.Add(new Action_Consume());
        Equal(ItemDescription.FormatSnapshot(TooltipItemSnapshot.Capture(coveredConsumable), "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. Cooking removes poison.", "known consumption action does not suppress a verified safety claim");

        var afflictionItem = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        afflictionItem.Add(new Action_ApplyAffliction { affliction = new Peak.Afflictions.Affliction_InfiniteStamina() });
        var afflictionSnapshot = TooltipItemSnapshot.Capture(afflictionItem);
        Equal(afflictionSnapshot.SafetySourcesComplete, false, "unparsed affliction action marks safety coverage incomplete");
        Equal(ItemDescription.FormatSnapshot(afflictionSnapshot, "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. Some effect sources are unrecognized, so this claim could not be verified.",
            "unknown affliction cannot produce a false safety claim");
        Equal(ItemDescription.FormatSnapshot(afflictionSnapshot, "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking changes the effect." }, null),
            "Found here. Cooking changes the effect. Some item effects may not appear in the modifier rows.",
            "unparsed effects are disclosed even without a safety claim");

        var parsedAffliction = new Item();
        parsedAffliction.Add(new Action_ApplyAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Injury,
                statusAmount = .05f
            },
            extraAfflictions = new Peak.Afflictions.Affliction[]
            {
                new Peak.Afflictions.Affliction_PoisonOverTime
                {
                    totalTime = 6f,
                    delayBeforeEffect = 2f,
                    statusPerSecond = .025f
                }
            }
        });
        Has(ItemModifiers.Format(parsedAffliction), "+5 Injury", "ApplyAffliction status payload is shown in modifier rows");
        Has(ItemModifiers.Format(parsedAffliction), "+10 Poison (after 2s delay, over 4s)", "extra timed poison affliction includes amount and timing");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(parsedAffliction)), "Poison", "parsed positive affliction participates in safety checks");
        Equal(TooltipItemSnapshot.Capture(parsedAffliction).SafetySourcesComplete, true,
            "fully parsed direct and extra afflictions count as supported sources");

        var extendedAfflictions = new Item();
        extendedAfflictions.Add(new Action_ApplyAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustColdOverTime
            {
                statusPerSecond = -.02f,
                totalTime = 5f
            },
            extraAfflictions = new Peak.Afflictions.Affliction[]
            {
                new Peak.Afflictions.Affliction_AdjustDrowsyOverTime
                {
                    statusPerSecond = .01f,
                    totalTime = 4f
                },
                new Peak.Afflictions.Affliction_AddBonusStamina
                {
                    staminaAmount = .1f,
                    totalTime = 6f
                }
            }
        });
        Has(ItemModifiers.Format(extendedAfflictions), "-10 Cold (over 5s)", "cold affliction shows signed aggregate and duration");
        Has(ItemModifiers.Format(extendedAfflictions), "+4 Drowsy (over 4s)", "drowsy affliction shows signed aggregate and duration");
        Has(ItemModifiers.Format(extendedAfflictions), "+10 Extra Stamina (over 6s)", "bonus stamina affliction shows amount and duration");
        Equal(TooltipItemSnapshot.Capture(extendedAfflictions).SafetySourcesComplete, true,
            "supported cold, drowsy, and stamina afflictions count as covered");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(extendedAfflictions)), "Drowsy",
            "positive timed drowsy affliction participates in safety checks");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(extendedAfflictions)), "ExtraStamina",
            "bonus stamina affliction participates in safety checks");

        var inheritedAffliction = new Item();
        inheritedAffliction.Add(new Peak.Action_SuperJumpAmulet
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Poison,
                statusAmount = .1f
            },
            petrifyPerUse = .05f
        });
        Has(ItemModifiers.Format(inheritedAffliction), "+10 Poison", "derived Action_ApplyAffliction payload is rendered");
        Has(ItemModifiers.Format(inheritedAffliction), "+5 Petrify (per use)", "derived action petrify modifier is rendered");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(inheritedAffliction)), "Petrify",
            "petrify field participates in safety checks");
        Equal(TooltipItemSnapshot.Capture(inheritedAffliction).SafetySourcesComplete, true,
            "known super-jump affliction subclass is covered when its payload is supported");

        var removedStatusAffliction = new Item();
        removedStatusAffliction.Add(new Action_ApplyAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Poison,
                statusAmount = -.2f
            }
        });
        Has(ItemModifiers.Format(removedStatusAffliction), "-20 Poison", "negative ApplyAffliction status remains signed as removal");
        Lacks(string.Join(",", ItemModifiers.ActivePositiveStatuses(removedStatusAffliction)), "Poison",
            "negative ApplyAffliction status does not count as a positive risk");

        var massAffliction = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        massAffliction.Add(new Action_ApplyMassAffliction
        {
            affliction = new Peak.Afflictions.Affliction_AdjustStatus
            {
                statusType = CharacterAfflictions.STATUSTYPE.Poison,
                statusAmount = .1f
            }
        });
        var massSnapshot = TooltipItemSnapshot.Capture(massAffliction);
        Equal(massSnapshot.SafetySourcesComplete, false,
            "mass affliction remains incomplete because target and radius conditions are not rendered");
        Equal(ItemDescription.FormatSnapshot(massSnapshot, "Raw text", "Found here.",
            new System.Collections.Generic.Dictionary<string, string> { ["cooked"] = "Cooking removes poison." }, cookedPoisonCheck),
            "Found here. Some effect sources are unrecognized, so this claim could not be verified.",
            "mass action cannot bypass the safety-claim fallback");

        var randomMushroom = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        randomMushroom.Add(new Action_RandomMushroomEffect());
        Equal(TooltipItemSnapshot.Capture(randomMushroom).SafetySourcesComplete, false,
            "random mushroom action marks safety coverage incomplete");

        var customAction = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        customAction.Add(new CustomUnknownAction());
        Equal(TooltipItemSnapshot.Capture(customAction).SafetySourcesComplete, false,
            "unrecognized ItemActionBase subclass marks safety coverage incomplete");

        var disabledUnknownAction = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        disabledUnknownAction.Add(new Action_ApplyAffliction { enabled = false });
        Equal(TooltipItemSnapshot.Capture(disabledUnknownAction).SafetySourcesComplete, true,
            "disabled unknown action does not affect safety coverage");

        var conditionalStatus = new Item { cooking = new ItemCooking { timesCookedLocal = 1 } };
        conditionalStatus.Add(new Action_ModifyStatus { statusType = CharacterAfflictions.STATUSTYPE.Poison, changeAmount = .1f, ifSkeleton = true });
        Equal(TooltipItemSnapshot.Capture(conditionalStatus).SafetySourcesComplete, false,
            "character-conditional status action marks safety coverage incomplete");

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

        var thornItem = new Item();
        thornItem.Add(new Action_AddOrRemoveThorns { thornCount = 3 });
        Has(ItemModifiers.Format(thornItem), "+3 Thorns", "direct thorn action is included in modifier rows");
        Has(string.Join(",", ItemModifiers.ActivePositiveStatuses(thornItem)), "Thorns", "direct thorn action participates in safety checks");
        thornItem.components.Clear();
        thornItem.Add(new Action_AddOrRemoveThorns { thornCount = -2 });
        Has(ItemModifiers.Format(thornItem), "-2 Thorns", "negative thorn action shows thorn removal");

        Zh.Active = true;
        Has(ItemModifiers.Format(berry), "+10 中毒", "localized live effects");
        Has(ItemModifiers.Format(parsedAffliction), "+10 中毒（延迟 2 秒，持续 4 秒）", "localized ApplyAffliction timing");
        Has(ItemModifiers.Format(extendedAfflictions), "-10 寒冷（持续 5 秒）", "localized timed cold affliction");
        Has(ItemDescription.Format(berry, "生食会中毒", "在雨林发现。", new System.Collections.Generic.Dictionary<string, string> { ["incinerated"] = "烧毁后再次中毒。" }), "烧毁后再次中毒", "localized state description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking() }, "自定义描述", "", states), "自定义描述 Raw poison starts later.", "custom raw description preserved with the selected state description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "旧版条目描述", "", null), "旧版条目描述", "unreviewed item retains its legacy description");
        Equal(ItemDescription.Format(new Item { cooking = new ItemCooking { timesCookedLocal = 1 } }, "生食会中毒", "", new System.Collections.Generic.Dictionary<string, string> { ["raw"] = "Raw poison." }), "", "curated item without a cooked rule hides its unclassified raw warning");
        Equal(ItemModifiers.Format(null), "", "null item modifiers");
        Equal(ItemDescription.Format(null, "Fallback", "", states), "Fallback", "null item description");

        TestDescriptionDefaultsMerge();
        TestDescriptionDefaultsRestoreMissingSections();
        TestDescriptionsInitializeAndHotReload();

        Console.WriteLine($"PASS: {checks} regression checks");
    }

    private static void TestDescriptionDefaultsMerge()
    {
        JObject user = JObject.Parse(@"{
          'types': { 'CUSTOM': '#123456' },
          'items': {
            'green crispberry': {
              'type': 'CUSTOM',
              'description': 'User-authored legacy text',
              'customNote': 'keep this field',
              'context': '',
              'stateDescriptions': { 'raw': 'User raw text', 'cooked': '' },
              'stateChecks': {},
              'wiki': { 'title': 'User-selected source', 'customSource': 'manual' }
            },
            'My Custom Item': { 'description': 'Keep custom entry' }
          }
        }");
        JObject defaults = JObject.Parse(@"{
          'types': { 'CONSUMABLE': '#F2C43D' },
          'items': {
            'Green Crispberry': {
              'type': 'CONSUMABLE',
              'description': 'Default text must not replace user text',
              'context': 'Found in the rainforest.',
              'stateDescriptions': {
                'raw': 'Default raw text',
                'cooked': 'Default cooked text',
                'burnt': 'Default burnt text'
              },
              'stateChecks': { 'cooked': { 'mustNotHaveActive': ['Poison'] } },
              'wiki': { 'title': 'Green Crispberry', 'revision': 10035, 'gameVersion': '2.6.b' }
            },
            'Defaults Only Item': { 'description': 'New default entry' }
          }
        }");

        var added = DescriptionDefaultsMerger.MergeMissing(user, defaults);
        Equal(added.Count, 6, "migration reports all inserted fields and entries");
        Equal(user["types"]["CUSTOM"].ToString(), "#123456", "migration preserves custom type");
        Equal(user["types"]["CONSUMABLE"].ToString(), "#F2C43D", "migration adds missing type");
        Equal(user["items"]["green crispberry"]["type"].ToString(), "CUSTOM", "migration preserves user's type");
        Equal(user["items"]["green crispberry"]["description"].ToString(), "User-authored legacy text", "migration preserves legacy description");
        Equal(user["items"]["green crispberry"]["customNote"].ToString(), "keep this field", "migration preserves custom fields");
        Equal(user["items"]["green crispberry"]["context"].ToString(), "", "migration preserves explicit empty context");
        Equal(user["items"]["green crispberry"]["stateDescriptions"]["raw"].ToString(), "User raw text", "migration preserves edited state text");
        Equal(user["items"]["green crispberry"]["stateDescriptions"]["cooked"].ToString(), "", "migration preserves explicit empty state text");
        Equal(user["items"]["green crispberry"]["stateDescriptions"]["burnt"].ToString(), "Default burnt text", "migration adds missing state text");
        Equal(user["items"]["green crispberry"]["stateChecks"]["cooked"]["mustNotHaveActive"][0].ToString(), "Poison", "migration adds missing state checks");
        Equal(user["items"]["green crispberry"]["wiki"]["title"].ToString(), "User-selected source", "migration preserves existing source metadata");
        Equal(user["items"]["green crispberry"]["wiki"]["revision"].ToString(), "10035", "migration adds missing source metadata");
        Equal(user["items"]["green crispberry"]["wiki"]["customSource"].ToString(), "manual", "migration preserves custom source metadata");
        Equal(user["items"]["My Custom Item"]["description"].ToString(), "Keep custom entry", "migration preserves custom item");
        Equal(user["items"]["Defaults Only Item"]["description"].ToString(), "New default entry", "migration adds new default item");
    }

    private static void TestDescriptionDefaultsRestoreMissingSections()
    {
        JObject user = JObject.Parse(@"{ 'customRoot': true }");
        JObject defaults = JObject.Parse(@"{
          'types': { 'CONSUMABLE': '#F2C43D' },
          'items': { 'Default Item': { 'description': 'Default text' } }
        }");

        var added = DescriptionDefaultsMerger.MergeMissing(user, defaults);
        Equal(added.Count, 2, "migration reports missing root sections");
        Equal(user["types"]["CONSUMABLE"].ToString(), "#F2C43D", "migration restores missing types section");
        Equal(user["items"]["Default Item"]["description"].ToString(), "Default text", "migration restores missing items section");
        Equal(user["customRoot"].ToString(), "True", "migration preserves unrelated root fields");
    }

    private static void TestDescriptionsInitializeAndHotReload()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "peak-item-tooltip-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            Zh.Active = false;
            BepInEx.Paths.ConfigPath = Path.Combine(tempRoot, "fresh-en");
            Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            var freshEnglish = new Descriptions();
            freshEnglish.Initialize();
            Equal(File.Exists(freshEnglish.Path_), true, "first initialization creates English defaults");
            Equal(freshEnglish.TryGet("Green Crispberry", out ItemInfo freshEnInfo), true, "fresh English defaults load");
            Equal(freshEnInfo.Description, "Default English raw description.", "fresh English description loads");

            BepInEx.Paths.ConfigPath = Path.Combine(tempRoot, "legacy-en");
            Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            File.WriteAllText(Path.Combine(BepInEx.Paths.ConfigPath, Descriptions.FileNameEn),
                "{\"types\":{\"CUSTOM\":\"#123456\"},\"items\":{\"green crispberry\":{\"type\":\"CUSTOM\",\"description\":\"My custom legacy description\"}}}");
            var migratedEnglish = new Descriptions();
            migratedEnglish.Initialize();
            Equal(migratedEnglish.TryGet("GREEN CRISPBERRY", out ItemInfo migratedEnInfo), true, "legacy English entry loads case-insensitively");
            Equal(migratedEnInfo.Description, "My custom legacy description", "legacy English description survives initialization");
            Equal(migratedEnInfo.Context, "Found in the rainforest.", "migration adds missing English context");
            Equal(migratedEnInfo.StateDescriptions["cooked"], "Cooking removes poison.", "migration adds missing English state text");
            Equal(migratedEnInfo.StateChecks["cooked"].MustNotHaveActive[0], "Poison", "migration adds missing English state check");

            JObject edited = JObject.Parse(File.ReadAllText(migratedEnglish.Path_));
            edited["items"]["green crispberry"]["description"] = "HotReloaded custom text";
            File.WriteAllText(migratedEnglish.Path_, edited.ToString());
            File.SetLastWriteTimeUtc(migratedEnglish.Path_, DateTime.UtcNow.AddMinutes(1));
            migratedEnglish.MaybeReload();
            Equal(migratedEnglish.TryGet("Green Crispberry", out ItemInfo reloadedInfo), true, "HotReload retains item lookup");
            Equal(reloadedInfo.Description, "HotReloaded custom text", "HotReload reads changed custom text");

            File.WriteAllText(migratedEnglish.Path_, "{");
            File.SetLastWriteTimeUtc(migratedEnglish.Path_, DateTime.UtcNow.AddMinutes(2));
            migratedEnglish.MaybeReload();
            Equal(migratedEnglish.TryGet("Green Crispberry", out ItemInfo afterInvalidReload), true, "invalid HotReload retains prior item data");
            Equal(afterInvalidReload.Description, "HotReloaded custom text", "invalid HotReload does not discard last good text");

            Zh.Active = true;
            BepInEx.Paths.ConfigPath = Path.Combine(tempRoot, "fresh-zh");
            Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
            var freshChinese = new Descriptions();
            freshChinese.Initialize();
            Equal(freshChinese.TryGet("Green Crispberry", out ItemInfo freshZhInfo), true, "fresh Chinese defaults load");
            Equal(freshZhInfo.Description, "默认中文生食描述。", "fresh Chinese description loads");
            Equal(freshZhInfo.StateDescriptions["cooked"], "烹饪后会去除中毒。", "fresh Chinese state description loads");
        }
        finally
        {
            Zh.Active = false;
            BepInEx.Paths.ConfigPath = "";
            string tempBase = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullTempRoot = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!fullTempRoot.StartsWith(tempBase, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a test directory outside the system temp folder.");
            Directory.Delete(fullTempRoot, true);
        }
    }

    private static void Equal(string actual, string expected, string label)
    {
        if (actual != expected) throw new Exception($"{label}: expected '{expected}', got '{actual}'");
        checks++;
    }
    private static void Equal(int actual, int expected, string label)
    {
        if (actual != expected) throw new Exception($"{label}: expected '{expected}', got '{actual}'");
        checks++;
    }
    private static void Equal(bool actual, bool expected, string label)
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
