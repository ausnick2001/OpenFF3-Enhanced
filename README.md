[![OpenFF3 Enhanced: watch the showcase video](Docs/Images/banner-video.jpg)](https://www.youtube.com/watch?v=cnKPI1xDIpE)

# OpenFF3 Enhanced

An enhanced version of **Final Fantasy III** (3D remake) on PC in OpenFF. It runs from your own Steam or GOG copy as a native desktop game, and adds the things I always wanted while playing it: FFX style turn-based battles, a full customisable dungeon minimap, upscaled textures, encounter rate modifiers, a more aesthetically pleasing battle menu, easier to read fonts, and more!

<a href="https://recomplauncher.com/game/openff3-enhanced"><picture><source media="(prefers-color-scheme: dark)" srcset="https://recomplauncher.com/assets/press/launcher/badge-light.svg"><img alt="Get it on RecompLauncher" src="https://recomplauncher.com/assets/press/launcher/badge-dark.svg" height="48"></picture></a>

Get it on the RecompLauncher (click above), or alternatively download the latest release from the releases tab: https://github.com/ausnick2001/OpenFF3-Enhanced/releases

This is built on [zerratar's OpenFF](https://github.com/zerratar/openff), which did the hard work of getting FF3 3Ds code decompiled. If you like what's here, go and give that project some love too.

I have always disliked JRPGs that have the combat system where your entire party puts in their commands and then you watch the round play out. It has always felt slow and clunky to me, and as a result I have never been able to stomach FF3. This all started as a very simple project to see if turn based battles were even feasible. As it turns out, implementing them as a mod was relatively simple, and in my handful of hours of playing it, does not seem to hurt the balance of the game too much. One thing led to another and before you know it I had suddenly implemented multiple optimisations, bug fixes, additional quality of life options, a minimap, and even AI upscaled textures. Despite never having coded or released anything before - I decided to release this publicly. When I started this was only for personal use but I figured other people may enjoy and want to build off what I have done here.

I wanted to keep the changes as modular and adjustable as possible with the emphasis on being able to enjoy the game the way you want to:

- Use turn based battles or don't.

- Use the minimap or don't.

- Make the minimap massive, or tiny. Make it opaque, make it transparent. Make it display unopened treasure chests, or hide them. Make it already complete or make it so you can uncover it as you go (fog of war), FF4 3D style.

- Use encounter rate modifiers, or don't.

- Use enhanced textures, or don't.

- Use the included sharper fonts, or don't. If you don't like the new fonts, that is fair! Download your own and put them in the fonts folder.


![OpenFF3 Enhanced: before and after](Docs/Images/hero-1.jpg)

![OpenFF3 Enhanced: before and after](Docs/Images/hero-2.jpg)

![OpenFF3 Enhanced: before and after](Docs/Images/hero-3.jpg)

> [!IMPORTANT]
> **You need your own copy of Final Fantasy III.** This download does not include the game. OpenFF3 Enhanced only works if you already own **Final Fantasy III** and have it installed from **Steam or GOG**. It reads the game from your own install, never copies or ships its files, and never changes them. Without the game it will not run.

## What's new

**Battles**

![Before and after](Docs/Images/shot-full-battle.jpg)



- **Turn-based battles.** An FFX-style system: each character acts one at a time, in speed order, with the camera staying on the command view. No more having to put the entire party's commands in at once to act. Enabled through the quality of life options menu. Switch it on or off at any time in OpenFF3s Quality of Life menu (start on the controller or esc on keyboard).
- **Faster battles.** The spoils and exp messages appear sooner, and can be skipped quickly with Cross/B/A.
- **Encounter rate modifier.** 0x (none), 0.5x, 1x, 2x or 4x. Too many random battles? Halve them and double the EXP and job points.
- **More aesthetic menu design** The battle menu and OpenFF3 Enhanced menu have been redesigned to be more cohesive with the style of the party menu and dialogue boxes.

**Dungeons**
- **A customisable minimap** (Esc/Start > Minimap). Show or hide it, change its size and zoom, and choose whether unopened chests are marked. You can choose between a completed map or an FF4-style "fog of war" map that fills in as you walk (no rewards for completing the map...sorry). You can even set how much of the map your character will uncover as they walk if you are finding it too narrow. Be wary though that higher settings (and minimap use in general) may spoil the location of hidden passages.

**The menus**

![Before and after](Docs/Images/shot-quality-of-life.jpg)

![Before and after](Docs/Images/shot-status-screen.jpg)

- **The OpenFF3 Enhanced menu.** Press **Esc** (or **Start** on a controller). It pauses the game, and has Settings, Quality of Life, Minimap, Report a Bug / Request a Feature (opens GitHub), Reset (back to the title screen) and Exit.
- **Aesthetic consistency** The menu and battle windows are restyled to match FF3's own party menu, hand cursor and all.
- **Quality of Life options** Game speed, random encounter multipliers, battle EXP and job EXP multipliers, save anywhere, no job change penalty, and an indicator in the top-left corner showing which of these are on.
- **Party and status screens** now show each hero's job level, and how many job points are left until the next level.
- **Controller icons** for PlayStation, Xbox, Switch or keyboard (Start/Esc > Settings > Button icons). Game ships with enhanced playstation icons.

**Visuals**

![Before and after](Docs/Images/shot-portrait.jpg)

![Before and after](Docs/Images/shot-prologue.jpg)

![Before and after](Docs/Images/shot-dialogue-box.jpg)

- **Sharper textures.** OpenFF3 Enhanced can use higher-quality textures and portraits. Some changes to the code were necessary for compatibility with my HD texture pack.
- **Fonts.** The menus, dialogue, names and title screen each have their own font, and you can swap in your own. See *Fonts* below.
- **Custom music.** Replace any of the game's tunes with your own music. See *Custom music* below.
- **Story text is easier to read.** A pale glow sits behind the prologue and cutscene text so it reads more easily over the pictures.
- **Smoother.** Frame pacing is smoothed, and there's a fix for the stutter that appeared with VSync on.

**Fixes**
- On the world map a full push of the stick no longer runs, which stopped the character moving faster than intended and clipping through walls.
- Shops: Originally it was impossible to move between Buy and Sell on the shop menu. This has been fixed. L / R switch between Buy and Sell on a controller.
- The Equipment menu: on a controller it was impossible to select "remove" and "remove all". These can both now be reached by pressing circle/B/A on your controller which takes you to the upper command menu where you can now select them.
- Controller configuration: rebinding a button no longer takes the press that chose the row as the new button too.
- Map and party leader: there was no way to open the world map with a controller or keyboard, and the game's Select-to-change-leader had never been connected. Y/M now opens the map and Select changes the leader.
- HD textures: removed the thin pale outline that showed around trees and other cut-out sprites, such as the big pines in the battle field in Ur.
- Company logos on startup are skipped. 
- Everything the game writes lives in one folder, `userdata` which is in the same directory as the game. All contained in one folder.

The full list, with the details, is in [Releases](Docs/Releases.md).


## Install
<a href="https://recomplauncher.com/game/openff3-enhanced"><picture><source media="(prefers-color-scheme: dark)" srcset="https://recomplauncher.com/assets/press/launcher/badge-light.svg"><img alt="Get it on RecompLauncher" src="https://recomplauncher.com/assets/press/launcher/badge-dark.svg" height="48"></picture></a>

The easiest way to install is with the RecompLauncher (click above). However installing it manually is also very easy:

1. Install **Final Fantasy III** from Steam or GOG.
2. Download `OpenFF3-Enhanced-<version>-win-x64.zip` from [Releases](../../releases) and unzip it anywhere. Nothing needs installing.
3. Run `OpenFF3 Enhanced.exe`. It finds the game by itself.
4. The first time you start it, the game offers to download the **HD textures**. They are highly recommended. Choose *Download now*, wait for it to finish, and the game restarts with them on. You can also pick *Not now* (asked again next time) or *Don't ask again*, and add them later from **Settings > Redownload HD textures** on the title screen (it also reinstalls them, for example after an interrupted download; the old files are deleted first, after asking). To turn them off, untick **FF3 HD Textures** in the MODS list on the title screen. To remove them completely, delete the **FF3 HD Textures** folder inside the `mods` folder (it is about 3 GB).

> [!NOTE]
> Windows may show a "Windows protected your PC" message the first time you run it, because the game is not code-signed. Choose *More info*, then *Run anyway*.

> [!WARNING]
> The HD textures are a decent size download (about 1.5 GB) and take about 3 GB of disk space once unpacked. Make sure you have the room before you choose *Download now*.

You need Windows 10 or 11 (64-bit).

### Your saves and settings

Saves, settings and achievements are kept in a `userdata` folder next to `OpenFF3 Enhanced.exe`. Delete the game's folder and it all goes with it. The first time you start the game it copies any older saves and settings across from your AppData folder (`%APPDATA%\FF3` and `%LOCALAPPDATA%\OpenFF`) and leaves the originals where they were.

> [!NOTE]
> Back up the `userdata` folder before you delete or move the game's folder: your saves live in it.

**Steam saves:** drop the Steam game's own `SAVE.BIN` into `userdata\saves` and the game converts it to its own format the next time it starts (the original is kept as `save.steam-backup.bin`). A `save.bin` of your own is never overwritten; the conversion is then written as `save.from-steam.bin`.

If you prefer to keep your saves/user data somewhere else, start the game with `--data-dir=<folder>`.

## Fonts

The `fonts` folder next to `OpenFF3 Enhanced.exe` has a folder for each kind of text: `menus`, `dialogue`, `names` (the speaker's name and the cutscene text) and `title`. To use a font of your own, put a `.ttf`, `.otf` or `.ttc` file in the right folder and restart the game. If a folder holds several fonts, the first one by file name is used.

### Dialogue and text size

Open the menu with **Esc** (**Start** on a pad) and choose **Text and Menus**. There you can change, and see the result straight away:

- **Dialogue box size** (60% to 100%): a smaller box takes up less of the screen. The text moves with it.
- **Dialogue text size** (70% to 130%): the size of the words said.
- **Speaker name size** (70% to 130%): the size of the name above them.
- **Dialogue shadow** and **Menu text shadow**: how dark the shadow behind the text is: Off, Light, Medium (the default) or Heavy. Each is set on its own. Battle text and the story scenes keep their own look.
- **Battle menus**: shrink the battle command list and help bar (**Menu size**), the party's names and health (**Party menu size**) and the text in them (**Text size**), each down to 75% in 5% steps. Changing these during a battle looks wrong until the next battle starts. The boxes for choosing a target, the spell and item lists and the equipment screens shrink with them. **Reduce clutter in battle** takes away the lists of names when choosing a target: the hand stands on the enemy (its name shows in the bar at the top) or on the hero's line of the party menu. It is on by default; switch it off to get the lists back.
- **Battle descriptions**: Off, Minimal or On (the default). These are the descriptions at the top of the battle screen for commands, spells, songs and items. Minimal skips the obvious commands (Attack, Guard, Magic and so on) and keeps the rest.
- **Damage popups**: opens a page for how damage, healing, hit counts, critical hits and misses appear in battle. **Style** is Original (the game's own numbers), Original (upscaled), the default, which moves the same way in a sharper font, or Bouncy, with each digit hopping in turn. For the other styles you can also pick the colour (red, white or gold), the size (small, medium or large) and the shadow (off, light, medium or heavy).

Prologue and cutscene text isn't affected by these, so the story scenes always look as they were meant to.


## Custom music

The `custom music` folder next to `OpenFF3 Enhanced.exe` (made the first time the game plays a tune) has a folder for each of the game's 59 tunes, `BGM00` to `BGM58`. To replace a tune, put one `.ogg` or plain 16-bit `.wav` file in its folder and start the game again; a folder left empty keeps the game's own music. A tune the game loops has your file looped from end to start, so trim it to loop cleanly; a jingle or fanfare the game plays once has your file played once. `.mp3` and `.flac` files are not read, so convert them first.

Some folders are named for where the tune plays (`BGM55 - Battle`, `BGM56 - Boss battle`, `BGM57 - Final boss battle`, `BGM58 - Victory fanfare`, `BGM00 - Title screen` and a few towns and dungeons); the rest are just `BGM01`, `BGM02` and so on, and you will need to work out by ear which is which. The name hints come from the game's own data and may be wrong. The number at the start of a folder's name is what counts, so you can rename a folder as long as it keeps its number. Only use music you have the right to use.

## Controls

Arrow keys move, **Enter** confirms, **Backspace** cancels, **Esc** opens the OpenFF3 Enhanced menu. Holding **Tab** fast-forwards. The Quality of Life menu also has a game speed option, and **F8** / **Shift+F8** change it while you play.
Gamepads work too, with rebindable buttons (Settings > Controller configuration). L3 toggles the turbo, R3 toggles autobattle, Y (or **M** on the keyboard) opens the world map on the overworld, and Select changes the party leader. Pressing start brings up the OpenFF3 Enhanced menu where you can rebind the controls to your liking. The full list is in [Controls](Docs/Controls.md).


## Command line

You don't need any of this to play. `"OpenFF3 Enhanced.exe" --help` lists every switch, and [Command line](Docs/Command-Line.md) explains them. A few worth knowing:

- `--data-dir=<folder>`: where saves and settings are kept.
- `--show-logos`: show the company logos at the start.
- `--show-field-buttons`: show the on-screen Map and Menu buttons, which are hidden by default.
- `--no-achievements`: don't send Steam achievements.
- `--log-stalls`: write a note to the logs whenever a frame takes over 50 ms, for tracking down stutter.

## Making mods

OpenFF3 Enhanced keeps everything zerratar's OpenFF can do: mods can change models, textures, text, sounds, maps, menus and even run C# code. **Crystal** (`crystal.exe`) is the editor for making them.

The Crystal editor has been separated from the main game and sits in its own `Crystal Modding Tools` folder, for those who wish to edit the game. Start the game once, then run `crystal.exe` from that folder.

> [!CAUTION]
> Mods can run code on your computer. Only install mods from people you trust.

- [The modding guide](https://zerratar.github.io/openff/)
- [Modding manual](Docs/Modding.md)
- [Crystal, the editor](Docs/Editor.md)
- [Samples](Samples/) (experimental mods from zerratar, to show what the modding tools can do; not supported and not included in releases)

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
- **Panda Venom** for their amazing work on the [textures for the PSP version](https://www.reddit.com/r/GameUpscale/comments/1cztzsz/psp_final_fantasy_iii_us_hd_textures/). Most of the HD textures here were made by me with the help of AI, but around 100 of them I struggled to get right, and Panda Venom very kindly let me use parts of their textures to fix mine. I don't really want or need donations for this project, but if you feel like giving someone a donation, please consider giving a few $ to them. Their Ko-Fi page is https://ko-fi.com/pandavenom
- **How the HD textures were made:** most were upscaled with [Upscayl](https://upscayl.org) using its standard and Digital Art models, then combined and touched up by hand. Parts of some textures were repainted with [Fooocus](https://github.com/lllyasviel/Fooocus) and the inpainting model it comes with. Faces and a few problem textures were corrected with small Python scripts (Pillow, OpenCV and NumPy). Models and textures were checked in the Crystal editor, and the game's own formats were read with OpenFF's tools.
- **Claude**, which I worked with throughout, writing and debugging much of the code and helping with the texture fixes.


## Game data and legal

OpenFF reads the game from your own install and never copies or ships its files. This repository contains no game data.

MIT licence, see [LICENSE](LICENSE). The code is for study, preservation and playing the copy you own in new ways, with no warranty. Final Fantasy is a trademark of Square Enix Co., Ltd. This project is not affiliated with or endorsed by Square Enix.
