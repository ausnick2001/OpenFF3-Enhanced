# Releases

Unzip anywhere and run `OpenFF3 Enhanced.exe` to play. The Crystal editor has been separated from the main game and sits in its own `Crystal Modding Tools` folder, for those who wish to edit the game. It needs the Steam or GOG copies of the games on the machine (Final Fantasy III the 3D remake); none of this data is in the zip or in this
repository. Windows 10/11, x64; the .NET runtime is inside, nothing to install. The game keeps its saves and settings in a `userdata` folder beside `OpenFF3 Enhanced.exe`.

## 0.9.0 - OpenFF3 Enhanced (2026-11-22)

Builds on the excellent work from zerratar on OpenFF to make a further enhanced version of Final Fantasy III 3D on PC. New features listed below. This release is for FF3 only; it starts Final Fantasy III from the Steam or GOG install. Everything the game writes - saves, settings, achievements - now lives in a `userdata` folder beside `OpenFF3 Enhanced.exe`: delete the game's folder and it is all gone, or use `--data-dir=<folder>` to put it elsewhere. If you have pre-existing savedata from your AppData folder, a first run copies it (`%APPDATA%\FF3`, `%LOCALAPPDATA%\OpenFF`) into the game directory, and leaves those original copies where they were.

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