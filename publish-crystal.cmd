@echo off
rem Builds Crystal, the editor, as dist\Crystal-Editing-Tools.zip: its own download, made once and attached by hand to
rem its own GitHub release ("Crystal editing tools"), not to the game's releases. The client updates itself from the
rem LATEST release's *-win-x64.zip, so that release must stay the game's: make Crystal's release a pre-release, or
rem untick "Set as the latest release".
setlocal
cd /d "%~dp0"
if exist dist\crystal rmdir /s /q dist\crystal
dotnet publish Crystal.Editor\Crystal.Editor.csproj -c Release -r win-x64 --self-contained true -o "dist\crystal\Crystal Modding Tools" -nologo -v q || goto fail
del /q "dist\crystal\Crystal Modding Tools\*.pdb" "dist\crystal\Crystal Modding Tools\createdump.exe" 2>nul
copy /y Docs\Modding.md "dist\crystal\Crystal Modding Tools\Modding.md" >nul
copy /y LICENSE "dist\crystal\Crystal Modding Tools\LICENSE.txt" >nul
rem The guide: the HTML tutorials Crystal opens with Help, readable on their own too.
if exist Docs\Guide robocopy Docs\Guide "dist\crystal\Crystal Modding Tools\Guide" /e /njh /njs /ndl /nfl /nc /ns >nul
if exist dist\Crystal-Editing-Tools.zip del dist\Crystal-Editing-Tools.zip
powershell -NoProfile -Command "Compress-Archive -Path 'dist\crystal\Crystal Modding Tools' -DestinationPath dist\Crystal-Editing-Tools.zip -CompressionLevel Optimal" || goto fail
if not exist dist\Crystal-Editing-Tools.zip goto fail
echo.
echo dist\Crystal-Editing-Tools.zip: Crystal, for its own release.
exit /b 0

:fail
echo publish failed
exit /b 1
