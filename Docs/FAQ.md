# Frequently asked questions

Please read through these before you report a bug or ask for a feature. Many of the common questions are answered here, and some features people ask for are already in the game.

## Alternate Music/Soundtracks

**Can you add an option for the NES/Pixel Remaster soundtracks?**  
I cannot do this as this would involve distributing copyright material, if those soundtracks were included in the Steam version it would be a different story, but unfortunately they aren't. However you can insert your own custom tracks (must be .ogg or .wav, the game unfortunately does not support other formats) in the `custom music` folder in the games directory. See **Custom music** in the [README](../README.md#custom-music).
  

## Steam Features

**Can you please make this compatible with Steam achievements?**  
It is already compatible with Steam achievements, you just might not see them pop in-game but if you go into Steam you should see them recorded. If not, please let me know with a bug report.

**Can you please make this compatible with Steam cloud saves?**  
This unfortunately is not possible due to how different the saves are (and this project's saves have two files rather than one). If cloud saves are important to you I recommend downloading the game through the RecompLauncher which has cloud save support.

**Can I use my Steam save?**  
Yes. Drop the Steam game's `SAVE.BIN` into `userdata\saves` and the game converts it the next time it starts.  

  
## Other ports/games

**Are you going to port this to Android?**  
Maybe one day but I am focussing on getting this feature complete and bug free first.


**Are you going to do Final Fantasy 4 3D?**  
Probably not for a while. The code still isn't full decompiled and I have my hands full with this project as it is.


**The text boxes or text are too big or too small.**  
Esc > **Text and Menus**. You can change the dialogue box size, the dialogue text size, the speaker name size, and the text and menu shadows. Story scenes always keep their own look.


## Still not answered?

[Open an issue](https://github.com/ausnick2001/OpenFF3-Enhanced/issues/new/choose) on GitHub and pick the **Bug report** or **Feature request** form; it asks for what is needed. For a bug, say what you did, what you expected, and which version you are using (shown in the game's **Settings > Check for updates**, and in the name of the zip you downloaded). **Please attach the log.** It is the single most useful thing in a bug report, because it records what the game was doing when things went wrong, and without it a bug is often impossible to track down. Open the `logs` folder next to `OpenFF3 Enhanced.exe`, and attach the newest file from the run where the problem happened (drag it into the issue, or paste its contents). A screenshot helps too. Thank you for reporting bugs, request features and playing the project. It means a lot!
