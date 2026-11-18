# OpenFF3 Enhanced

A more playable **Final Fantasy III** (the 3D remake) on PC. It runs from your own Steam or GOG copy as a native desktop game, and adds the things I always wanted while playing it: turn-based battles, a dungeon minimap, the Pixel Remaster's quality-of-life boosters, a menu that looks like the game's own, and sharper textures.

This is built on [zerratar's OpenFF](https://github.com/zerratar/openff), which did the hard work of getting FF3 running natively. If you like what's here, go and give that project some love too.

No game files are included. You need your own copy of Final Fantasy III.

<!-- TODO: screenshots. Suggested: the OpenFF3 Enhanced menu, a turn-based battle, the minimap, the HD textures. Put them in Docs/Images and link them here. -->

## What's new

**Battles**
- **Turn-based battles.** An FFX-style system: each character acts one at a time, in speed order, with the camera staying on the command view. No more entering the whole party's commands at once. Switch it on or off at any time (Quality of life menu).
- **Faster battles.** The spoils appear sooner, and the end-of-battle messages can be skipped quickly with Cross / B / A.
- **Encounter rate.** 0.5x, 1x, 2x or 4x. Too many random battles? Halve them and double the EXP and job points.

**The menus**
- **The OpenFF3 Enhanced menu.** Press **Esc** (or **Start** on a controller). It pauses the game, and has Settings, Quality of life, Minimap, Reset (back to the title screen) and Exit.
- **Looks like the game.** The menu and battle windows are restyled to match FF3's own party menu, hand cursor and all.
- **Quality of life.** Game speed, random encounters on or off, battle EXP and job EXP multipliers, save anywhere, no job change penalty, and an indicator in the top-left corner showing which of these are on.
- **Party and status screens** now show each hero's job level, and how many job points are left until the next one.
- **Controller icons** for PlayStation, Xbox, Switch or keyboard (Settings > Button icons).

**Dungeons**
- **A customisable minimap** (Esc > Minimap). Show or hide it, change its size and zoom, and choose whether chests and hidden walls are marked. Pick a completed map or an FF4-style "fog of war" map that fills in as you walk, and how much of it you uncover at a time. Be warned: it can spoil hidden passages.

**Looks and sound**
- **Sharper textures.** OpenFF3 Enhanced can use higher-quality textures and portraits. See *Texture packs* below.
- **Fonts.** The menus, dialogue, names and title screen each have their own font, and you can swap in your own. See *Fonts* below.
- **Story text is easier to read.** A pale glow sits behind the prologue and cutscene text so it reads over the pictures.
- **Smoother.** Frame pacing is smoothed, and there's a fix for the stutter that appeared with VSync on.

**Other**
- On the world map a full push of the stick no longer runs, which stopped the character moving faster than intended and clipping through walls.
- Shops: L / R switch between Buy and Sell on a controller.
- The Equipment menu on a controller can now reach "Remove" and "Remove all".
- Steam achievements, when you play from your Steam copy with Steam running.
- Company logos are skipped. Everything the game writes lives in one folder, `userdata`.

The full list, with the details, is in [Releases](Docs/Releases.md).

## Install

1. Install **Final Fantasy III** from Steam or GOG.
2. Download `OpenFF-<version>-win-x64.zip` from [Releases](../../releases) and unzip it anywhere. Nothing needs installing.
3. Run `OpenFF.exe`. It finds the game by itself.

You need Windows 10 or 11 (64-bit).

### Your saves and settings

Saves, settings and achievements are kept in a `userdata` folder next to `OpenFF.exe`. Delete the game's folder and it all goes with it, and you can move the whole thing to another machine. The first time you start the game it copies any older saves and settings across from your AppData folder (`%APPDATA%\FF3` and `%LOCALAPPDATA%\OpenFF`) and leaves the originals where they were.

To keep it somewhere else, start the game with `--data-dir=<folder>`.

## Texture packs

These are optional mods, so you only need them if you want the sharper look:

- **FF3 HD Textures**: higher-quality textures for the world, the title and menu hand, and the name entry screen. It's a few GB.
- **Retextured Portraits**: higher-quality character portraits.

<!-- TODO: where to download them. -->

To install one, put its folder in the `mods` folder next to `OpenFF.exe`. You can switch mods on and off, and change their order, from the title screen's MODS entry.

## Fonts

The `fonts` folder next to `OpenFF.exe` has a folder for each kind of text: `menus`, `dialogue`, `names` (the speaker's name and the cutscene text) and `title`. To use a font of your own, put a `.ttf`, `.otf` or `.ttc` file in the right folder and restart the game. If a folder holds several fonts, the first one by file name is used. To make your own font come first, start its name with an `a`, or take ours out.

If the dialogue looks too big or too small, put a text file called `scale.txt` in `fonts\dialogue` containing a number from 0.5 to 1.5. Without one the size is 0.88; the font that comes with the game is set to 0.75.

The fonts that come with this release, and their licences, are listed in `fonts\LICENSES.txt`.

## Controls

Arrow keys move, **Enter** confirms, **Backspace** cancels, **Esc** opens the OpenFF3 Enhanced menu. Game pads work too, with rebindable buttons. The full list is in [Controls](Docs/Controls.md).

Holding **Tab** fast-forwards. The Quality of life menu also has a game speed option, and **F8** / **Shift+F8** change it while you play.

## Command line

You don't need any of this to play. `OpenFF.exe --help` lists every switch, and [Command line](Docs/Command-Line.md) explains them. A few worth knowing:

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

OpenFF also has an in-progress Final Fantasy IV port, written by zerratar, and its code is still in this repository. This release is made and tested for FF3 only. For FF4, see [zerratar's project](https://github.com/zerratar/openff) and [FF4 status](Docs/FF4-Status.md).

## Credits

- **zerratar** for [OpenFF](https://github.com/zerratar/openff), which this is built on.
- The fonts' authors, in `fonts\LICENSES.txt`.

## Game data and legal

OpenFF reads the game from your own install and never copies or ships its files. This repository contains no game data.

MIT licence, see [LICENSE](LICENSE). The code is for study, preservation and playing the copy you own in new ways, with no warranty. Final Fantasy is a trademark of Square Enix Co., Ltd. This project is not affiliated with or endorsed by Square Enix.
