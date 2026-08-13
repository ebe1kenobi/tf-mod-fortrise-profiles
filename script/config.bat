set REPO_DRIVE=D:
set TOWERFALL_DRIVE=C:

set BUILD_NAME=TFModFortRiseArcher
set DLL_MOD_FILE_NAME=TFModFortRiseArcher.dll
set PDB_MOD_FILE_NAME=TFModFortRiseArcher.pdb
set MODULE_DIR_NAME=tf-mod-fortrise-archer

REM Le mod s'appelle desormais Archer ; le REPERTOIRE, lui, n'a pas pu etre
REM renomme - VSCode et Visual Studio le tenaient ouvert. Quand il sera libre :
REM   ren D:\__dev\code\FR5tf-mod-fortrise-profiles FR5tf-mod-fortrise-archer
REM et remplacer profiles par archer dans la ligne qui suit. Rien d'autre a changer.
set REPO_PATH=D:\__dev\code\FR5tf-mod-fortrise-profiles\
set REPO_SCRIPT_PATH=%REPO_PATH%script\
set REPO_BUILD_PATH=%REPO_PATH%VSCode\bin\Debug\net10.0\
set REPO_RELEASE_PATH=%REPO_PATH%release\
set REPO_RELEASE_MOD_PATH=%REPO_RELEASE_PATH%%MODULE_DIR_NAME%\

set TOWERFALL_PATH="C:\Program Files (x86)\Steam\steamapps\common\TowerFall\FortRise\"
set TOWERFALL_MODS_PATH=%TOWERFALL_PATH%Mods\
set TOWERFALL_THIS_MODULE_PATH=%TOWERFALL_MODS_PATH%%MODULE_DIR_NAME%\
set TOWERFALL_THIS_MODULE_AUTO_PATH=%TOWERFALL_MODS_PATH%%BUILD_NAME%\
set EXECUTABLE=%TOWERFALL_PATH%FortRise.exe
