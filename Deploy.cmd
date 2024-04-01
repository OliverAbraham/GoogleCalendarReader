@ECHO on
@ECHO -----------------------------------------------------------------------------------
@ECHO Deploy-Skript
@ECHO Zielanwendung: %DESTINATION%
@ECHO Interprozess-Kommunikation: %COMMUNICATION_DIR%
@ECHO -----------------------------------------------------------------------------------


del %COMMUNICATION_DIR%\Force_application_close.dat
del %COMMUNICATION_DIR%\Application_is_updated.dat


echo .
echo Dateien kopieren...
rem xcopy "DeployDirectory\*" 		%DESTINATION%   	/Y /D
xcopy "%SOURCE%" 				%DESTINATION%\bin   /Y /D /s

echo Increment version...
if not exist %COMMUNICATION_DIR%\Version echo 0 >%COMMUNICATION_DIR%\Version
Set /P COUNT=< %COMMUNICATION_DIR%\Version
Set /A COUNT=%COUNT%+1
echo %COUNT% >%COMMUNICATION_DIR%\Version
echo Current version is %COUNT%

echo .
echo Anwendung auffordern. sich zu beenden...
echo .>%COMMUNICATION_DIR%\Force_application_close.dat


rem goto dontwait
rem echo .
rem echo Warten, bis Anwendung sich beendet und upgedated hat...
rem :wait
rem @CHOICE /T 2 /M "Warten,bis Anwendung sich upgedated hat...  2 druecken zum uebergehen" /C:123 /CS /D 1
rem IF ERRORLEVEL 2 GOTO dontwait
rem if not exist %COMMUNICATION_DIR%\Application_is_updated.dat goto wait
rem :dontwait


rem echo .
rem echo Aufraeumen...
rem if exist  %COMMUNICATION_DIR%\Force_application_close.dat   del %COMMUNICATION_DIR%\Force_application_close.dat
rem if exist  %COMMUNICATION_DIR%\Application_is_updated.dat	del %COMMUNICATION_DIR%\Application_is_updated.dat
rem if exist  %COMMUNICATION_DIR%\Application_is_closed.dat	    del %COMMUNICATION_DIR%\Application_is_closed.dat

@CHOICE /T 2 /M "Files sind deployt" /C:123 /CS /D 1

