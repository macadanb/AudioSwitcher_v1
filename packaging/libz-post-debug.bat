@echo off
set "SOLUTION_DIR=%~1"

echo ========================================
echo Fusionando ejecutable DEBUG...
echo ========================================

cd /d "%SOLUTION_DIR%FortyOne.AudioSwitcher\bin\Debug"

echo [1/2] Fusionando DLLs con ILMerge...
"%SOLUTION_DIR%packages\ILMerge.2.14.1208\tools\ILMerge.exe" /target:winexe /out:AudioSwitcher_DEBUG_Merged.exe /wildcards AudioSwitcher.exe *.dll /log

echo [2/2] Inyectando recursos con LibZ...
"%SOLUTION_DIR%packages\LibZ.Bootstrap.1.2.0.0\tools\libz" inject-dll --assembly=AudioSwitcher_DEBUG_Merged.exe --include=*.dll --key "%SOLUTION_DIR%FortyOne.AudioSwitcher\fortyone.snk" --move

if exist "AudioSwitcher.exe" del /Q "AudioSwitcher.exe"
move /Y "AudioSwitcher_DEBUG_Merged.exe" "AudioSwitcher.exe" > nul

echo.
echo ========================================
echo ¡Ejecutable DEBUG fusionado creado!
echo Ubicacion: %SOLUTION_DIR%FortyOne.AudioSwitcher\bin\Debug\AudioSwitcher.exe
echo ========================================