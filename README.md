# OpenFF3 Enhanced

An enhanced version of **Final Fantasy III** (3D remake) on PC in OpenFF . It runs from your own Steam or GOG copy as a native desktop game, and adds the things I always wanted while playing it: FFX style turn-based battles, a dungeon minimap, encounter rate modifiers, a more aesthetically pleasing battle menu, easier to read fonts, and sharper textures.

This is built on [zerratar's OpenFF](https://github.com/zerratar/openff), which did the hard work of getting FF3 3Ds code decompiled. If you like what's here, go and give that project some love too.

I have always disliked JRPGs that have the combat system where your entire party puts in their commands and then you watch the round play out. It has always felt slow and clunky to me. And as a result I have never been able to stomach FF3. This all started as a very simple project to see if turn based battles were even feasible. As it turns out, implementing them as a mod was relatively simple, and in my handful of hours of playing it does not seem to hurt the balance of the game too much. One thing led to another and before you know it I had suddenly implemented multiple optimisations, bug fixes, additional quality of life options, a minimap, and even AI upscaled textures. Despite never having coded or released anything before - I decided to release this publicly. When I started this was only for personal use but I figured other people may enjoy and want to build off what I have done here.

I wanted to keep the changes as modular and adjustable as possible with the emphasis on being able to enjoy the game the way you want to.

Use turn based battles or don't.
Use the minimap or don't.
Make the minimap massive, or tiny. Make it opaque, make it transparent. Make it display unopened treasure chests, and secret passages, or neither. Make it already complete or make it so you can uncover it FF4 3D style.
Use encounter rate modifiers, or don't.
Use enhanced textures, or don't.
Use the included sharper fonts, or don't. If you don't like the new fonts, that is fair! Download your own and put them in the fonts folder.

No game files are included. You need your own copy of Final Fantasy III from Steam or GOG.

<!-- TODO: screenshots. Suggested: the OpenFF3 Enhanced menu, a turn-based battle, the minimap, the HD textures. Put them in Docs/Images and link them here. -->

## What's new

**Battles**
- **Turn-based battles.** An FFX-style system: each character acts one at a time, in speed order, with the camera staying on the command view. No more having to put the entire party's commands in at once to act. Enabled through the quality of life options menu. Can be enabled and disabled at any time.. Switch it on or off at any time in OpenFF3s Quality of life menu (start on the controller or esc on keyboard).
- **Faster battles.** The spoils and exp messages appear sooner, and can be skipped quickly with Cross/B/A.
- **Encounter rate modifier.** 0.5x, 1x, 2x or 4x. Too many random battles? Halve them and double the EXP and job points.

**Dungeons**
- **A customisable minimap** (Esc/Start > Minimap). Show or hide it, change its size and zoom, and choose whether chests and hidden walls are marked. You can choose between a completed map or an FF4-style "fog of war" map that fills in as you walk (no rewards for completing the map...sorry). You can even set how much of the map your character will uncover as they walk if you are finding it too narrow. Be wary though that higher settings (and minimap use in general) may spoil the location of hidden passages.

**The menus**
- **The OpenFF3 Enhanced menu.** Press **Esc** (or **Start** on a controller). It pauses the game, and has Settings, Quality of life, Minimap, Reset (back to the title screen) and Exit.
- **Aesthetic consistency** The menu and battle windows are restyled to match FF3's own party menu, hand cursor and all.
- **Quality of life.** Game speed, random encounters on or off, battle EXP and job EXP multipliers, save anywhere, no job change penalty, and an indicator in the top-left corner showing which of these are on.
- **Party and status screens** now show each hero's job level, and how many job points are left until the next level.
- **Controller icons** for PlayStation, Xbox, Switch or keyboard (Start/Esc > Settings > Button icons). Game ships with enhanced playstation icons.

**Looks and sound**
- **Sharper textures.** OpenFF3 Enhanced can use higher-quality textures and portraits. Some changes to the code were necessary for compatibility with my HD texture packs. See *Texture packs* below.
- **Fonts.** The menus, dialogue, names and title screen each have their own font, and you can swap in your own. See *Fonts* below.
- **Story text is easier to read.** A pale glow sits behind the prologue and cutscene text so it reads more easily over the pictures.
- **Smoother.** Frame pacing is smoothed, and there's a fix for the stutter that appeared with VSync on.

**Fixes**
- On the world map a full push of the stick no longer runs, which stopped the character moving faster than intended and clipping through walls.
- Shops: Originally it was impossible to move between Buy and Sell on the shop menu. This has been fixed. L / R switch between Buy and Sell on a controller.
- The Equipment menu: on a controller it was impossible to select "remove" and "remove all". These can both now be reached by pressing circle/B/A on your controller which takes you to the upper command menu where you can now select them.
- Company logos are skipped. 
- Everything the game writes lives in one folder, `userdata` which is in the same directory as the game. All contained in one folder.

The full list, with the details, is in [Releases](Docs/Releases.md).

## Install

1. Install **Final Fantasy III** from Steam or GOG.
2. Download `OpenFF-<version>-win-x64.zip` from [Releases](../../releases) and unzip it anywhere. Nothing needs installing.
3. Run `OpenFF3 Enhanced.exe`. It finds the game by itself.
4. (Optional but highly recommended) Download my HD texture packs from the links below. Copy the folders into the "mods" folder in the games root directory (where the game.exe is). If no mods folder exists, create it. Then in the main menu before starting the game, select the MODS option at the bottom, make sure both are enabled. Please let me know of any issues that you encounter with them;

- **FF3 HD Textures**: [Download here](link). Most of these textures were done by myself with the help of AI, however there were around 100 textures that I struggled to get right. Fortunately I was able to find [Panda Venom's amazing work on the textures for the PSP version](https://www.reddit.com/r/GameUpscale/comments/1cztzsz/psp_final_fantasy_iii_us_hd_textures/), and they very kindly let me use parts of their textures to fix mine. I don't really want or need donations for this project but if you did feel like giving anyone a donation, please consider giving a few $ to them. Their Ko-Fi page can be found here https://ko-fi.com/pandavenom  
- **Retextured Portraits**: [Download here](link)higher-quality character portraits, includes all character portraits for every different job.


You need Windows 10 or 11 (64-bit).

### Your saves and settings

Saves, settings and achievements are kept in a `userdata` folder next to `OpenFF3 Enhanced.exe`. Delete the game's folder and it all goes with it. The first time you start the game it copies any older saves and settings across from your AppData folder (`%APPDATA%\FF3` and `%LOCALAPPDATA%\OpenFF`) and leaves the originals where they were.

If you prefer to keep your saves/user data somewhere else, start the game with `--data-dir=<folder>`.

## Fonts

The `fonts` folder next to `OpenFF3 Enhanced.exe` has a folder for each kind of text: `menus`, `dialogue`, `names` (the speaker's name and the cutscene text) and `title`. To use a font of your own, put a `.ttf`, `.otf` or `.ttc` file in the right folder and restart the game. If a folder holds several fonts, the first one by file name is used. To make your own font come first, start its name with an `a`, or take ours out.

If the dialogue looks too big or too small, put a text file called `scale.txt` in `fonts\dialogue` containing a number from 0.5 to 1.5. Without one the size is 0.88; the font that comes with the game is set to 0.75.


## Controls

Arrow keys move, **Enter** confirms, **Backspace** cancels, **Esc** opens the OpenFF3 Enhanced menu. Holding **Tab** fast-forwards. The Quality of life menu also has a game speed option, and **F8** / **Shift+F8** change it while you play.
Gamepads work too, with rebindable buttons. L3 toggles the turbo, R3 toggles autobattle. Pressing start brings up the OpenFF3 Enhanced menu where you can rebind the controls to your liking. The full list is in [Controls](Docs/Controls.md).


## Command line

You don't need any of this to play. `"OpenFF3 Enhanced.exe" --help` lists every switch, and [Command line](Docs/Command-Line.md) explains them. A few worth knowing:

- `--data-dir=<folder>`: where saves and settings are kept.
- `--show-logos`: show the company logos at the start.
- `--show-field-buttons`: show the on-screen Map and Menu buttons, which are hidden by default.
- `--no-achievements`: don't send Steam achievements.
- `--log-stalls`: write a note to the logs whenever a frame takes over 50 ms, for tracking down stutter.

## Making mods

OpenFF3 Enhanced keeps everything zerratar's OpenFF can do: mods can change models, textures, text, sounds, maps, menus and even run C# code. **Crystal** (`crystal.exe`, in the same folder) is the editor for making them.

- [The modding guide](https://zerratar.github.io/openff/)
- [Modding manual](Docs/Modding.md)
- [Crystal, the editor](Docs/Editor.md)
- [Samples](Samples/)

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).

```
dotnet build OpenFF\OpenFF.csproj -c Release
```

`publish.cmd` builds the release zip. How the code is laid out is in [Architecture](Docs/Architecture.md).

## A note on Final Fantasy IV

OpenFF also has an in-progress Final Fantasy IV port, written by zerratar, and its code is still in this repository. However this release will not detect or play FF4. This release is made and tested for FF3 only. For FF4, see [zerratar's project](https://github.com/zerratar/openff) and [FF4 status](Docs/FF4-Status.md).

## Credits

- **zerratar** for [OpenFF](https://github.com/zerratar/openff), which this is built on.


## Game data and legal

OpenFF reads the game from your own install and never copies or ships its files. This repository contains no game data.

MIT licence, see [LICENSE](LICENSE). The code is for study, preservation and playing the copy you own in new ways, with no warranty. Final Fantasy is a trademark of Square Enix Co., Ltd. This project is not affiliated with or endorsed by Square Enix.
