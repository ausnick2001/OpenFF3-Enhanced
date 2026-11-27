# Releases

Unzip anywhere and run `OpenFF3 Enhanced.exe` to play. The Crystal editor is a separate download (the "Crystal editing tools" release), only for those who wish to edit the game or make mods. It needs the Steam or GOG copies of the games on the machine (Final Fantasy III the 3D remake); none of this data is in the zip or in this
repository. Windows 10/11, x64; the .NET runtime is inside, nothing to install. The game keeps its saves and settings in a `userdata` folder beside `OpenFF3 Enhanced.exe`.

## 1.0.0 - OpenFF3 Enhanced (2026-11-30)

**Added**
- **Health bars** (Text and Menus > Battle menus > Health bars): a bar for each hero's health on the battle party panel, beside the numbers. Off, Short bar (under the numbers), Full row (the whole row fills from the left as health drops) or Long bar (a thin line along the bottom of the row). The bar is green while the hero is well, amber at half or less, red at a quarter or less. Short bar by default. A Colour scheme row under it (greyed out while the bars are off) picks the greens: Forest (the default), Emerald, Mint, Lime, Teal or Jade, each with a deeper shade at full health. Numbers take the bar's colours. The HP numbers on the party panel are a little smaller than the game's in every setting, including with health bars off.

