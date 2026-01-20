@echo off
REM Chemin vers sbox.exe (le CLIENT Steam, pas sbox-server.exe)
REM Adapte le chemin si besoin :
set SBOX_EXE="C:\Program Files (x86)\Steam\steamapps\common\sbox\sbox.exe"

REM Ident de ton jeu
set GAME_IDENT=victarrow.astrofront

%SBOX_EXE% ^
  +game "%GAME_IDENT%" ^
  +astro_start menu

pause
