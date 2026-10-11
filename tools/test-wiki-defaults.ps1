$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$english = Get-Content (Join-Path $repoRoot "assets/default-descriptions.json") -Raw | ConvertFrom-Json -AsHashtable
$chinese = Get-Content (Join-Path $repoRoot "assets/default-descriptions-zh-CN.json") -Raw | ConvertFrom-Json -AsHashtable
$manifest = Get-Content (Join-Path $repoRoot "assets/wiki-page-manifest.json") -Raw | ConvertFrom-Json -AsHashtable

if ($english.items.Count -ne 131 -or $chinese.items.Count -ne 131) {
    throw "Expected 131 item keys in each language file."
}

$englishReviewed = @($english.items.Keys | Where-Object { $english.items[$_].wiki })
$chineseReviewed = @($chinese.items.Keys | Where-Object { $chinese.items[$_].wiki })
$englishProfiles = @($english.items.Keys | Where-Object { $english.items[$_].stateDescriptions -and $english.items[$_].stateDescriptions.Count -gt 0 })
$chineseProfiles = @($chinese.items.Keys | Where-Object { $chinese.items[$_].stateDescriptions -and $chinese.items[$_].stateDescriptions.Count -gt 0 })
if ($englishReviewed.Count -ne 118 -or $chineseReviewed.Count -ne 118) {
    throw "Expected 118 fact-reviewed item pages in each language file."
}
if ($englishProfiles.Count -ne 67 -or $chineseProfiles.Count -ne 67) {
    throw "Expected 67 bilingual cooking-state profiles."
}
if ((Compare-Object $englishReviewed $chineseReviewed -CaseSensitive) -or
    (Compare-Object $englishProfiles $chineseProfiles -CaseSensitive)) {
    throw "English and Chinese reviewed keys or cooking profiles differ."
}

$cookingSpecialKeys = @(
    "anti-rope cannon", "anti-rope spool", "VOIDLAUNCHER", "Antidote", "Backpack", "Balloon",
    "Balloon Bunch", "Bandages", "Big Lollipop", "Bing Bong", "Blowgun", "Bugle", "Chain Launcher",
    "Cure-All", "Cursed Skull", "Dynamite", "Energy Drink", "Faerie Lantern", "First Aid Kit", "Flare",
    "Fortified Milk", "Guidebook", "Heat Pack", "Lantern", "Mandrake", "Medicinal Root", "Pandora's Lunchbox",
    "Piton", "Portable Stove", "Rescue Claw", "rope cannon", "rope spool", "Scout Cannon", "Sports Drink",
    "Sunscreen", "THEBOOKOFBONES", "Big Egg", "Beehive", "Bird", "Checkpoint Flag", "Coconut Half",
    "Scoutmaster's Bugle", "Scroll", "Scout Effigy", "ScoutsHonor", "Scorpion", "Strange Gem", "SNOWBALL", "EARLYWORM",
    "Frog", "FrogLegs", "Red Crispberry", "Small Egg"
)
foreach ($key in $cookingSpecialKeys) {
    if (-not $english.items[$key].wiki -or -not $english.items[$key].stateDescriptions) {
        throw "Cooking special item '$key' has no sourced state profile."
    }
}

$requiredStates = @{
    "Big Egg" = @("cooked", "burnt", "incinerated")
    "Beehive" = @("cooked", "burnt", "incinerated")
    "Bird" = @("cooked", "burnt", "incinerated")
    "Checkpoint Flag" = @("cooked")
    "Coconut Half" = @("cooked")
    "Scoutmaster's Bugle" = @("incinerated")
    "Scroll" = @("cooked")
    "Scout Effigy" = @("cooked", "incinerated")
    "ScoutsHonor" = @("cooked")
    "Scorpion" = @("cooked")
    "Strange Gem" = @("cooked", "incinerated")
    "SNOWBALL" = @("cooked")
    "EARLYWORM" = @("cooked", "burnt", "incinerated")
    "Frog" = @("cooked")
    "FrogLegs" = @("raw", "cooked")
    "Red Crispberry" = @("cooked")
    "Small Egg" = @("cooked")
}
foreach ($key in $requiredStates.Keys) {
    foreach ($state in $requiredStates[$key]) {
        if (-not $english.items[$key].stateDescriptions.ContainsKey($state)) {
            throw "Cooking profile '$key' is missing the required '$state' state."
        }
    }
}
if ($english.items["Gold Prickleberry"].wiki) {
    throw "Gold Prickleberry has conflicting Wiki cooking facts and must remain unprofiled."
}

