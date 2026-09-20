@echo off
mode con: cols=120 lines=35 >nul 2>&1
title Publish RetroTank 1985 WASM (Cloudflare Pages)

echo ======================================================================
echo     Publishing RetroTank 1985 (WebAssembly - Cloudflare Pages)
echo ======================================================================

REM Generate Version Tag (Format: YYYYMMDD_HHMMSS)
set CUR_DATE=%DATE:~0,4%%DATE:~5,2%%DATE:~8,2%
set CUR_TIME=%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%
set CUR_TIME=%CUR_TIME: =0%
set VERSION_TAG=%CUR_DATE%_%CUR_TIME%

set SCRIPT_DIR=%~dp0
set PROJECT_PATH=%SCRIPT_DIR%RetroTank1985.Client\RetroTank1985.Client.csproj
set HOST_DIR=%SCRIPT_DIR%RetroTank1985
REM Output folder moved one level up into ..\publish\Wasm
set TARGET_FOLDER=%SCRIPT_DIR%..\publish\Wasm
set TARGET_WWWROOT=%TARGET_FOLDER%\wwwroot
set ZIP_NAME=RetroTank1985_Wasm_%VERSION_TAG%.zip
set CONFIGURATION=Release

echo Project:        %PROJECT_PATH%
echo Target Folder:  %TARGET_FOLDER%
echo Target wwwroot: %TARGET_WWWROOT%
echo Version Tag:    %VERSION_TAG%
echo Configuration:  %CONFIGURATION%
echo.

echo === [1/6] CLEAN PREVIOUS PUBLISH AND RESTORE ===
if exist "%TARGET_FOLDER%" (
    echo Cleaning previous publish directory...
    rmdir /s /q "%TARGET_FOLDER%"
)

dotnet restore "%PROJECT_PATH%"
if errorlevel 1 (
    echo [ERROR] Restore failed. Aborting publish.
    pause
    exit /b 1
)

echo.
echo === [2/6] PUBLISH STEP (Blazor WASM) ===
dotnet publish "%PROJECT_PATH%" ^
    --configuration %CONFIGURATION% ^
    --output "%TARGET_FOLDER%" ^
    /p:BlazorWebAssemblyEnableLinking=true ^
    /p:PublishTrimmed=true
if errorlevel 1 (
    echo [ERROR] Publish failed. Check your project settings.
    pause
    exit /b 1
)

echo.
echo === [3/6] WRITE VERSION AND PREPARE STATIC ASSETS ===
echo { "version": "%VERSION_TAG%", "app": "RetroTank1985", "date": "%CUR_DATE%" } > "%TARGET_WWWROOT%\version.json"


echo.
echo === [4/6] GENERATE STATIC HOSTING CONFIGS (_redirects and _headers) ===
REM SPA routing redirect for Cloudflare Pages / Netlify
echo /* /index.html 200 > "%TARGET_WWWROOT%\_redirects"

REM Header configurations for Cloudflare Pages
echo /*.wasm.br > "%TARGET_WWWROOT%\_headers"
echo   Content-Type: application/wasm >> "%TARGET_WWWROOT%\_headers"
echo   Content-Encoding: br >> "%TARGET_WWWROOT%\_headers"
echo   Cache-Control: no-cache >> "%TARGET_WWWROOT%\_headers"

echo /*.wasm.gz >> "%TARGET_WWWROOT%\_headers"
echo   Content-Type: application/wasm >> "%TARGET_WWWROOT%\_headers"
echo   Content-Encoding: gzip >> "%TARGET_WWWROOT%\_headers"
echo   Cache-Control: no-cache >> "%TARGET_WWWROOT%\_headers"

echo /version.json >> "%TARGET_WWWROOT%\_headers"
echo   Cache-Control: no-cache >> "%TARGET_WWWROOT%\_headers"

echo /manifest.json >> "%TARGET_WWWROOT%\_headers"
echo   Content-Type: application/manifest+json >> "%TARGET_WWWROOT%\_headers"
echo   Cache-Control: no-cache >> "%TARGET_WWWROOT%\_headers"

echo /service-worker.js >> "%TARGET_WWWROOT%\_headers"
echo   Content-Type: application/javascript >> "%TARGET_WWWROOT%\_headers"
echo   Cache-Control: no-cache >> "%TARGET_WWWROOT%\_headers"

echo /service-worker-assets.js >> "%TARGET_WWWROOT%\_headers"
echo   Content-Type: application/javascript >> "%TARGET_WWWROOT%\_headers"
echo   Cache-Control: no-cache >> "%TARGET_WWWROOT%\_headers"

echo.
echo === [5/6] PROCESS index.publish.html (IF APPLICABLE) ===
if exist "%TARGET_WWWROOT%\index.publish.html" (
    echo Replacing index.html with index.publish.html...
    copy /Y "%TARGET_WWWROOT%\index.publish.html" "%TARGET_WWWROOT%\index.html"
    if exist "%TARGET_WWWROOT%\index.publish.html.br" (
        copy /Y "%TARGET_WWWROOT%\index.publish.html.br" "%TARGET_WWWROOT%\index.html.br"
    )
    if exist "%TARGET_WWWROOT%\index.publish.html.gz" (
        copy /Y "%TARGET_WWWROOT%\index.publish.html.gz" "%TARGET_WWWROOT%\index.html.gz"
    )
) else (
    echo Standard index.html found. Skipping index.publish.html replacement.
)

echo.
echo === [6/6] ZIP wwwroot CONTENT ===
if exist "C:\Program Files\7-Zip\7z.exe" (
    echo Compressing using 7-Zip...
    "C:\Program Files\7-Zip\7z.exe" a "%TARGET_FOLDER%\%ZIP_NAME%" "%TARGET_WWWROOT%\*" -mx=9 -tzip
) else (
    echo 7-Zip not found at default location. Using PowerShell Compress-Archive...
    powershell -NoProfile -Command "Compress-Archive -Path '%TARGET_WWWROOT%\*' -DestinationPath '%TARGET_FOLDER%\%ZIP_NAME%' -Force"
)

if errorlevel 1 (
    echo [ERROR] Failed to create zip package.
    pause
    exit /b 1
)

echo.
echo ======================================================================
echo     PUBLISH COMPLETED SUCCESSFULLY!
echo ======================================================================
echo Output ZIP: %TARGET_FOLDER%\%ZIP_NAME%
echo Output Dir: %TARGET_WWWROOT%
echo ======================================================================
echo.
pause
