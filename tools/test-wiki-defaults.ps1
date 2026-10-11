$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$english = Get-Content (Join-Path $repoRoot "assets/default-descriptions.json") -Raw | ConvertFrom-Json -AsHashtable
$chinese = Get-Content (Join-Path $repoRoot "assets/default-descriptions-zh-CN.json") -Raw | ConvertFrom-Json -AsHashtable
$manifest = Get-Content (Join-Path $repoRoot "assets/wiki-page-manifest.json") -Raw | ConvertFrom-Json -AsHashtable

if ($english.items.Count -ne 131 -or $chinese.items.Count -ne 131) {
    throw "Expected 131 item keys in each language file."
}

$englishProfiles = @($english.items.Keys | Where-Object { $english.items[$_].wiki })
$chineseProfiles = @($chinese.items.Keys | Where-Object { $chinese.items[$_].wiki })
if ($englishProfiles.Count -ne 50 -or $chineseProfiles.Count -ne 50) {
    throw "Expected 50 reviewed state profiles in each language file."
}
if (Compare-Object $englishProfiles $chineseProfiles -CaseSensitive) {
    throw "English and Chinese state profile keys differ."
}

$cookingSpecialKeys = @(
    "anti-rope cannon", "anti-rope spool", "VOIDLAUNCHER", "Antidote", "Backpack", "Balloon",
    "Balloon Bunch", "Bandages", "Big Lollipop", "Bing Bong", "Blowgun", "Bugle", "Chain Launcher",
    "Cure-All", "Cursed Skull", "Dynamite", "Energy Drink", "Faerie Lantern", "First Aid Kit", "Flare",
    "Fortified Milk", "Guidebook", "Heat Pack", "Lantern", "Mandrake", "Medicinal Root", "Pandora's Lunchbox",
    "Piton", "Portable Stove", "Rescue Claw", "rope cannon", "rope spool", "Scout Cannon", "Sports Drink",
    "Sunscreen", "THEBOOKOFBONES"
)
foreach ($key in $cookingSpecialKeys) {
    if (-not $english.items[$key].wiki -or -not $english.items[$key].stateDescriptions) {
        throw "Cooking special item '$key' has no sourced state profile."
    }
}

$manifestItems = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($entry in $manifest.items) { $manifestItems[$entry.gameKey] = $entry }

foreach ($key in $englishProfiles) {
    $en = $english.items[$key]
    $zh = $chinese.items[$key]
    if (Compare-Object @($en.stateDescriptions.Keys) @($zh.stateDescriptions.Keys) -CaseSensitive) {
        throw "State keys differ by language for '$key'."
    }
    $enCheckKeys = if ($en.stateChecks) { @($en.stateChecks.Keys) } else { @() }
    $zhCheckKeys = if ($zh.stateChecks) { @($zh.stateChecks.Keys) } else { @() }
    $checkKeysMissingFromZh = @($enCheckKeys | Where-Object { $_ -notin $zhCheckKeys })
    $checkKeysMissingFromEn = @($zhCheckKeys | Where-Object { $_ -notin $enCheckKeys })
    if ($checkKeysMissingFromZh.Count -gt 0 -or $checkKeysMissingFromEn.Count -gt 0) {
        throw "State checks differ by language for '$key'."
    }
    if (($en.wiki.revision -ne $zh.wiki.revision) -or
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

if ($english.items["Green Crispberry"].stateDescriptions.ContainsKey("unusable")) {
    throw "Green Crispberry must not claim that cooking destroys it."
}

[PSCustomObject]@{
    EnglishItems = $english.items.Count
    ChineseItems = $chinese.items.Count
    BilingualWikiProfiles = $englishProfiles.Count
    ProfilesWithLiveChecks = @($englishProfiles | Where-Object { $english.items[$_].stateChecks.Count -gt 0 }).Count
    Result = "PASS"
} | ConvertTo-Json