**Changed**
- The battle magic and item lists: the top row's text and icons sat a little high in its box and the bottom row's a little low (the panel's border covers the edges of those rows); they now sit in the middle of the visible row. The spell level number also sits closer to its "LV." lettering.

**Fixed**
- The turn order bar numbered the monsters by their place in the monster party, so E1 was often not the one at the top. They are now numbered from the top of the screen down: E1 is the highest, then E2, and so on.
- The border of the help bar at the top of the battle screen (and the foe name box) broke in a spot at the smaller battle menu sizes (85% and below). Its frame pieces overlapped more than they do at full size; they now overlap as they do at 100%.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7 or later, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.15 - OpenFF3 Enhanced (2026-11-27)

**Fixed**
- A picture flashing up for a frame or two every few seconds at 60 fps while walking around Ur's forest and lake (the map seen from ground level under a blue sky, or a close-up of wood): the smoothing blended a big piece of the ground with another piece. Found by [3122380113Linh Vu Nhat](https://github.com/3122380113).
- After a game over, pressing A on the blank Continue row at the title screen locked the menu up (only restarting the game got out). The pad now ignores it, as touch already did. Found and fixed by [3122380113Linh Vu Nhat](https://github.com/3122380113).

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7 or later, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.14 - OpenFF3 Enhanced (2026-11-26)

**Added**
- **Turn order bar**: a column down the right edge of the battle screen (in turn-by-turn battles) showing who acts next this round, as in Final Fantasy X. Heroes show their party-menu face in their current job, monsters are violet squares labelled E1, E2 and so on by their place in the fight, and a boss is a dark square with a pulsing ember border marked BOSS. The one acting now is highlighted and the bar empties as turns are taken, then fills again when the next round's order is rolled. When you choose a target, a small hand on the bar points at who the battle's hand is on (all of them, for an all-target spell). Turn it off under Text and Menus > Battle menus > Turn order bar.
- **FAQ** (`Docs/FAQ.md`), linked from the bug report and feature request forms, answering the questions that come up most, with a reminder to attach the log to a bug report.

**Fixed**
- The "4 HITS" label now sits under the damage number of the monster you hit, not over the attacking hero, so it no longer lands on your own character from our camera angle. It also appears at once and stays still, as in the game, instead of lifting like the damage number (Original (upscaled) style).

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7 or later, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.13 - OpenFF3 Enhanced (2026-11-26)

**Added**
- **Battle Menu: Clear / Opaque** now works (Esc > Config, the row that was greyed out). Clear makes the battle windows see-through, with the list lines faded to a soft grey to match the window's outline.
- **Compact mode** (Text and Menus > Battle menus): condenses the battle menus by reducing empty space either side of the spell, item and equipment lists. Off by default.
- **Custom music**: a `custom music` folder beside the game (made the first time a tune plays) with a folder for each of the game's 59 tunes. Drop one `.ogg` or `.wav` in a tune's folder and it plays instead of the game's, looped for the tunes the game loops. Some folders are named for where the tune plays (Battle, Boss battle, Final boss battle, Victory fanfare and a few towns and dungeons); the rest are just `BGM01`, `BGM02`, and so on, for you to work out by ear. A `README.txt` in the folder explains it.

**Fixed**
- Minimaps now save: each dungeon's map is kept in every save slot and in the autosave, and survives quitting and loading. Before, opening the save menu in a dungeon could wipe the map you had filled in.
- Choppy motion at 60 fps in caves (Altar Cave at two spots on the way round): the frame smoothing mistook the big cave floor pieces for a scene cut as the camera followed the hero, and drew those frames unblended. It now ignores large pieces whose middle is behind the camera or far off the screen. 30 fps was never affected.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7 or later, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.12 - OpenFF3 Enhanced (2026-11-26)

**Added**
- **Touch buttons** switch (Esc > Text and Menus): takes down the touch screen's on-screen buttons - the menus' Back and player switch, and the battle's Back, Player / Enemy, All and Group - for those who play with a pad. On by default; the pad and keys do everything either way.
- **Transition sounds** switch (Esc > Quality of Life): turn off the swoosh the game plays as an area loads and as the menu opens and closes, for those it grates on. On by default.
- Steam saves now come over. Copy the Steam game's own `SAVE.BIN` into `userdata\saves` (as `save.bin`) and the game converts it to its own format the first time it starts, party, items, gil, job levels, position and story progress included. The Steam file is kept beside it as `save.steam-backup.bin`. If you already have a `save.bin` of your own there, it is left alone and the conversion is written as `save.from-steam.bin`.

**Changed**
- Gambits may now be set across sides: Attack on an ally (to cure a confused hero, say), and spells and items at the other side where the game lets them be aimed (Cure at the undead, a Potion at a foe). Those are listed after the ones that suit the condition's side.
- A save the game cannot read now shows as a broken slot in the load list instead of crashing it.

**Fixed**
- Auto-battle rules (gambits) no longer fall back to "Foe (any) > Attack" once you are playing: the game's reads of the save file (options, slot headers, the save menu) used to wipe them, most of all before a first save was made.
- On the Gambits screen, coming back from choosing a target or action returns to the hero you switched to with L / R, not the one you first opened the screen with.
- Opening and closing the list screens (the Gambits screen's scroll arrows) no longer uses up the game's image slots, which crashed the world map after about two dozen openings.
- Running from a fight: the hero whose turn it was no longer pops back onto the empty field for a moment as the screen fades out.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7 or later, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.11 - OpenFF3 Enhanced (2026-11-25)

**Added**
- A "Play opening FMV" entry on the title screen, above Mods, plays the game's opening movie from your install, and New Game now asks whether you would like to watch it first. Press any key or button to skip the movie. Both only appear if the movie file is found in your game folder.
- Battle menu size (Text and Menus > Battle menus): shrink the command list and help bar in battle down to 75% in 5% steps, resize the party's names and health on their own, and set the text size (also down to 75%) on top. The text follows the size of the menus. The target boxes (foe names, Back, Player/Enemy, All, Group) follow the size too, and so do the spell list, the item list and the equipment screens (the hands and weapons, and the weapons-in-hand screen): their windows shrink toward the middle of the screen with the text, icons, numbers, scroll arrows and hand. "Reduce clutter in battle" (same page) takes away the target name lists: the hand stands on the foe, with its name in the help bar at the top, or on the hero's line of the party menu. On by default; switch it off to get the name lists back.

**Changed**
- The battle item list now has four rows in the room of its three, like the magic list, so more items show at once. Its scroll arrows are smaller and sit inside the box (the top one used to float above it), and the hand stands on the item's icon, not over its name.
- The Back button in battle is a fifth smaller.
- The battle equipment screen and the weapons-in-hand screen (R. Hand / L. Hand) are tidied: everything is moved right so the hand no longer covers the names, and the weapon list's lines line up with its rows, with the centre line running the full height.

**Fixed**
- Fixed the cursor in the battle magic list: scrolling up from the top wrapped to the last page but left the hand on that page's first row instead of its last. The list now has four rows in the same space as its three, so the last page ends on level 8 (it stopped at level 7 before, with level 8 never on show).
- Fixed scrolling up in the battle item list opening an empty equipment screen when the hero has nothing in hand.
- Fixed the game opening behind Steam Big Picture mode when it is added to Steam as a non-Steam game, so you had to Alt+Tab to it. The game now comes to the front when it starts, and a black console window no longer opens before it.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7 or later, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.10 - OpenFF3 Enhanced (2026-11-25)

**Added**
- Damage popups (Text and Menus > Damage popups), contributed by zerafidev. Battle damage, healing, hit counts, critical hits and misses are drawn as outlined text in the style of the NES and FF6 games, each with its own animation. Style has three choices: Original (upscaled), the default, which moves the same way as the game's own numbers but in our sharper font; Bouncy, with each digit hopping in turn; and Original, the game's own numbers. Except for Original, you can choose the number colour (red, white or gold), the size (small, medium or large) and the shadow (off, light, medium or heavy).
- Battle descriptions (Text and Menus > Battle descriptions): choose Off, Minimal or On for the descriptions shown at the top of the battle screen for commands, spells, songs and items. Minimal skips the obvious commands (Attack, Guard, Magic, Steal, Summon and so on) and keeps the rest. On by default.

**Fixed**
- Fixed multi-language support: The game was supposed to match the language of your vanilla FF3 3D install but it wasn't. To change the language, right click on FF3 3D in Steam and then click properties and select a language. Steam will then download the language files. OpenFF3 can then use them to display in that language. Unfortunately this only applies to in-game menus and dialogue. The OpenFF3 menus will still be in English. Players wishing to play in Japanese, Chinese or Korean may need to provide their own fonts to display correctly
- Fixed Sort in the Item menu and the Magic menu tabs not being reachable with a gamepad.
- Fixed the hand pointer in battle sitting slightly too far to the right of party members. It now points at the character.
- Fixed backing out of choosing a target for a spell, song or item taking you back to the main battle menu. It now takes you back to the list you chose from, with the cursor on the spell, song or item you picked.

**Changed**
- In the pause menu, the left or right arrow of a setting is greyed out when there is no further value that way (for example the shadow at Heavy, or the dialogue box size at its limit).
- The game no longer checks its files against the release when it starts (it warned about a language installed through Steam, and about harmless changes). Settings > Check game files still does the check when you ask for it.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.9 - OpenFF3 Enhanced (2026-11-24)

**Added**
- Dialogue shadow and Menu text shadow (Esc or Start, then Font / Dialogue) now have four levels each: Off, Light, Medium and Heavy. Each is set on its own, and both start on Medium. Battle text and the story scenes (the prologue, the crystal's words) keep their own look whatever you choose.

**Changed**
- The spell, item and song lists in battle open at once. The magic list used to grow open slowly, like a dialogue box, and the commands and party bars blinked off for a moment before any of the lists came in.
- The Font / Dialogue page no longer has its description lines, which ran off the page.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.8 - OpenFF3 Enhanced (2026-11-24)

**Fixed**
- Settings > Check for updates, in a game started from a launcher such as RecompLauncher, now says that the launcher keeps the game up to date. It used to say "only an installed release updates itself, not a source build", which read like a fault.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.7, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.7 - OpenFF3 Enhanced (2026-11-24)

**Added**
- Font / Dialogue options (Esc or Start, then Font / Dialogue): the size of the dialogue box (60% to 100%) with its text and the speaker's name together, and the dialogue text and the speaker's name larger or smaller on their own.  Two shadow switches, Dialogue shadow and Menu text shadow, take the dark drop shadow off the dialogue and off the menus' text. The prologue's and cutscenes' centred text is not changed. Final Fantasy III only.

**Fixed**
- The game's own tips now name the right button for auto battle. They used to say "Square" (the NPC who teaches it, and the battle help page); they now show the button you bound on the Controller configuration page (R3 by default, F on a keyboard).
- The shadow behind dialogue and menu text is now the same on every line. It used to be heavier on some lines of a box than on others (with three lines of text, the first and third looked raised and the second flat), because the shadow's distance was rounded differently depending on which row the line sat on.
- The help page's escape tip said to hold one shoulder button; escaping takes both shoulders together, and the tip now says so with your own buttons.

**Changed**
- The text over black screens and cutscenes (the crystal's words, for one) is crisper: pale lettering now has a thin dark outline with a soft glow further out, in place of a glow that blurred its edges.

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.6, the game offers this update itself; your saves and settings in `userdata` are kept.

## 0.9.6 - OpenFF3 Enhanced (2026-11-24)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.5, the game offers this update itself; your saves and settings in `userdata` are kept.

**Fixed**
- The Job menu now scrolls. With more jobs open than the list has rows (the list shows 16), pressing Down on the bottom row or Up on the top row moves the list, so every job after Black Belt can be chosen.
- Loading a save with a mod's data (a mod's own chests, a hero's job progress) now brings back the data of the slot you chose. The game reads every slot at the title screen, and a mod could be left holding the last slot read rather than the one picked.

## 0.9.5 - OpenFF3 Enhanced (2026-11-23)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.4, the game offers this update itself; your saves and settings in `userdata` are kept.

**Changed**
- The release is lighter: the Crystal editor is no longer in the game's zip. It is its own download, the "Crystal editing tools" release, only for those who want to edit the game or make mods. If you already have a `Crystal Modding Tools` folder, it stays as it is.
- The sample mods are no longer shipped (they stay in the source repository). They were experiments to show what the modding tools can do and are not supported.
- Updating: the game now checks that Crystal is closed before it downloads an update, and says so if it is open. The updater itself is simpler.

## 0.9.4 - OpenFF3 Enhanced (2026-11-23)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.3, the game offers this update itself; your saves and settings in `userdata` are kept.

**Fixed**
- The "missing files" warning no longer appears for the font files some copies of the game lack (arial.ttf, mona.ttf and similar). The game does not need them.
- The Speed setting no longer speeds up the title menu. It now applies only once you start a new game or load a save.

**Added**
- Quality of Life: **Critical hit flash**. Switch it off to stop the white screen flash on critical hits (on by default, as the game plays).
- Debug overlay (F1) in an FF3 battle: the status test keys (Insert chooses a status, Home a party member, End inflicts it, Delete clears it) for seeing how each status looks, and a new PageUp that makes the party's hits always critical. They only work while the overlay is up and never run in a normal game.

## 0.9.3 - OpenFF3 Enhanced (2026-11-23)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.2, the game offers this update itself; your saves and settings in `userdata` are kept.

**Fixed**
- Character dialogue showed as garbled text on copies of the game installed in Japanese (treasure chest messages were fine). The game now recognises Japanese dialogue files and reads them correctly. English and other languages are unchanged. If you play in Japanese, please let us know whether it now reads correctly.

## 0.9.2 - OpenFF3 Enhanced (2026-11-23)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.1, the game offers this update itself; your saves and settings in `userdata` are kept.

**Fixed**
- If the game could not find your copy of Final Fantasy III on its own, it closed without saying anything. It now tells you and lets you choose the game's folder (the one holding `FF3_Win32.exe`). Your choice is remembered, so you are only asked once.

**Added**
- An experimental Linux build (`linux-x64` tar.gz, for desktops and the Steam Deck's desktop mode) is attached to the release. It looks for Steam's library folders on Linux and reads the Windows files of the game you own. It has not been tested on a real machine yet, so bug reports are welcome. Windows players are not affected.

## 0.9.1 - OpenFF3 Enhanced (2026-11-22)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. If you already have 0.9.0, the game offers this update itself; your saves and settings in `userdata` are kept.

**Changed**
- The quality-of-life indicator (top left) now only shows once you are in the game, not on the title screen or while loading.
- When you load a save or start a new game, the indicator appears for the first few seconds to remind you what is switched on (including the minimap), even if it is normally turned off.
- The OpenFF3 Enhanced menu's rows sit a little higher, so the line under Exit Game no longer touches the button icons.
- Window size now offers 3840 x 2160 (4K), and only lists sizes your screen supports: in a window the sizes that fit your desktop, in full screen the sizes your display reports.
- Window size is greyed out when selecting borderless mode, as this setting always draws at your desktop's resolution.

**Fixed**
- The game now overrides Windows' high DPI scaling by default, so it draws at your screen's real resolution instead of being stretched. Useful if you play on a TV or 4K screen with Windows scaling set up.

**Added**
- RecompLauncher support: the release now includes a `recomplauncher.json`, so players who use [RecompLauncher](https://recomplauncher.com) get one-click setup and settings. Nothing changes for anyone else.

## 0.9.0 - OpenFF3 Enhanced (2026-11-22)

**Instructions:** make sure you have Final Fantasy III 3D installed from either Steam or GOG (GOG is untested but should work). Download the zip below, unzip it anywhere, run `OpenFF3 Enhanced.exe` and play. On first start it offers a one-off download of the HD textures (about 1.5 GB).

Builds on zerratar's excellent OpenFF to make a further enhanced version of Final Fantasy III 3D on PC. New features are listed below. This release is for FF3 only.

Saves, settings and achievements live in a `userdata` folder beside the exe, so deleting the game's folder removes everything. Use `--data-dir=<folder>` to put it elsewhere. On first run it also copies any existing save data from `%APPDATA%\FF3` and `%LOCALAPPDATA%\OpenFF`, and leaves the originals where they were.

**Added**
- Turn-based battles: An FFX style battle system where each combatant acts one by one at their turn in speed order. The camera stays on the command view. Enabled through the quality of life options menu. Can be enabled and disabled at any time.
- Customisable Dungeon Minimap (Quality of Life > Minimap): a toggleable minimap in the top right corner of the screen. Can be turned on or off. Can alter the size and zoom. Can set whether to display unopened chests. Can be set to an FF4 3D style "Fog of War" map that you need to uncover as you walk, or a pre-completed map. You can also set how much of the map your character will uncover as they walk if you are finding it too narrow. Be wary though that higher settings (and minimap use in general) may spoil the location of hidden passages.
- Faster battles: the game is faster to display spoils at the end of battle. End of battle notifications can be skipped through more quickly by pressing cross/B/A.
- Battle windows and OpenFF start menu redesigned to be more aesthetically pleasing and coherent with the in-game menu.
- The OpenFF3 Enhanced menu - brought up by pressing esc on keyboard or start on the controller. Pauses the game in the background while open.
- Status menu for each character now shows the number of Job Points needed for the next Level.
- The party menu now shows what job level each character is.
- Encounter rate modifier: Change Encounter rate (0x, 0.5x, 1x, 2x, 4x; 0x turns random encounters off, replacing the separate on/off switch). Too many random battles for your liking? Reduce the encounter rate to 0.5x and double the EXP and job points! In addition to the existing OpenFF features (Game speed modifier, turning off random encounters, battle EXP/Job EXP multipliers, Save anywhere, removing Job change adjustment penalty)
- The field's on-screen Map and Menu buttons are now hidden by default to reduce clutter as they serve no purpose on a non-touchscreen device. The `--show-field-buttons` launch option restores them
- the company logos are skipped. The launch argument `--show-logos` restores them.
- Indicator that displays what cheats/modifiers you are currently using in the top left corner now has an additional option to just display the game speed and encounter rate when they are above or below the default setting (most frequent modifier players are likely to want to see constantly). Options are now: Off, Speed and encounters, All.
- Option to change controller icons in the OpenFF3 Enhanced menu (Auto, PS, Xbox, Switch, Keyboard).
- "Report a Bug / Request a Feature" in the OpenFF3 Enhanced menu: opens the project's GitHub page, where bug reports and feature requests have their own forms.
- Option to reset the game back to the opening menu in the Open FF3 Enhanced menu. Useful for if you want to load a save (attempts to create a "load save" option from the Open FF3 Enhanced menu have led to lots of bugs).
- Better PlayStation icons
- Fonts: fonts have been divided into 4 categories  `menus`, `dialogue`, `names` and `title`. A `fonts` folder now appears in the games main directory beside the .exe with these 4 folders - drop a font in and it is used. If dialogue is too big or too small, put a text file called scale.txt in the dialogue folder with a number from 0.5 to 1.5: 0.88 is the default.
- The story text (prologue, cutscenes) has been changed to be more readable; a pale glow now illuminates it so it reads over the prologue pictures (`storyGlow` 0-3 in settings.json).
- Setting text speed to "fast" in game now makes text appear immediately
- Compatibility with my FF3 HD Textures mod and Retextured Portraits mod. In order for these textures to all work, some changes to the code needed to be made to prevent errors (e.g. only the top corner of an upscaled texture displaying).
- The party screen shows each hero's job level after the job name; Status shows the job and its level on the name row and the job points still needed for the next job level.
- Added command line options: `--data-dir`, `--no-gpu-drain`, `--log-stalls` (every frame over 50 ms, with where the time went: game step, engine, drawing, garbage collector, textures); `--help` lists every option.

**Changed**
- Saves, settings, achievements and the game-files check's cache moved into `userdata` beside the game (see above); the updater leaves that folder, `mods` and `logs` alone. Everything is now contained in one folder which is the preference of many recomp launcher devs.

**Fixes**
- Petrified characters now turn grey with the HD Textures pack installed. The game petrifies a character by swapping in a grey palette, which a full-colour HD picture ignored, so the hero froze but stayed coloured; the picture is now drawn grey while petrified and its colour returns when cured.
- On the world map a full push of the left stick no longer runs. Running on the world map moved the character faster than intended, looked strange and led to collision issues (running at a wall for example would eventually let you clip through it, causing sequence breaks).
- Fixed stutter with VSync on: with v-sync on the game would stutter frequently. The first time a model, text or picture was shown, the driver held about three frames queued and the texture upload waited for all of them - about 50 ms each time. The queue is now emptied right after each frame is shown, so the upload returns at once, and replaced textures are released a frame later.
- Shops: L / R (or d-pad left / right) on the gamepad switch between Buy and Sell; the tab had previously worked by touch only.
- Controller configuration: rebinding a button no longer double-presses. The press that picked the row (Cross) was also taken as the new button, so whatever you chose ended up bound to Cross. The note under the list is now sized to its text and no longer sits over the button hints.
- A map button: there was no way to open the world map with a gamepad or keyboard (the game only offered an on-screen touch button, now hidden by default). Press Y on a controller or M on the keyboard on the overworld.
- Select on the gamepad now changes the party leader on the field, as on the DS. The game's code for it was never connected.
- Controller configuration (was "Pad buttons", Settings menu): lists every function with its button (Start, Select, Y for the map, auto battle, speed up...) so you can see what is assigned and rebind it. Auto battle (R3) was missing from it.
- The Equipment menu on a gamepad: Previously it was impossible to select "unequip" from the equipment menu on a controller as pressing circle would back you out into the main menu. Pressing circle now backs up to the command menu above where "Remove" and "Remove all" can now be selected.
- HD textures: a thin pale outline around trees and other cut-out sprites (most visible on the big pines in the goblin battle) is gone. The game tests see-through textures at a very low cut-off, so the almost invisible fringe round a sharp edge still blocked what was drawn behind it and showed the background colour as a line. Large textures are now cut off at the usual halfway point. If you ever need the old behaviour, launch with `--alpharef=game`.