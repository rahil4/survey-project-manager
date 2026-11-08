@echo off
rem ساخت فایل SurveyProjectManager.exe (نیاز به .NET SDK 8 از dotnet.microsoft.com)
dotnet publish src\SurveyProjectManager.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist || goto :error
echo.
echo Done: dist\SurveyProjectManager.exe
exit /b 0
:error
echo Build failed.
exit /b 1
