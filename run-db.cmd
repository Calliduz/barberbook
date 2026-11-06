@echo off
echo [BarberBook] Starting PostgreSQL server...
"%USERPROFILE%\.pgsql\pgsql\bin\postgres.exe" -D "%USERPROFILE%\.pgsql\data"

