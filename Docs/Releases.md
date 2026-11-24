# Releases

Unzip anywhere and run `OpenFF3 Enhanced.exe` to play. The Crystal editor is a separate download (the "Crystal editing tools" release), only for those who wish to edit the game or make mods. It needs the Steam or GOG copies of the games on the machine (Final Fantasy III the 3D remake); none of this data is in the zip or in this
repository. Windows 10/11, x64; the .NET runtime is inside, nothing to install. The game keeps its saves and settings in a `userdata` folder beside `OpenFF3 Enhanced.exe`.

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
- Quality of life: **Critical hit flash**. Switch it off to stop the white screen flash on critical hits (on by default, as the game plays).
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
- Customisable Dungeon Minimap (Quality of life > Minimap): a toggleable minimap in the top right corner of the screen. Can be turned on or off. Can alter the size and zoom. Can set whether to display unopened chests. Can be set to an FF4 3D style "Fog of War" map that you need to uncover as you walk, or a pre-completed map. You can also set how much of the map your character will uncover as they walk if you are finding it too narrow. Be wary though that higher settings (and minimap use in general) may spoil the location of hidden passages.
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
- "Report a bug / Request a feature" in the OpenFF3 Enhanced menu: opens the project's GitHub page, where bug reports and feature requests have their own forms.
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