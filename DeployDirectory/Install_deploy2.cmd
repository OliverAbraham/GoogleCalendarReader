@ECHO off
@ECHO -----------------------------------------------------------------------------------
@ECHO Deploy-Skript zum Installieren einer neuen Version auf diesem Client
@ECHO Quelle                    : %SOURCE% 
@ECHO Interprozess-Kommunikation: %COMMUNICATION_DIR%
@ECHO Zielanwendung             : %DESTINATION%
@ECHO Anwendungsname            : %PROCESS_NAME%
@ECHO -----------------------------------------------------------------------------------



:update
call :kill_the_application
call :copy_new_files
call :start_application

:infinite_loop
    FOR /L %%G IN (1,1,6) DO (call :wait_10_seconds "%%G")
    if exist %COMMUNICATION_DIR%\Force_reboot.dat goto reboot_computer
    if exist %COMMUNICATION_DIR%\Force_application_close.dat goto update_application
    if exist %DESTINATION%\bin\restart-request.dat goto restart_application
    tasklist | find /N "%PROCESS_NAME%"
    if ERRORLEVEL 1 goto update_application
    goto infinite_loop


:reboot_computer
    echo Reboot is being done...
    shutdown -r
    echo . >%COMMUNICATION_DIR%\rebooting
    del     %COMMUNICATION_DIR%\Force_reboot.dat
    CHOICE /C:j /N /CS /T 120 /D j /M "waiting 120 seconds for reboot..."
    goto reboot_computer


:restart_application
    echo Application requested a hard restart.
    del %DESTINATION%\bin\restart-request.dat
    taskkill /IM %PROCESS_NAME%
    CHOICE /C:j /N /CS /T 10 /D j /M "waiting 10 seconds ..."
    goto update


:wait_10_seconds
    if exist %COMMUNICATION_DIR%\Force_application_close.dat GOTO :eof
    if exist %DESTINATION%\bin\restart-request.dat GOTO :eof
    CHOICE /C:j /N /CS /T 10 /D j /M "waiting 10 seconds, press j to proceed now..."
    GOTO :eof


:copy_new_files
    echo .
    echo .
    echo copying files...
    if exist    %COMMUNICATION_DIR%\Force_reboot.dat        del %COMMUNICATION_DIR%\Force_reboot.dat
    if exist    %COMMUNICATION_DIR%\rebooting               del %COMMUNICATION_DIR%\rebooting
    echo on
    xcopy %SOURCE%\bin\*                    %DESTINATION%\bin /Y /D
    xcopy %SOURCE%\*.cmd                    %DESTINATION%     /Y /D
    xcopy %SOURCE%\*.ttc                    %DESTINATION%     /Y /D
    
    rem copying back the log file to the server
    xcopy %DESTINATION%\bin\supervisorthread.log      %COMMUNICATION_DIR%    /Y /D
    
    echo   .>%COMMUNICATION_DIR%\Application_is_updated.dat
    if exist %COMMUNICATION_DIR%\Force_application_close.dat del %COMMUNICATION_DIR%\Force_application_close.dat
    if exist %DESTINATION%\bin\restart-request.dat           del %DESTINATION%\bin\restart-request.dat
    GOTO :eof


:start_application
    echo on
    pushd %DESTINATION%\bin
    start %START_NAME%
    popd
    echo off
    GOTO :eof


:kill_the_application
    CHOICE /C:j /N /CS /T 2 /D j /M "Killing the application process..."
    taskkill /IM %PROCESS_NAME%
    echo .>%COMMUNICATION_DIR%\Application_is_closed.dat
	CHOICE /C:j /N /CS /T 2 /D j /M "waiting..."
    GOTO :eof


:update_application
