@echo off
rem Builds a ready-to-run OpenFF3 Enhanced folder for people who do not have the .NET SDK: the client
rem (OpenFF3 Enhanced.exe) and the engine mods reference, with the .NET runtime included, in dist\OpenFF3-Enhanced - and
rem zips it as dist\OpenFF3-Enhanced-<version>-win-x64.zip, which is what a GitHub release carries and the game updates
rem itself from. Crystal (the editor) goes in its own subfolder, "Crystal Modding Tools", so the root holds only the game.
rem The game itself is not in it: the client reads the Steam or GOG install on the machine it runs on.
rem
rem The version is the <Version> in OpenFF\OpenFF.csproj, Crystal.Editor\Crystal.Editor.csproj
rem and OpenFF.Engine\OpenFF.Engine.csproj; keep the three the same and this line with them.
setlocal
set VERSION=0.9.0
cd /d "%~dp0"
if exist dist\OpenFF3-Enhanced rmdir /s /q dist\OpenFF3-Enhanced
dotnet publish OpenFF\OpenFF.csproj -c Release -r win-x64 --self-contained true -o dist\OpenFF3-Enhanced -nologo -v q || goto fail
dotnet publish Crystal.Editor\Crystal.Editor.csproj -c Release -r win-x64 --self-contained true -o "dist\OpenFF3-Enhanced\Crystal Modding Tools" -nologo -v q || goto fail
rem The updater: a small native program (Native AOT - no .NET needed), which the client runs from a copy in the
rem temp folder to put a new release in place; the linker's tools are found through vswhere, from the VS Installer.
set "PATH=%PATH%;%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"
if exist dist\updater rmdir /s /q dist\updater
dotnet publish OpenFF.Updater\OpenFF.Updater.csproj -c Release -r win-x64 -o dist\updater -nologo -v q || goto fail
if not exist dist\OpenFF3-Enhanced\tools mkdir dist\OpenFF3-Enhanced\tools
copy /y dist\updater\OpenFF.Updater.exe dist\OpenFF3-Enhanced\tools\OpenFF.Updater.exe >nul || goto fail
rem Nothing a player does not need in the root: no crash-dump tool, no debug symbols or XML docs.
if exist dist\OpenFF3-Enhanced\createdump.exe del dist\OpenFF3-Enhanced\createdump.exe
del /q dist\OpenFF3-Enhanced\*.pdb dist\OpenFF3-Enhanced\*.xml 2>nul
del /q "dist\OpenFF3-Enhanced\Crystal Modding Tools\*.pdb" "dist\OpenFF3-Enhanced\Crystal Modding Tools\createdump.exe" 2>nul
if not exist dist\OpenFF3-Enhanced\mods mkdir dist\OpenFF3-Enhanced\mods
copy /y README.md dist\OpenFF3-Enhanced\README.md >nul
copy /y Docs\Modding.md "dist\OpenFF3-Enhanced\Crystal Modding Tools\Modding.md" >nul
copy /y LICENSE "dist\OpenFF3-Enhanced\Crystal Modding Tools\LICENSE.txt" >nul
copy /y Docs\Releases.md dist\OpenFF3-Enhanced\Releases.md >nul
rem RecompLauncher reads this from the top of the zip: one-click setup and settings for people who use it.
copy /y recomplauncher.json dist\OpenFF3-Enhanced\recomplauncher.json >nul || goto fail
copy /y LICENSE dist\OpenFF3-Enhanced\LICENSE.txt >nul
rem The sample mods, for Crystal's Sample projects (Samples\ beside crystal.exe); their build
rem output is left out. Nothing is put in mods\ - it starts empty, for the player's own mods.
robocopy Samples "dist\OpenFF3-Enhanced\Crystal Modding Tools\Samples" /e /xd bin obj /njh /njs /ndl /nfl /nc /ns >nul
rem The samples with code, built against this release's engine: each one's assembly beside its mod.json, so a sample
rem copied into mods\ plays as it stands with no .NET SDK (without it, Mastery's screens came up empty).
if exist dist\samples rmdir /s /q dist\samples
for %%S in (Fellowship HelloMod Mastery Survivors Rogue) do (
  dotnet build Samples\%%S\%%S.csproj -c Release -nologo -v q -o dist\samples\%%S || goto fail
)
copy /y dist\samples\Fellowship\Fellowship.dll "dist\OpenFF3-Enhanced\Crystal Modding Tools\Samples\Fellowship\" >nul || goto fail
copy /y dist\samples\HelloMod\Hello.dll "dist\OpenFF3-Enhanced\Crystal Modding Tools\Samples\HelloMod\" >nul || goto fail
copy /y dist\samples\Mastery\Mastery.dll "dist\OpenFF3-Enhanced\Crystal Modding Tools\Samples\Mastery\" >nul || goto fail
copy /y dist\samples\Survivors\Survivors.dll "dist\OpenFF3-Enhanced\Crystal Modding Tools\Samples\Survivors\" >nul || goto fail
copy /y dist\samples\Rogue\Rogue.dll "dist\OpenFF3-Enhanced\Crystal Modding Tools\Samples\Rogue\" >nul || goto fail
rem The PlayStation button pictures (Settings > Button icons), which the client reads from pad-icons beside OpenFF3 Enhanced.exe.
robocopy pad-icons dist\OpenFF3-Enhanced\pad-icons /e /njh /njs /ndl /nfl /nc /ns >nul
rem The fonts that come with it (fonts\menus, fonts\names and fonts\dialogue), which the client reads from fonts beside OpenFF3 Enhanced.exe.
robocopy fonts dist\OpenFF3-Enhanced\fonts /e /njh /njs /ndl /nfl /nc /ns >nul
rem The guide: the HTML tutorials Crystal opens with Help, readable on their own too.
if exist Docs\Guide robocopy Docs\Guide "dist\OpenFF3-Enhanced\Crystal Modding Tools\Guide" /e /njh /njs /ndl /nfl /nc /ns >nul
if exist dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip del dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip
powershell -NoProfile -Command "Compress-Archive -Path dist\OpenFF3-Enhanced -DestinationPath dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip -CompressionLevel Optimal" || goto fail
if not exist dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip goto fail
rem The zip's SHA-256, which the client checks a downloaded update against before it installs it.
powershell -NoProfile -Command "$f = [IO.File]::OpenRead((Resolve-Path 'dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip')); $h = -join ([Security.Cryptography.SHA256]::Create().ComputeHash($f) | ForEach-Object { $_.ToString('x2') }); $f.Close(); Set-Content -NoNewline -Encoding ascii 'dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip.sha256' ($h + '  OpenFF3-Enhanced-%VERSION%-win-x64.zip')" || goto fail
echo.
echo dist\OpenFF3-Enhanced: "OpenFF3 Enhanced.exe" plays, "Crystal Modding Tools\crystal.exe" edits, mods\ is where mods go (empty).
echo dist\OpenFF3-Enhanced-%VERSION%-win-x64.zip: the release, and its .sha256 beside it.
exit /b 0

:fail
echo publish failed
exit /b 1
