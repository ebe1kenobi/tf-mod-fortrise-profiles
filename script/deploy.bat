@echo off

echo =================================
echo =       DEPLOY RELEASE          =
echo =================================

< NUL call config.bat

rem La tache MSBuild de FortRise.Configuration publie deja le mod sous le nom du
rem projet : on efface ce dossier en plus du notre, sinon deux copies du meme mod
rem se retrouvent chargees.
rmdir /S /Q %TOWERFALL_THIS_MODULE_PATH%
rmdir /S /Q %TOWERFALL_THIS_MODULE_AUTO_PATH%

xcopy /E /S /Y %REPO_RELEASE_PATH%* %TOWERFALL_MODS_PATH%
