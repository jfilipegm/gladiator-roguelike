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
Measured with bot runs to each boss (dev build, `-bot -timed`, the Nobody, seed 0C0FFEE5; the bot picks random
cards, so a human's build is stronger): see the table below once filled.
