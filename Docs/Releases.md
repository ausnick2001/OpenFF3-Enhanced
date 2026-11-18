# Releases

What each release carries, and how one is made. The download is one zip,
`OpenFF-<version>-win-x64.zip`: unzip anywhere, run `OpenFF.exe` to play, `crystal.exe` to
edit and make mods. It needs the Steam copies of the games on the machine (Final Fantasy III
and/or Final Fantasy IV, the 3D remakes); none of their data is in the zip or in this
repository. Windows 10/11, x64; the .NET runtime is inside, nothing to install. The game keeps its saves and settings in a `userdata` folder beside `OpenFF.exe`. How a release
is made is in `Docs/Releasing.md`; each version's section below is its release's description.

## 0.9.0 - OpenFF3 Enhanced (2026-11-18)

Builds on the excellent work from zerratar on OpenFF to make a further enhanced version of Final Fantasy III 3D on PC. New features listed below include; FFX style turn based battles, a menu in the look of the game's own party menu with the quality-of-life boosters the Pixel Remaster has, a dungeon minimap, higher-quality textures and pictures, and a smoother game with VSync on. This release is for FF3; it starts Final Fantasy III from the Steam or GOG install. Everything the game writes - saves, settings, achievements - now lives in a `userdata` folder beside `OpenFF.exe`: delete the game's folder and it is all gone, or use `--data-dir=<folder>` to put it elsewhere. If you have pre-existing savedata from your AppData folder, a first run copies it (`%APPDATA%\FF3`, `%LOCALAPPDATA%\OpenFF`) into the game directory, and leaves those original copies where they were.

**Added**
- Turn-based battles: An FFX style battle system where each combatant acts one by one at their turn in speed order. The camera stays on the command view. No more having to put the entire party's commands in at once to act. Enabled through the quality of life options menu. Can be enabled and disabled at any time.
- Faster battles: the game is faster to display spoils at the end of battle. End of battle notifications can be skipped through more quickly by pressing cross/B/A.
- Battle windows and OpenFF start menu redesigned to be more aesthetically pleasing and coherent with the in-game menu.
- The OpenFF3 Enhanced menu - Brought up by pressing esc on keyboard or start on the controller. Pauses the game in the background while open.
- Encounter rate modifier: Change Encounter rate (0.5x, 1x, 2x, 4x). Too many random battles for your liking? Reduce the encounter rate to 0.5x and double the EXP and job points! In addition to the existing OpenFF features (Game speed modifier, turning off random encounters, battle EXP/Job EXP multipliers, Save anywhere, removing Job change adjustment penalty)
- Indicator which displays what cheats/modifiers you are currently using in the top left corner now has an additional option to just display the game speed and encounter rate (most frequent modifier players are likely to want to see constantly), which displays nothing if both are set to default 1x. Options are now: Off, Speed and encounters, All.
- Dungeon minimap (Quality of life > Minimap): Fog of war or Complete; Size; Zoom 50-200%; Area uncovered 100-200% (how much of the map the party uncovers, fog of war only); a Transparent or solid style; unopened chests; hidden passages; every exit drawn as a door from the game's own door texture; a smooth outline along the floor's edge and the walls; an arrow for the hero. Explored floors are kept in the save, and Forget explored maps (asks to confirm) clears them.
- Settings in the menu: Window (windowed, borderless, full screen), Window size, Anti-aliasing, VSync, Frame rate, Run, Updates, Check for updates, Check game files, Button icons, Pad buttons.
- Button icons (Auto, PlayStation, Xbox, Switch, Keyboard), from the `pad-icons` folder beside the game; Steam's `%key_assign N%` button names in the dialogue show the player's own button.
- Fonts: a `fonts` folder beside the game with `menus`, `dialogue`, `names` and `title` folders - drop a font in and it is used. The speaker's name has its own face; the story text (prologue, cutscenes) has a pale glow behind it so it reads over the picture (`storyGlow` 0-3 in settings.json); MS UI Gothic (.ttc) faces work. Text speed Fast shows a page at once.
- Higher-quality textures: a mod's PNG replaces a texture at any size (named as the texture, or with its package: `j104.eye.png`, `files_j104.ntxp.eye.png`), drawn with linear filtering. Larger replacement pictures are fitted to the party faces, the title sheet, the name screen's pictures and the pointing hand (`icon_yubi.NCBR` and `.NCGR` in a mod's `files` folder, at any upscale factor).
- The party screen shows each hero's job level after the job name; Status shows the job and its level on the name row and the job points still needed for the next job level.
- Mastery sample: FF3's own 23 jobs only; Freelancer and Onion Knight keep the game's Magic command; a job change or a change of set abilities returns gear the hero may no longer wear to the bag.
- Command line: `--data-dir`, `--no-gpu-drain`, `--log-stalls` (every frame over 50 ms, with where the time went: game step, engine, drawing, garbage collector, textures); `--help` lists every option.

**Changed**
- Starts Final Fantasy III from the Steam install; the release's `mods` folder starts empty.
- On the world map a full push of the stick walks; it no longer runs (Settings > Run chooses).
- The field's on-screen Map and Menu (touch) buttons are hidden by default (`--show-field-buttons`); the company logos are skipped (`--show-logos`).
- Saves, settings, achievements and the game-files check's cache moved into `userdata` beside the game (see above); the updater leaves that folder, `mods` and `logs` alone.

**Fixes**
- Stutter with VSync on: the first time a model, text or picture was shown, the driver held about three frames queued and the texture upload waited for all of them - about 50 ms each time. The queue is now emptied right after each frame is shown, so the upload returns at once, and replaced textures are released a frame later.
- Shops: L / R (or left / right) on a pad switch between Buy and Sell; the tab had worked by touch only.
- The Equipment menu on a pad: Circle from the slots steps up to the command row (Equip, Remove, Remove all) and a chosen command now runs; after a job change the hero is already chosen, so the character pick is skipped once.
- Unarmed heroes strike as in the original FF3.
- Messages: a yellow indented line keeps the body font; Fast waits for the window to open before showing a page whole.
- Progression: previewing a save slot no longer overwrites a hero's live ABP.
- The minimap: floors of the slot or autosave that is loaded are the ones applied; cliff tops, floor pockets too narrow to stand in and gaps behind walls are no longer filled in (Complete mode can still fill in areas behind walls - its note says so).
