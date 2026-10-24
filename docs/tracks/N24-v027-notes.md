# Notes 24–28: the producer's playtest of v0.27.0 (2026-10-24)

Branch `phase5-notes27`. The producer played the v0.27.0 dev build at home.

| # | Note (the producer) | Cause | Fix |
|---|---|---|---|
| 24 | The gladiator select tiles ("the shield and stuff") look bugged; should look like the Codex's. | T1's admin switch rebuilds the select and arena screens; built while their CanvasGroup was at alpha 0, the new tiles kept that batch and drew washed out (a forced redraw fixed them; dirtying the graphics in the same frame didn't). | `MainMenu.OnAdminChanged` makes both screens visible for the rebuild, then shows the current screen again, as Awake builds them. |
| 25 | "The last boss seemed too easy, after a full 8 min run with the upgrades; the bosses in general are too easy." | To measure: `BossFight` now logs each fight (`[Boss] <id> down after Ns: dealt D (D/s), the player took T in H hits, HP x% left, level L`). | See "Boss tuning" below. |
| 26 | The pause menu's words aren't centred. | The labels were centred in the room right of the key chip. | `LevelUpPanel.CentreLabel`: each word centred on its whole plate, its box kept clear of the chip. |
| 27 | The Codex's "N" isn't a circle, is off centre, shouldn't sit there: smaller, in front of the text. | The seal art was an irregular wax blob with a drop-shadowed N; `PlaceSeal` put it on the plate's top-right corner at 36 units. | A symmetric 12 px disc with the N dead centre (`PixelSrc/ui_frames.py`); `PlaceSeal` puts it on the text line right after the word, like the dot after DAILY; 24 units on text plates (`UiKit.SealSizeInline`). "In front of the text" was read as beside it on the line (à frente): say if it should come before the word instead. |
| 28 | Every gold title in that font has letters going too dark. | Marcellus SC / Marcellus outlined at 0.32 SDF units on a 0.1 face: the dark rim ate the thin strokes, so gold showed only as slivers. | `UiThemeSetup`: thin fonts 0.15 outline on a 0.2 face (both Outline materials updated): the letters read gold, the rim still clear over sky and sand. |

## Boss tuning (note 25)
Measured with bot runs to each boss (dev build, `-bot -timed`, the Nobody, seed 0C0FFEE5, immortal: it never dodges;
it picks random cards, so a human's build is 2-3x stronger):

| Boss | Fight | Bot DPS | Taken (hits) | Level |
|---|---|---|---|---|
| Gorm | 95 s | 106/s | 959 (73) | 36 |
| Provincial Champion | 89 s | 152/s | 787 (63) | 29 |
| Beast Lord | 52 s | 332/s | 653 (23) | 34 |
| Gilded Patron | not done: 97 % HP after ~95 s (his guards cut damage to 40 %; the bot ignores them) | | | 31 |
| Emperor's Champion | not done: 50 % HP after ~95 s | | | 37 |
| Eternal Victor (the final boss) | 68 s | 437/s | 891 (33) | 35 |

**Changes (tested 2026-10-24: the boss PlayMode tests 65/65; EditMode 309/309 after the Victor's spec test moved to 34000):**
- `BossDef.hpPerLevel` (0.025, from level 20): a boss's HP grows with the player's level when he enters (L35 = +37 %).
- `BossDef.damageMul` (1.25): his hits, javelins, coins and bombs deal more.
- HP: Champion 13000 -> 15000, Beast Lord 15000 -> 24000, Eternal Victor 22000 -> 34000.
- Pause between attacks: Gorm 0.7 -> 0.6, Champion and Beast Lord 0.6 -> 0.5, Eternal Victor 0.55 -> 0.47.
- Level scaling: Patron 0.015, Emperor 0.01 (already long fights).
- Values in the assets and in `BossSetup.cs`.

**After (the same bot and seed, 2026-10-24; the bot stops at 640 s, so a fight still going is cut there):**

| Boss | HP at the bot's level | Fight | Bot DPS | Before |
|---|---|---|---|---|
| Gorm | 11 250 (L30) | 77 s, down | 154/s (a stronger build than before) | 95 s at 106/s |
| Provincial Champion | 17 600 (L27) | 30 % left after 108 s (≈ 155 s) | ≈ 114/s | 89 s |
| Beast Lord | 31 800 (L33) | 42 % left after 92 s (≈ 160 s) | ≈ 200/s | 52 s |
| Eternal Victor | 47 600 (L36) | 54 % left after 86 s (≈ 185 s + the remains) | ≈ 255/s | 68 s |

The bot's random cards swing its damage 2-4x between runs, so one run per boss is a rough measure. At a human's
2-3x the bot's damage, the fights land around 50-90 s, the final boss longest. Logs: `~/.local/share/gladiator-roguelike/scratch-2026-10-24/*2.log`.
The producer's playtest decides from here: every number is in the `BD_*` assets (`hpPerLevel`, `damageMul`, `recover`) and the `EN_*` bodies (`maxHp`).
