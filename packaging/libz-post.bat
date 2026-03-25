@echo off
set "SOLUTION_DIR=%~1"

echo ========================================
echo Iniciando proceso de empaquetado...
echo ========================================

REM Cambiar al directorio de Release
cd /d "%SOLUTION_DIR%FortyOne.AudioSwitcher\bin\Release"

echo.
echo [1/3] Verificando archivos...
if not exist "AudioSwitcher.exe" (
    echo ERROR: No se encuentra AudioSwitcher.exe
    echo Asegurate de haber compilado primero en Release
    pause
    exit /b 1
)

echo [2/3] Fusionando DLLs con ILMerge...
REM Usar ILMerge para combinar todas las DLLs en el ejecutable
"%SOLUTION_DIR%packages\ILMerge.2.14.1208\tools\ILMerge.exe" /target:winexe /out:AudioSwitcher_Merged.exe /wildcards AudioSwitcher.exe *.dll /log /targetplatform:v4,"C:\Windows\Microsoft.NET\Framework\v4.0.30319"

if errorlevel 1 (
    echo ERROR: ILMerge falló
    pause
    exit /b 1
)

echo [3/3] Inyectando recursos con LibZ...
REM Usar LibZ para el paso final de inyección
"%SOLUTION_DIR%packages\LibZ.Bootstrap.1.2.0.0\tools\libz" inject-dll --assembly=AudioSwitcher_Merged.exe --include=*.dll --key "%SOLUTION_DIR%FortyOne.AudioSwitcher\fortyone.snk" --move

if errorlevel 1 (
    echo ERROR: LibZ falló
    pause
    exit /b 1
)

REM Renombrar al nombre final
if exist "AudioSwitcher.exe" del /Q "AudioSwitcher.exe"
move /Y "AudioSwitcher_Merged.exe" "AudioSwitcher.exe" > nul

echo.
echo ========================================
echo ¡Ejecutable creado exitosamente!
echo Ubicacion: %SOLUTION_DIR%FortyOne.AudioSwitcher\bin\Release\AudioSwitcher.exe
echo ========================================