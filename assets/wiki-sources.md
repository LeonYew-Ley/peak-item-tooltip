# PEAK Wiki-derived tooltip data

The English and Simplified Chinese defaults contain short summaries for 19 items whose effects change with cooking. The prose is independently written; numeric values that the mod reads from the current item remain in the live modifier rows.

## Sources reviewed on 2026-10-11

| Source | Revision | Used for |
|---|---:|---|
| [Food](https://peak.wiki.gg/wiki/Food) | 12104 | Food acquisition and baseline effects |
| [Cooking](https://peak.wiki.gg/wiki/Cooking) | 11785 | Cooking stages, equivalent Cooked/Well-Done effects, berry and mushroom exceptions, burnt/incinerated behavior |
| [Green Crispberry](https://peak.wiki.gg/wiki/Green_Crispberry) | 10035 | Poison timing, duration, cooking removes poison |
| [Green Kingberry](https://peak.wiki.gg/wiki/Green_Kingberry) | 10039 | Immediate poison and cooked poison removal |
| [Yellow Kingberry](https://peak.wiki.gg/wiki/Yellow_Kingberry) | 10038 | Delayed poison and cooked poison removal |
| [Pink Berrynana](https://peak.wiki.gg/wiki/Pink_Berrynana) | 10041 | Poison timing, cooked poison removal, peel |
| [Red Prickleberry](https://peak.wiki.gg/wiki/Red_Prickleberry) | 10057 | Thorns and cooked removal |
| [Green Shroomberry](https://peak.wiki.gg/wiki/Green_Shroomberry) | 10678 | Random harmful effect and spore removal |
| [Blue Shroomberry](https://peak.wiki.gg/wiki/Blue_Shroomberry) | 10680 | Random harmful effect and spore removal |
| [Purple Shroomberry](https://peak.wiki.gg/wiki/Purple_Shroomberry) | 11164 | Random good or bad effect and spore removal |
| [Red Shroomberry](https://peak.wiki.gg/wiki/Red_Shroomberry) | 10677 | Random beneficial effect and spore removal |
| [Yellow Shroomberry](https://peak.wiki.gg/wiki/Yellow_Shroomberry) | 10679 | Random beneficial effect and spore removal |
| [Bugle Shroom](https://peak.wiki.gg/wiki/Bugle_Shroom) | 11906 | Poisonous variety and its delayed effect |
| [Button Shroom](https://peak.wiki.gg/wiki/Button_Shroom) | 11909 | Poisonous variety and its delayed effect |
| [Cluster Shroom](https://peak.wiki.gg/wiki/Cluster_Shroom) | 11908 | Poisonous variety and its delayed effect |
| [Medicinal Root](https://peak.wiki.gg/wiki/Medicinal_Root) | 11876 | Cooking removes an additional 20 Poison and Spores |
| [Mandrake](https://peak.wiki.gg/wiki/Mandrake) | 11458 | Raw numb effect, cooking stops screams, cooked Curse removal |
| [Big Lollipop](https://peak.wiki.gg/wiki/Big_Lollipop) | 10558 | Cooking removes its post-use Drowsy penalty |
| [Energy Drink](https://peak.wiki.gg/wiki/Energy_Drink) | 11234 | Cooked explosion, longer speed effect, Drowsy behavior |
| [Fortified Milk](https://peak.wiki.gg/wiki/Fortified_Milk) | 11633 | First-cook invulnerability duration increase |
| [Sports Drink](https://peak.wiki.gg/wiki/Sports_Drink) | 11351 | First-cook stamina increase and no later-stage penalty |

## Review status and limits

All 19 profiles have been checked against the cited Wiki pages and the general cooking rules. In-game validation is still pending. The Green Crispberry rule was also compared with the locally installed PEAK 2.6.b cooking implementation; the remaining special-item rules have not yet been confirmed in a live game session.

The Wiki states that Cooked and Well-Done food effects are identical and do not stack; cooked poisonous berries become safe, but mushroom poison cannot be cooked out; Shroomberries lose 5 Spores; Burnt food restores less Hunger; and each further Incinerated stage adds Poison. Sports Drink and several other items have exceptions, so the tooltip uses item-specific text only where a page documents the exception.

Gold Prickleberry is deliberately left out of this batch: its page's infobox says cooking removes Poison, while its prose says cooking removes Thorns. That conflict needs an in-game check before adding a state-specific claim. Other indexed pages remain candidates until their facts are reviewed; the page index is not an accuracy claim.

PEAK Wiki pages are community-maintained. The site identifies Wiki text as CC BY-SA 4.0 unless an individual page says otherwise. This repository keeps source links and revision IDs with each derived profile; confirm the notice on any page added later. The repository's MIT code license does not replace the terms applying to Wiki-derived data.

The maintenance index is generated with `tools/refresh-wiki-manifest.ps1` from the MediaWiki API at `https://peak.wiki.gg/api.php`. The game does not access the Wiki at runtime.
