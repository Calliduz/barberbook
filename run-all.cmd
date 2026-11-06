@echo off
echo ========================================================
echo   BarberBook - Classic Grooming Management System
echo ========================================================

REM Check if postgres is running
tasklist /FI "IMAGENAME eq postgres.exe" 2>NUL | find /I /N "postgres.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [OK] PostgreSQL is already running.
) else (
    echo [..] Starting PostgreSQL in background...
    start /min "BarberBook-Postgres" "%USERPROFILE%\.pgsql\pgsql\bin\postgres.exe" -D "%USERPROFILE%\.pgsql\data"
    timeout /t 2 /nobreak >NUL
)

echo [..] Launching BarberBook Web at http://localhost:5000 ...
dotnet run --project src/BarberBook.Web --urls "http://localhost:5000"