$manifestItems = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($entry in $manifest.items) { $manifestItems[$entry.gameKey] = $entry }
if ($manifestItems["Small Egg"].wikiTitle -ne "Small Egg") {
    throw "Small Egg must map to its own Wiki page, not Egg."
}
if (-not $english.items["Small Egg"].stateDescriptions.ContainsKey("cooked")) {
    throw "Small Egg must describe the cooked result when cracked."
}
if ($manifestItems["Fannypack"].wikiTitle -ne "Fanny Pack" -or $manifestItems["Frisbee"].wikiTitle -ne "Flying Disc") {
    throw "Renamed item aliases do not resolve to their current Wiki pages."
}
if ($manifestItems["Gold Prickleberry"].status -ne "source-conflict; in-game-resolution-required" -or
    -not $manifestItems["Gold Prickleberry"].reviewNote) {
    throw "Gold Prickleberry's conflicting source notes must be explicit in the manifest."
}
$unreviewedDirect = @($manifest.items | Where-Object { $_.status -eq "page-found-needs-content-review" -or $_.status -eq "page-revision-changed-needs-review" }).Count
$sourceConflicts = @($manifest.items | Where-Object { $_.status -like "source-conflict;*" }).Count
$missingSources = @($manifest.items | Where-Object { $_.status -eq "missing" }).Count
if ($unreviewedDirect -ne 0 -or $sourceConflicts -ne 1 -or $missingSources -ne 0) {
    throw "Expected no unreviewed direct pages, one documented source conflict, and no items without a source."
}
if ($manifestItems["Green Clusterberry"].status -ne "no-direct-item-page; secondary-source-reviewed" -or
    $manifestItems["Green Clusterberry"].secondarySources[0].revision -ne 11407 -or
    $manifestItems["Green Clusterberry"].secondarySources[0].section -ne "Green Clusterberry") {
    throw "Green Clusterberry must cite the reviewed Unused content section."
}
if ($english.items["Green Clusterberry"].description -notmatch "Unused item.*Hunger restoration.*Poison removal.*undocumented" -or
    $chinese.items["Green Clusterberry"].description -notmatch "未使用物品.*恢复饥饿.*移除中毒.*未说明烹饪效果") {
    throw "Green Clusterberry's bilingual description must use only the sourced base facts and leave cooking undocumented."
}

foreach ($key in @("AMULET_CLONE", "AMULET_DOUBLEJUMP", "AMULET_HEALING", "AMULET_INFINITESTAM")) {
    if (-not $manifestItems[$key].wikiTitle) { throw "Amulet key '$key' has no resolved Wiki source." }
}

$secondaryItems = @($manifest.items | Where-Object { $_.status -like "no-direct-item-page;*" })
$secondaryReviewed = @($secondaryItems | Where-Object { $_.status -eq "no-direct-item-page; secondary-source-reviewed" })
if ($secondaryItems.Count -ne 12 -or $secondaryReviewed.Count -ne 12) {
    throw "All 12 items without direct pages must have reviewed secondary sources."
}
foreach ($entry in $secondaryReviewed) {
    if (-not $entry.reviewNote -or -not $entry.secondarySources) {
        throw "Secondary source review is incomplete for '$($entry.gameKey)'."
    }
    foreach ($source in $entry.secondarySources) {
        if ($source.revision -ne $source.currentRevision) {
            throw "Secondary source revision is stale for '$($entry.gameKey)' ($($source.title))."
        }
    }
}
if ($manifestItems["SCOUTMASTERSOUL"].secondarySources[0].title -ne "Nadir") {
    throw "Scoutmaster's Soul must point to its current Nadir section, not the old redirect title."
}

foreach ($key in $englishReviewed) {
    $en = $english.items[$key]
    $zh = $chinese.items[$key]
    if (($en.wiki.revision -ne $zh.wiki.revision) -or
        ($en.wiki.title -ne $zh.wiki.title) -or
        ($en.wiki.cookingRevision -ne 11785) -or
        ($zh.wiki.cookingRevision -ne 11785) -or
        (-not $en.wiki.reviewStatus) -or (-not $zh.wiki.reviewStatus)) {
        throw "Wiki provenance is missing or inconsistent for '$key'."
    }
    if (-not $manifestItems.ContainsKey($key) -or $manifestItems[$key].revision -ne $en.wiki.revision) {
        throw "Manifest page revision does not match the curated profile for '$key'."
    }
    if ($manifestItems[$key].status -ne "facts-reviewed; in-game-validation-pending") {
        throw "Manifest review status is stale for '$key'."
    }
}

foreach ($key in $englishProfiles) {
    $en = $english.items[$key]
    $zh = $chinese.items[$key]
    if (Compare-Object @($en.stateDescriptions.Keys) @($zh.stateDescriptions.Keys) -CaseSensitive) {
        throw "State keys differ by language for '$key'."
    }
    $enCheckKeys = if ($en.stateChecks) { @($en.stateChecks.Keys) } else { @() }
    $zhCheckKeys = if ($zh.stateChecks) { @($zh.stateChecks.Keys) } else { @() }
    if ((@($enCheckKeys | Where-Object { $_ -notin $zhCheckKeys }).Count -gt 0) -or
        (@($zhCheckKeys | Where-Object { $_ -notin $enCheckKeys }).Count -gt 0)) {
        throw "State checks differ by language for '$key'."
    }
}
if ($english.items["Red Crispberry"].stateDescriptions.cooked -notmatch "doubles Hunger|doubling.*Hunger" -or
    $chinese.items["Red Crispberry"].stateDescriptions.cooked -notmatch "翻倍") {
    throw "Red Crispberry's cooked effect is missing in one language."
}
if ($english.items["Frog"].stateDescriptions.cooked -notmatch "Frog Legs" -or
    $chinese.items["Frog"].stateDescriptions.cooked -notmatch "青蛙腿") {
    throw "Frog's cooked transformation is missing in one language."
}

if ($english.items["Green Crispberry"].stateDescriptions.ContainsKey("unusable")) {
    throw "Green Crispberry must not claim that cooking destroys it."
}

[PSCustomObject]@{
    EnglishItems = $english.items.Count
    ChineseItems = $chinese.items.Count
    FactReviewedPages = $englishReviewed.Count
    BilingualCookingProfiles = $englishProfiles.Count
    ProfilesWithLiveChecks = @($englishProfiles | Where-Object { $english.items[$_].stateChecks.Count -gt 0 }).Count
    ReviewedSecondarySourceItems = $secondaryReviewed.Count
    Result = "PASS"
} | ConvertTo-Json
