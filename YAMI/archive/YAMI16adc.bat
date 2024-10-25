@echo off

rem ---
rem YAMI start batch file. This file will be executed by the ExpSuite FrameWork with the following parameters:
rem ---
rem %1 : sampling rate
rem %2 : audio device index
rem %3 : number of channels
rem %4 : MIDI out device index
rem %5 : MIDI in device index
rem %6-%14: additional parameters

rem --- which libraries to load?
set pd_LIB=-lib %~dp0\lib\iemlib1 -lib %~dp0\lib\iemlib2 -lib %~dp0\lib\OSC -lib %~dp0\lib\comport -lib %~dp0\lib\vasp -lib %~dp0\lib\zexy -lib %~dp0\lib\dyn~

rem --- pd directory
set pd_PDDIR=%~dp0..
rem --- any additional paths? (%~dp0 is the path of this file)
set pd_PATH=-path %~dp0abs

rem --- what patch open?
set pd_OPEN=-open %~dp0\patches\YAMI16adc.pd

rem --- Sampling rate? 
set pd_SRATE=%1

rem --- which device do you use?
set pd_DEVICE=%2
if %pd_DEVICE%==-1 ( set pd_DEVICE= ) else set pd_DEVICE=-audiodev %pd_DEVICE%

rem --- how many channels do you have?
set pd_CHANNELS=%3

rem --- MIDI out device
set pd_MIDIOUTDEV=%4
if %pd_MIDIOUTDEV%==0 ( set pd_MIDIOUTDEV= ) else set pd_MIDIOUTDEV=-midioutdev %pd_MIDIOUTDEV%

rem --- MIDI in device
set pd_MIDIINDEV=%5
if %pd_MIDIINDEV%==0 ( set pd_MIDIINDEV= ) else set pd_MIDIINDEV=-midiindev %pd_MIDIINDEV%

rem --- allow 5 more flags by shifting, up to 9 flags parameters in total...
SHIFT
SHIFT
SHIFT
SHIFT
SHIFT
set pd_FLAGS=%1 %2 %3 %4 %5 %6 %7 %8 %9

rem --- audio settings (adjust on every computer for sound interface)
set pd_AUDIO=-blocksize 64 -audiobuf 70

rem --- start Pd

echo on
%pd_PDDIR%\bin\pd -r %pd_SRATE% %pd_DEVICE% -channels %pd_CHANNELS% %pd_MIDIOUTDEV% %pd_MIDIINDEV% %pd_FLAGS% %pd_OPEN% %pd_AUDIO% %pd_LIB% %pd_PATH%