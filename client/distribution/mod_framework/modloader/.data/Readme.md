Mod Loader
==================

## What is it?

Mod Loader is a plugin for Grand Theft Auto III, Vice City and San Andreas that adds an easy and user-friendly way to install and uninstall modifications into the game, as if the game had official modding support. No changes are ever made to the original game files, everything is injected on the fly, at runtime!

The usage is as simple as inserting the mod files into the modloader/ directory. Uninstalling is as easy as that too, delete the mod files and you are done. Hot swapping mods while the game is running? By using Mod Loader you can!

Still not sure? Check out this nice [video](https://www.youtube.com/watch?v=TvRpQa8dJ7E) from Ivy.

## Installing Mod Loader

### GTA San Andreas

  Mod Loader requires an [ASI Loader](http://www.gtagarage.com/mods/show.php?id=21709). 
  
  Extract *modloader.asi* and the *modloader* folder into your game directory.

### GTA Vice City or GTA III:

  Mod Loader requires the [Ultimate ASI Loader](https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases).
  
  Extract *modloader.asi* into the *scripts/* directory and the *modloader* folder into your game directory.


## Installing Modifications

Extract the mod contents into a folder inside the *modloader* directory.

That means the following are valid installation methods:

 + modloader/nsx/infernus.dff
 + modloader/nsx/another folder/infernus.dff

But the following is **NOT** valid:

 - modloader/infernus.dff 
 - modloader/.data/infernus.dff


## Uninstalling Modifications

Even simpler, just delete the mod content from *modloader* directory.

If you just want to disable the mod for a while, go to the in-game menu and disable it or edit *modloader.ini* manually. 

## Highlights

- Do not replace **ANY** original file, never, really.
- Let Mod Loader take care of everything
- Data files merging. You can for example have 70 handling.cfg files at modloader and they'll all work perfectly fine.
- Readme files reading. No need to care about taking the data line from the readme file and placing in the data file, Mod Loader does that for you too!
- Refreshable mods. Change or add the files while the game is running and see the results immediately!
- Command line support. See *modloader/.data/Command Line Arguments.md*.
- In-Game menu for configurations. Go to *Options > Mod Configuration*.

### Have you found a bug?

Found a bug? Report it on [our issue tracker](https://github.com/thelink2012/modloader/issues).

Please include the *modloader/modloader.log* file created just after the crash and steps to reproduce.

### Supported Executables

Supported game versions:

 + GTA III 1.0
 + GTA VC 1.0
 + GTA SA 1.0 US
 + GTA SA 1.0 EU

## Download

You can download the latest version of Mod Loader from:

 * [GTA Garage](http://www.gtagarage.com/mods/show.php?id=25377) — latest stable build
 * [GitHub](https://github.com/thelink2012/modloader/releases) — latest builds, including unstable

## Source Code

Mod Loader is a open source project, feel free to learn and contribute.
The source code is licensed under the MIT License, check it out on [GitHub](https://github.com/thelink2012/modloader/).

## Contributors

Originally developed by LINK/2012 with contributions, support, and testing from

- Andryo
- ArtututuVidor$
- CookiePLMonster
- Fabio3rs
- Gramps
- Junior_Djjr
- JNRois12
- LINK/2012
- rokuniichi
- Silent
- ThirteenAG
- TheComputerGuy96
- TJGM

and many many other folks.