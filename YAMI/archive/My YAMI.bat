
@rem --- where is pd.exe?
@set pd_BINDIR=..\bin

@rem --- where am I? (YAMI.bat)
@set YAMI_DIR=..\YAMI

@rem --- which libraries to load?
@set pd_LIB=-lib ..\YAMI\lib\iemlib1 -lib ..\YAMI\lib\iemlib2 -lib ..\YAMI\lib\OSC -lib ..\YAMI\lib\comport -lib ..\YAMI\lib\vasp -lib ..\YAMI\lib\zexy -lib ..\YAMI\lib\dyn~


@rem --- how many channels do you have?
@set pd_CHANNELS=-channels 2

@rem --- which device do you use? (Not used in EXPSUITE)
@set pd_DEVICE=-audiodev 0

@rem --- which MIDI device(s) do you use?
@rem @set pd_MIDI=-midiindev 4 -midioutdev 2

@set pd_PATH=-path %YAMI_DIR%\abs -path %~dp0..\extra\vbap
@set pd_OPEN=-open patches\YAMI.pd
@rem @set pd_ASIO=-asio
@set pd_SRATE=-r 48000
@rem set pd_AUDIO=-blocksize 4 -audiobuf 42.8


%pd_BINDIR%\pd %pd_SRATE% %pd_ASIO% %pd_CHANNELS% %pd_DEVICE% %pd_MIDI% %pd_OPEN% %pd_AUDIO% %pd_LIB% -path %YAMI_DIR% %pd_PATH%