for %%I in (.) do set slnFile=%%~nxI

..\tools\BuildTools.Console.exe version -i src\%slnFile%.sln
