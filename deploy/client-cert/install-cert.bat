@echo off
setlocal
REM ===========================================================================
REM  Loopback certificate installer  --  NO administrator rights required
REM
REM  Installs the Loopback root certificate for the CURRENT USER so Chrome, Edge
REM  and the Loopback desktop app trust the app over https and stop blocking
REM  downloads. Keep this file in the same folder as LoopbackRootCA.crt and
REM  double-click it.
REM ===========================================================================

set "CERT=%~dp0LoopbackRootCA.crt"

if not exist "%CERT%" (
  echo.
  echo   ERROR: LoopbackRootCA.crt was not found next to this file.
  echo   Keep install-cert.bat and LoopbackRootCA.crt together in one folder.
  echo.
  pause
  exit /b 1
)

echo.
echo   Installing the Loopback certificate for %USERNAME% (no admin needed)...
echo.

REM -user targets the current user's store (no admin); -f overwrites if present.
certutil -user -addstore -f Root "%CERT%"
if errorlevel 1 (
  echo.
  echo   Something went wrong installing the certificate.
  echo   If a security prompt appeared, click Yes and run this again.
  echo.
  pause
  exit /b 1
)

echo.
echo   Success! Fully close your browser (all windows), reopen it, and go to:
echo.
echo       https://192.168.1.200:5052
echo.
echo   Firefox users only: open about:config and set
echo   security.enterprise_roots.enabled to true (Chrome/Edge need nothing more).
echo.
pause
