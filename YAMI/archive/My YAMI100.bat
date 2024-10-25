
@rem --- where is pd.exe?
@set pd_BINDIR=..\bin

@rem --- where am I? (YAMI.bat)
@set YAMI_DIR=..\YAMI

@rem --- which libraries to load?
@set pd_LIB=-lib ..\YAMI\lib\iemlib1 -lib ..\YAMI\lib\iemlib2 -lib ..\YAMI\lib\OSC -lib ..\YAMI\lib\comport -lib ..\YAMI\lib\vasp -lib ..\YAMI\lib\zexy -lib ..\YAMI\lib\dyn~ -lib vbap -lib iemmatrix -lib partconv~ -lib define_loudspeakers

@rem --- how many channels do you have?
@set pd_CHANNELS=-channels 100

@rem --- which device do you use?
@set pd_DEF=-audioadddev "ASIO:ASIO MADIface USB"

@set pd_PATH=-path %YAMI_DIR%\abs -path %~dp0..\extra\vbap -path ..\extra\iemlib -path ..\extra\iemmatrix -path ..\extra\bsaylor

@set pd_OPEN=-open patches\YAMI100.pd
@set pd_ASIO=-asio
@set pd_SRATE=-r 48000
@set pd_AUDIO=-blocksize 64 -audiobuf 42


%pd_BINDIR%\pd %pd_SRATE% %pd_ASIO% %pd_CHANNELS% %pd_DEF% %pd_OPEN% %pd_AUDIO% %pd_LIB% -path %YAMI_DIR% %pd_PATH%