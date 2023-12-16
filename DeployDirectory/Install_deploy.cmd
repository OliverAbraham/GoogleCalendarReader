:loop
pushd .
call setparams.cmd
xcopy %SOURCE%\*.cmd    %DESTINATION%     /Y /D
call install_deploy2.cmd
popd
goto loop
