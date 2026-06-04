@echo off
cd /d "%~dp0"

echo Tworzenie wirtualnego srodowiska Python...
py -m venv venv

echo Instalowanie zaleznosci...
venv\Scripts\pip install --upgrade pip
venv\Scripts\pip install -r requirements.txt

echo.
echo Gotowe! Srodowisko wirtualne zostalo skonfigurowane w: %~dp0venv
pause
