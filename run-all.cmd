@echo off
echo ========================================================
echo   BarberBook - Classic Grooming Management System
echo   Database: MongoDB Atlas (barberbook)
echo ========================================================

echo [..] Launching BarberBook Web at http://localhost:5000 ...
dotnet run --project src/BarberBook.Web --urls "http://localhost:5000"
