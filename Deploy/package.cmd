@ECHO OFF

SET SIGNTOOL="C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
IF NOT EXIST %SIGNTOOL% (
	ECHO This program requires SignTool installed at %SEVENZ%
	ECHO The SignTool is part of Windows SDK
	EXIT /B
)

SET SEVENZ="C:\Program Files\7-Zip\7z.exe"
IF NOT EXIST %SEVENZ% (
	ECHO This program requires 7-Zip installed at %SEVENZ%
	ECHO You may get it at http://www.7-zip.org/
	EXIT /B
)

ECHO Creating XML4web distribution package...

ECHO Preparing file system...
IF EXIST Distribution RMDIR /Q /S Distribution
IF EXIST XML4web.zip DEL XML4web.zip
IF EXIST XML4web-setup.exe DEL XML4web-setup.exe
MKDIR Distribution\lib

ECHO Copying documentation files...
COPY /Y ..\README.md Distribution
COPY /Y ..\LICENSE Distribution

ECHO Copying Altairis.XML4web.Compiler files...
COPY /Y ..\Altairis.XML4web.Compiler\bin\Debug\net47\*.dll Distribution\lib
COPY /Y ..\Altairis.XML4web.Compiler\bin\Debug\net47\*.exe Distribution
COPY /Y ..\Altairis.XML4web.Compiler\bin\Debug\net47\*.exe.config Distribution
ECHO.

ECHO Copying Altairis.XML4web.AzureSync files...
COPY /Y ..\Altairis.XML4web.AzureSync\bin\Debug\net47\*.dll Distribution\lib
COPY /Y ..\Altairis.XML4web.AzureSync\bin\Debug\net47\*.exe Distribution
COPY /Y ..\Altairis.XML4web.AzureSync\bin\Debug\net47\*.exe.config Distribution

ECHO Digitally signing EXE files...
%SIGNTOOL% sign /n "Altairis, s. r. o." /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 "Distribution\*.exe"

ECHO Making ZIP file...
CD Distribution
%SEVENZ% a ..\XML4web.zip *
CD ..

