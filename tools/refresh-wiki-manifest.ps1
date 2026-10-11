param(
    [string]$GameVersion = "2.6.b",
    [int]$BatchSize = 40
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$defaultsPath = Join-Path $repoRoot "assets/default-descriptions.json"
$manifestPath = Join-Path $repoRoot "assets/wiki-page-manifest.json"
$api = "https://peak.wiki.gg/api.php"
$userAgent = "PEAK-Item-Tooltip-Wiki-Indexer/1.0 (open-source mod research)"

$defaults = Get-Content -LiteralPath $defaultsPath -Raw | ConvertFrom-Json -AsHashtable
$keys = @($defaults.items.Keys | Sort-Object)

# Wiki display/page names that intentionally differ from the game's internal keys.
$aliases = @{
    "Bird" = "Cooked Bird"
    "Coconut Half" = "Half-Coconut"
    "EARLYWORM" = "The Early Worm"
    "FrogLegs" = "Frog Legs"
    "Small Egg" = "Egg"
    "anti-rope cannon" = "Anti-Rope Cannon"
    "anti-rope spool" = "Anti-Rope Spool"
    "RitualDagger" = "Ritual Dagger"
    "rope cannon" = "Rope Cannon"
    "rope spool" = "Rope Spool"
    "ScoutsHonor" = "Scout's Honor"
    "SNOWBALL" = "Snowball"
    "THEBOOKOFBONES" = "The Book of Bones"
    "VOIDLAUNCHER" = "Anti-Zooka"
}

$entries = [System.Collections.Generic.List[object]]::new()
for ($offset = 0; $offset -lt $keys.Count; $offset += $BatchSize) {
    $end = [Math]::Min($offset + $BatchSize - 1, $keys.Count - 1)
    $batch = @($keys[$offset..$end])
    $requestedTitles = @($batch | ForEach-Object { if ($aliases.ContainsKey($_)) { $aliases[$_] } else { $_ } })
    $query = @{
        action = "query"
        format = "json"
        formatversion = "2"
        prop = "info|revisions"
        rvprop = "ids|timestamp"
        rvslots = "main"
        titles = ($requestedTitles -join "|")
    }

    $response = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-RestMethod -Method Get -Uri $api -Body $query -Headers @{ "User-Agent" = $userAgent } -TimeoutSec 30
            break
        }
        catch {
            if ($attempt -eq 3) { throw "Wiki API request failed for batch starting at item ${offset}: $($_.Exception.Message)" }
            Start-Sleep -Seconds $attempt
        }
    }

    $canonicalByAlias = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($rename in @($response.query.normalized) + @($response.query.converted) + @($response.query.redirects)) {
        if ($rename.from -and $rename.to) { $canonicalByAlias[$rename.from] = $rename.to }
    }

    $pageByTitle = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($page in $response.query.pages) { $pageByTitle[$page.title] = $page }

    foreach ($gameKey in $batch) {
        $requestedTitle = if ($aliases.ContainsKey($gameKey)) { $aliases[$gameKey] } else { $gameKey }
        $resolvedTitle = $requestedTitle
        if ($canonicalByAlias.ContainsKey($resolvedTitle)) { $resolvedTitle = $canonicalByAlias[$resolvedTitle] }
        $page = if ($pageByTitle.ContainsKey($resolvedTitle)) { $pageByTitle[$resolvedTitle] } else { $null }

        if ($null -eq $page -or $page.missing) {
            $entries.Add([ordered]@{ gameKey = $gameKey; requestedTitle = $requestedTitle; wikiTitle = $null; revision = $null; timestamp = $null; status = "missing" })
        }
        else {
            $revision = if ($page.revisions -and $page.revisions.Count -gt 0) { $page.revisions[0] } else { $null }
            $defaultItem = $defaults.items[$gameKey]
            $profile = if ($defaultItem -and $defaultItem.wiki) { $defaultItem.wiki } else { $null }
            $reviewStatus = $null
            $entryStatus = if ($revision) { "page-found-needs-content-review" } else { "page-found-no-revision" }
            if ($revision -and $profile -and $profile.revision -eq $revision.revid) {
                $reviewStatus = $profile.reviewStatus
                $entryStatus = "facts-reviewed; in-game-validation-pending"
            }
            elseif ($revision -and $profile) {
                $entryStatus = "page-revision-changed-needs-review"
            }
            $entries.Add([ordered]@{
                gameKey = $gameKey
                requestedTitle = $requestedTitle
                wikiTitle = $page.title
                revision = if ($revision) { $revision.revid } else { $null }
                timestamp = if ($revision) { $revision.timestamp } else { $null }
                status = $entryStatus
                reviewStatus = $reviewStatus
            })
        }
    }

    if ($end -lt $keys.Count - 1) { Start-Sleep -Milliseconds 500 }
}

$manifest = [ordered]@{
    source = "PEAK Wiki (community-maintained)"
    api = $api
    retrievalDateUtc = [DateTime]::UtcNow.ToString("yyyy-MM-dd")
    checkedGameVersion = $GameVersion
    reuseNotice = "Wiki page text is CC BY-SA 4.0 unless the individual page states otherwise; verify each source page."
    reviewPolicy = "A page lookup is only a candidate. Read and compare its item facts and cooking notes before using it in a tooltip."
    aliases = $aliases
    items = $entries
}

$json = ConvertTo-Json -InputObject $manifest -Depth 8
[IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$unreviewed = @($entries | Where-Object { $_.status -eq "page-found-needs-content-review" }).Count
$reviewed = @($entries | Where-Object { $_.status -eq "facts-reviewed; in-game-validation-pending" }).Count
$missing = @($entries | Where-Object { $_.status -eq "missing" }).Count
[PSCustomObject]@{ Path = $manifestPath; GameItems = $entries.Count; FactReviewedPages = $reviewed; UnreviewedCandidatePages = $unreviewed; MissingPages = $missing; InGameValidationPending = $true } | ConvertTo-Json
