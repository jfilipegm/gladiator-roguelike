# Credits and third-party licences

Everything in the game is made for it (code, models, pixel art, frames, placeholder sounds) except the fonts below.
Each one allows commercial use. Its licence file ships with every build in
`<build>_Data/StreamingAssets/Licenses/Fonts/` (copied there by `Gladiator/Setup/UI/All`) and sits beside the TTF in
`Assets/Art/Fonts/`.

## Fonts

| Font | Designer | Licence | Used for | Source | Licence file in the build |
|---|---|---|---|---|---|
| **Luckiest Guy** | Astigmatic (Brian J. Bonislawsky) | Apache License 2.0 | Numbers: damage numbers, the HUD's numbers, the timer, prices (role `Number`) | github.com/google/fonts `apache/luckiestguy` | `LuckiestGuy-LICENSE.txt` |
| **Caesar Dressing** | Open Window | SIL Open Font License 1.1 | The announcer: banners, callouts, LEVEL UP!, VICTORY! (role `Shout`) | github.com/google/fonts `ofl/caesardressing` | `CaesarDressing-OFL.txt` |
| **Marcellus SC** | Astigmatic (Brian J. Bonislawsky) | SIL Open Font License 1.1 | Names, headers, buttons, tabs (role `Title`) | github.com/google/fonts `ofl/marcellussc` | `MarcellusSC-OFL.txt` |
| **Marcellus** | Astigmatic (Brian J. Bonislawsky) | SIL Open Font License 1.1 | Long text: card and Codex descriptions, hints (role `Body`) | github.com/google/fonts `ofl/marcellus` | `Marcellus-OFL.txt` |
| **Liberation Sans** | Red Hat, Inc. (digitized data © Google) | SIL Open Font License 1.1 | The symbols fallback (`FN_Symbols SDF`: arrows and maths signs the four fonts lack) and the debug line | ships with TextMesh Pro (`Assets/TextMesh Pro/Fonts/`) | `LiberationSans-OFL.txt` |

The OFL lets the fonts be bundled, embedded and sold with the game. The fonts themselves must not be sold on their own,
and a modified font must not use the Reserved Font Name. We only convert them to TextMesh Pro SDF atlases
(`Assets/Art/Fonts/TMP/`), which the OFL allows. Apache 2.0 needs its licence (and any NOTICE file; Luckiest Guy has none)
to ship with the font.

## Sounds

Today every sound is a placeholder synthesised in an Editor script. Any third-party sound added later (CC0 or bought)
gets a row here with its author, source and licence before it ships.
