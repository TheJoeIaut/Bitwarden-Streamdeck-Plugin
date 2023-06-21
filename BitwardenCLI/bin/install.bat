setlocal
cd /d %~dp0
cd %1

SET OUTPUT_DIR="C:\TEMP"
SET DISTRIBUTION_TOOL="C:\Users\juerg\source\repos\BitwardenCLI\StreamDeck-Tools Template1\bin\DistributionTool.exe"
SET STREAM_DECK_FILE="C:\Program Files\Elgato\StreamDeck\StreamDeck.exe"
SET STREAM_DECK_LOAD_TIMEOUT=7

taskkill /f /im streamdeck.exe
taskkill /f /im %2.exe
timeout /t 2
del %OUTPUT_DIR%\%2.streamDeckPlugin
%DISTRIBUTION_TOOL% -b -i %2.sdPlugin -o %OUTPUT_DIR%
rmdir %APPDATA%\Elgato\StreamDeck\Plugins\%2.sdPlugin /s /q
START "" %STREAM_DECK_FILE%
timeout /t %STREAM_DECK_LOAD_TIMEOUT%
%OUTPUT_DIR%\%2.streamDeckPlugin