# The HD textures download

On the first start, if no mod in `mods` carries the HD textures, the game offers to download them (the panel is `OpenFF/Compat/TexturesScreen.cs`, the work is `HdTextures.cs`). It answers to the keyboard, the mouse and the pad. "Not now" asks again next start, "Don't ask again" is kept in `userdata\settings.json` (`"hdTextures": "no"`), and `--get-textures` offers it again whatever was said.

## Making the pack

The pack is ONE mod, "FF3 HD Textures", with the portraits folded in. `Tools/make-texture-pack.py` builds it from the two mod folders:

```
python Tools/make-texture-pack.py --mod "mods/FF3 HD Textures" --portraits "mods/Retextured Portraits" --out dist/textures
```

- Textures that are the very same picture under different names are stored once (about half of them), and `pack.json` lists the other names; the game copies them back out when it unpacks. So the download is about 1.6 GB for 3.1 GB installed.
- Parts are kept under 1.9 GB each (GitHub's limit is 2 GiB a file); today it is one part, `hd-textures-1.zip`.
- Stray files (`BACKUP`, `ANNOTATED`, `Copy of`, `PUT-PNGS-HERE.txt`) are left out.
- It can be stopped and run again (`--budget <seconds>`); the hashes are cached in `_hashes.json` and finished parts are kept. Run it again whenever a texture changes.

## Publishing it

1. On the fork, make a release with the tag `hd-textures` (a pre-release, so it is never the "latest" the update check reads).
2. Attach `pack.json` and every `hd-textures-N.zip` from the output folder.
3. `HdTextures.PackUrl` already points at the fork (ausnick2001/OpenFF3-Enhanced), so the game reads `https://github.com/<owner>/OpenFF3-Enhanced/releases/download/hd-textures/pack.json`.

Every part is checked against the SHA-256 in `pack.json` before anything is unpacked. The pack is unpacked in `mods\.hd-textures-staging` (a dot folder is not a mod) and moved to `mods\FF3 HD Textures` whole, so a failed or cancelled download leaves nothing half-installed.

## Testing it without GitHub

Point the game at the output folder: `"OpenFF3 Enhanced.exe" --get-textures --textures-url=C:\path\to\textures\pack.json`. The parts are read from beside `pack.json`. To see a real first install, rename your `mods` folder out of the way first, and put it back afterwards (delete the new `mods\FF3 HD Textures` first). `--update-throttle=<KB/s>` slows the download to see the progress bar.
