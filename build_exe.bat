@echo off
rem ساخت فایل SurveyProjectManager.exe (نیاز به پایتون ۳.۱۰ یا بالاتر)
python -m venv .venv || goto :error
call .venv\Scripts\activate.bat
pip install -r requirements-dev.txt || goto :error
pyinstaller --noconfirm --onefile --windowed --name SurveyProjectManager --paths src run.py || goto :error
echo.
echo Done: dist\SurveyProjectManager.exe
exit /b 0
:error
echo Build failed.
exit /b 1
