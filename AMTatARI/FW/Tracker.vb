Option Strict On
Option Explicit On
#Region "ExpSuite License"
'ExpSuite - software framework for applications to perform experiments (related but not limited to psychoacoustics).
'Copyright (C) 2003-2021 Acoustics Research Institute - Austrian Academy of Sciences; Piotr Majdak and Michael Mihocic
'Licensed under the EUPL, Version 1.2 or – as soon they will be approved by the European Commission - subsequent versions of the EUPL (the "Licence")
'You may not use this work except in compliance with the Licence. 
'You may obtain a copy of the Licence at: http://joinup.ec.europa.eu/software/page/eupl
'Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
'See the Licence for the specific language governing  permissions and limitations under the Licence. 
#End Region
''' <summary>
''' FrameWork - Tracker support.
''' </summary>
''' <remarks></remarks>
Module Tracker
    ''' <summary>
    ''' Experimental response from (Oculus) Tracker.
    ''' </summary>
    ''' <remarks><li>-1: no response</li>
    ''' <li>other values: see key handling from ExpSuite</li></remarks>
    Public glTrackerTriggerResponse As Integer = -1

    Private mblnSimulation As Boolean
    Private mlSensorCount As Integer
    Private mlSensorRequest As Integer

    ''' <summary>
    ''' Tracker Sensor data.
    ''' </summary>
    ''' <remarks></remarks>
    Public Structure TrackerSensor
        ''' <summary>
        ''' X position in cm
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngX As Double
        ''' <summary>
        ''' Y position in cm
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngY As Double
        ''' <summary>
        ''' Z position in cm
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngZ As Double
        ''' <summary>
        ''' Azimuth angle in degrees
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngA As Double
        ''' <summary>
        ''' Elevation angle in degrees
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngE As Double
        ''' <summary>
        ''' Roll in degrees
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngR As Double
        ''' <summary>
        ''' Component X of quaternion
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngQX As Double
        ''' <summary>
        ''' Component Y of quaternion
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngQY As Double
        ''' <summary>
        ''' Component Z of quaternion
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngQZ As Double
        ''' <summary>
        ''' Component W of quaternion
        ''' </summary>
        ''' <remarks></remarks>
        Dim sngQW As Double
        ''' <summary>
        ''' TRUE if the values are valid
        ''' </summary>
        ''' <remarks></remarks>
        Dim blnValid As Boolean
        ''' <summary>
        ''' Status of the sensor (used dependent on the data)
        ''' </summary>
        ''' <remarks></remarks>
        Dim lStatus As Integer
        Dim nCameras As Integer
        Dim visible As Boolean
    End Structure

    Private mtsData(13) As TrackerSensor ' max. 14 sensors
    Private mlMinTracked() As Integer
    Private mlMinStatus() As Integer
    Private mlInRange() As Integer
    Private mlMaxTracked() As Integer
    Private mlMaxStatus() As Integer
    Private currentMin As TrackerSensor
    Private currentMax As TrackerSensor
    Private currentTrackerValue As TrackerSensor
    Private calibrationValue As TrackerSensor ' sensor values stored when pressing the Calibrate Optitrack button
    Private RotM(2, 2) As Double ' rotation matrix for the calibration compensation
    Private calibrated As Boolean = False
    Private tsMinT, tsMaxT As TrackerSensor
    Private firstFrameReceived As Boolean = False
    Private lostTrackingCount As Integer = 0

    Private mcurTic As Long
    Private mlSensor As Integer
    Private mszDim As String

    Public Function Init(ByVal lCOM As Integer, ByVal lBaudRate As Integer, ByVal lSensorCount As Integer, ByVal blnSimulation As Boolean, ByVal lRepRate As Integer, ByVal sngPosScaling As Double) As String

        Dim lTO As Integer
        Dim curToc, curTicc As Long

        mblnSimulation = blnSimulation
        mlSensorCount = lSensorCount
        ReDim mlMinTracked(lSensorCount - 1)
        ReDim mlMaxTracked(lSensorCount - 1)
        ReDim mlMinStatus(lSensorCount - 1)
        ReDim mlMaxStatus(lSensorCount - 1)
        ReDim mlInRange(lSensorCount - 1)

        If glTrackerMode = 2 Then
            ' Tracker expected in ViWo (Unity)
            frmMain.SetStatus("Init Tracker: Expected in ViWo")
            Return ""
        ElseIf glTrackerMode = 3 Then
            ' OptiTrack (Motive OSC Streamer)
            If Not FindProcess("MotiveOscStreamer.exe") Then
                frmMain.SetStatus("Initializing Tracker: " & OSCstreamerFile & " on address 127.0.0.1 port " & TStr(motiveUDPport) & " with settings " & motiveFile & "...")
                Shell("""" & OSCstreamerFile & """ """ & motiveFile & """ 127.0.0.1 " & TStr(motiveUDPport))
            Else
                frmMain.SetStatus("Tracker already running...")
            End If
            ' Init current tracker value to 0
            mtsData(0).sngX = 0
            mtsData(0).sngY = 0
            mtsData(0).sngZ = 0
            mtsData(0).sngA = 0
            mtsData(0).sngE = 0
            mtsData(0).sngR = 0
            mtsData(0).sngQX = 0
            mtsData(0).sngQY = 0
            mtsData(0).sngQZ = 0
            mtsData(0).sngQW = 1
            ' Init other variables
            currentMin = mtsData(0)
            currentMax = mtsData(0)
            calibrationValue = mtsData(0)
            For i As Integer = 0 To 2
                For j As Integer = 0 To 2
                    If i = j Then
                        RotM(i, j) = 1
                    Else
                        RotM(i, j) = 0
                    End If
                Next
            Next
            mlMinTracked(0) = 0
            mlMaxTracked(0) = 0
            firstFrameReceived = False
            Return ""
        Else
            ' Tracker expected in the Output device (YAMI)   
            frmMain.SetStatus("Init Tracker: Expected in YAMI")

            ' abstraction "tracker" laden
            Output.Send("/Tracker/Load")
            QueryPerformanceCounter(mcurTic)
            mcurTic = mcurTic + CLng(0.3 * gcurHPFrequency)
            While (Not gblnCancel) And (curToc < mcurTic)
                Windows.Forms.Application.DoEvents()
                QueryPerformanceCounter(curToc)
            End While

            ' init serial port
            frmMain.SetStatus("Init serial port")
            Output.Send("/Tracker/InitCOM", CSng(lCOM - 1), CSng(lBaudRate))
            QueryPerformanceCounter(mcurTic)
            mcurTic = mcurTic + CLng(0.2 * gcurHPFrequency)
            While (Not gblnCancel) And (curToc < mcurTic)
                Windows.Forms.Application.DoEvents()
                QueryPerformanceCounter(curToc)
            End While
            ' init FoB
            frmMain.SetStatus("Get tracker status")
            Output.Send("/Tracker/Simulation", CSng(-1 * CInt(blnSimulation)))
            mlSensorRequest = 1
            Output.Send("/Tracker/GetFoBStatus")
            QueryPerformanceCounter(mcurTic)
            mcurTic = mcurTic + CLng(2 * gcurHPFrequency)
            While CBool(mlSensorRequest) And (Not gblnCancel) And (curToc < mcurTic)
                Windows.Forms.Application.DoEvents()
                QueryPerformanceCounter(curToc)
            End While
            If gblnCancel Then Return "Canceled"
            If curToc >= mcurTic Then Return "Time Out waiting for GetFoBStatus"
            If (mtsData(0).lStatus And 64) = 0 Then
                ' master not running - cold start with much delay
                frmMain.SetStatus("Tracker: Cold start")
                lTO = 15000
            ElseIf (mtsData(1).lStatus And 64) = 0 Then
                ' master: RUN, slave: NOT RUN;
                If lSensorCount = 1 Then
                    frmMain.SetStatus("Tracker: Master ready")
                    lTO = 2000
                Else
                    frmMain.SetStatus("Tracker: Master ready, waiting for Slave")
                    lTO = 5000
                End If
            Else
                ' master: RUN, slave: RUN
                If lSensorCount = 1 Then
                    frmMain.SetStatus("Tracker: Disconnecting Slave")
                    lTO = 5000
                Else
                    frmMain.SetStatus("Tracker: Master & Slave ready")
                    lTO = 2000
                End If
            End If
            mtsData(0).blnValid = False
            mtsData(1).blnValid = False

            QueryPerformanceCounter(mcurTic)
            mcurTic = mcurTic + CLng(0.2 * gcurHPFrequency)
            While (Not gblnCancel) And (curToc < mcurTic)
                Windows.Forms.Application.DoEvents()
                QueryPerformanceCounter(curToc)
            End While
            mlSensorRequest = 1
            Output.Send("/Tracker/InitFoB", CSng(lSensorCount), CSng(lTO), CSng(lRepRate), CSng(sngPosScaling))
            QueryPerformanceCounter(mcurTic)
            curTicc = mcurTic
            mcurTic = mcurTic + CLng(lTO * 1.5 * gcurHPFrequency / 1000) ' wait 50% longer than Tracker
            While CBool(mlSensorRequest) And (Not gblnCancel) And (curToc < mcurTic)
                Windows.Forms.Application.DoEvents()
                QueryPerformanceCounter(curToc)
                frmMain.SetProgressbar(CSng((curToc - curTicc) / (mcurTic - curTicc) * 100))
            End While
            If gblnCancel Then Return "Canceled"
            If mlSensorRequest > 0 Then Return "Time Out waiting for InitFoB"
            frmMain.SetProgressbar(0)

            mlSensorRequest = 0
            Return ""
        End If ' Tracker in Output

    End Function

    ''' <summary>
    ''' Set current tracker values.
    ''' </summary>
    ''' <param name="lS">Sensor index.</param>
    ''' <param name="sngX">X-value</param>
    ''' <param name="sngY">Y-value</param>
    ''' <param name="sngZ">Z-value</param>
    ''' <param name="sngA">A-value</param>
    ''' <param name="sngE">E-value</param>
    ''' <param name="sngR">R-value</param>
    ''' <returns>Error text if an error occures.</returns>
    ''' <remarks></remarks>
    Public Function SetCurrentValues(ByVal lS As Integer, ByVal sngX As Double, ByVal sngY As Double, ByVal sngZ As Double, ByVal sngA As Double, ByVal sngE As Double, ByVal sngR As Double) As String

        If lS >= mlSensorCount Then SetCurrentValues = "Sensor index out of bound." : Exit Function

        'If Not mblnSimulation Then
        If glTrackerMode = 2 Then
            ViWo.Send("/Tracker/SetCurrentValues/" & TStr(lS), sngX, sngY, sngZ, sngA, sngE, sngR)
        ElseIf glTrackerMode = 3 Then
            ' TO DO
        Else
            Output.Send("/Tracker/SetCurrentValues/" & TStr(lS), sngX, sngY, sngZ, sngA, sngE, sngR)
        End If
        'End If
        '  mtsData(lS).sngA = sngA
        '  mtsData(lS).sngE = sngE
        '  mtsData(lS).sngR = sngR
        '  mtsData(lS).sngX = sngX
        '  mtsData(lS).sngY = sngY
        '  mtsData(lS).sngZ = sngZ
        '  mtsData(lS).blnValid = True
        SetCurrentValues = ""
    End Function

    ''' <summary>
    ''' Set tracker offsets.
    ''' </summary>
    ''' <param name="lS">Sensor index.</param>
    ''' <param name="sngX">X-value</param>
    ''' <param name="sngY">Y-value</param>
    ''' <param name="sngZ">Z-value</param>
    ''' <param name="sngA">A-value</param>
    ''' <param name="sngE">E-value</param>
    ''' <param name="sngR">R-value</param>
    ''' <returns>Error text if an error occures.</returns>
    ''' <remarks></remarks>
    Public Function SetOffset(ByVal lS As Integer, ByVal sngX As Double, ByVal sngY As Double, ByVal sngZ As Double, ByVal sngA As Double, ByVal sngE As Double, ByVal sngR As Double) As String

        If lS >= mlSensorCount Then SetOffset = "Sensor index out of bound." : Exit Function
        If glTrackerMode = 2 Then
            ViWo.Send("/Tracker/SetOffset/" & TStr(lS), 0, sngY, 0, 0, 0, 0) 'only y (height) in Unity
        ElseIf glTrackerMode = 3 Then
            ' TO DO
        Else
            Output.Send("/Tracker/SetOffset/" & TStr(lS), sngX, sngY, sngZ, sngA, sngE, sngR)
        End If

        SetOffset = ""
    End Function

    ''' <summary>
    ''' Get Current Values of a sensor from tracker.
    ''' </summary>
    ''' <param name="lTO">Time Out in ms. Special cases: <li>lTo = 0: send request only, don't wait</li><li>lTo = -1: don't send request, get received values requested ages before.</li></param>
    ''' <param name="lS">Sensor index (0...first sensor)</param>
    ''' <param name="tsResult">Current values from tracker.</param>
    ''' <returns>Empty if no errors, error message otherwise.</returns>
    ''' <remarks></remarks>
    Public Function GetCurrentValues(ByVal lTO As Integer, ByVal lS As Integer, ByRef tsResult As TrackerSensor) As String
        Dim curToc, curTic As Long

        ' send request if lTo = 0 or positive
        If lTO >= 0 Or glTrackerMode = 2 Then
            mtsData(lS).blnValid = False
            If glTrackerMode = 2 Then
                ViWo.Send("/Tracker/GetCurrentValues/" & TStr(lS)) 'request current tracker values from ViWo (Unity)
            ElseIf glTrackerMode = 3 Then
                ' Motive OSC streamer just streams nonstop, so no need to request
            Else
                If lS >= mlSensorCount Then Return "Sensor index out of bound."
                mtsData(lS).blnValid = False
                Output.Send("/Tracker/GetCurrentValues/" & TStr(lS)) 'request current tracker values from FoB tracker
            End If
        End If

        ' wait for request only if lTo is positive
        If lTO <> 0 And lTO <> -1 Then
            gblnCancel = False
            QueryPerformanceCounter(curTic)
            curTic = curTic + CLng(System.Math.Abs(lTO) * gcurHPFrequency / 1000)
            While mtsData(lS).blnValid = False And (Not gblnCancel) And (curToc < curTic)
                Windows.Forms.Application.DoEvents()
                QueryPerformanceCounter(curToc)
            End While
            If gblnCancel Then Return "Canceled"
        End If

        If lTO <> 0 Then
            If mtsData(lS).blnValid = False Then Return "Time Out"
            tsResult = mtsData(lS)
            ' For optitrack, get the calibrated value
            If glTrackerMode = 3 Then
                tsResult = currentTrackerValue
            End If
        End If
        Return ""
    End Function

    ''' <summary>
    ''' Track Min or/and Max values of a sensor.
    ''' </summary>
    ''' <param name="lS">Sensor index (0...first sensor)</param>
    ''' <param name="tsMin">Minimum values for all dimensions. The .lStatus field must have a set bit for each tracker dimension (order from LSB to MSB.</param>
    ''' <param name="tsMax">Maximum values for all dimensions.</param>
    ''' <returns>Empty if no error, error message otherwise.</returns>
    ''' <remarks></remarks>
    Public Function TrackMinMaxValues(ByVal lS As Integer, ByRef tsMin As TrackerSensor, ByRef tsMax As TrackerSensor) As String

        mlMinStatus(lS) = 0
        mlMaxStatus(lS) = 0
        mlInRange(lS) = 0

        mlMinTracked(lS) = tsMin.lStatus
        mlMaxTracked(lS) = tsMin.lStatus

        ' Dim tsMinT, tsMaxT As TrackerSensor

        tsMinT = tsMin
        If (tsMin.lStatus And 1) = 0 Then tsMinT.sngX = -1234
        If (tsMin.lStatus And 2) = 0 Then tsMinT.sngY = -1234
        If (tsMin.lStatus And 4) = 0 Then tsMinT.sngZ = -1234
        If (tsMin.lStatus And 8) = 0 Then tsMinT.sngA = -1234
        If (tsMin.lStatus And 16) = 0 Then tsMinT.sngE = -1234
        If (tsMin.lStatus And 32) = 0 Then tsMinT.sngR = -1234

        tsMaxT = tsMax
        If (tsMax.lStatus And 1) = 0 Then tsMaxT.sngX = 1234
        If (tsMax.lStatus And 2) = 0 Then tsMaxT.sngY = 1234
        If (tsMax.lStatus And 4) = 0 Then tsMaxT.sngZ = 1234
        If (tsMax.lStatus And 8) = 0 Then tsMaxT.sngA = 1234
        If (tsMax.lStatus And 16) = 0 Then tsMaxT.sngE = 1234
        If (tsMax.lStatus And 32) = 0 Then tsMaxT.sngR = 1234
        If glTrackerMode = 2 Then
            ViWo.Send("/Tracker/TrackMinMaxValues/" & TStr(lS), tsMinT.sngX, tsMaxT.sngX, tsMinT.sngY, tsMaxT.sngY, tsMinT.sngZ, tsMaxT.sngZ, tsMinT.sngA, tsMaxT.sngA, tsMinT.sngE, tsMaxT.sngE, tsMinT.sngR, tsMaxT.sngR)
        ElseIf glTrackerMode = 3 Then
            ' TO DO
        Else
            Output.Send("/Tracker/TrackMinMaxValues/" & TStr(lS), tsMinT.sngX, tsMaxT.sngX, tsMinT.sngY, tsMaxT.sngY, tsMinT.sngZ, tsMaxT.sngZ, tsMinT.sngA, tsMaxT.sngA, tsMinT.sngE, tsMaxT.sngE, tsMinT.sngR, tsMaxT.sngR)
        End If
        TrackMinMaxValues = ""
    End Function

    ''' <summary>
    ''' Stops tracking the min/max values by tracker.
    ''' </summary>
    ''' <param name="lS">Sensor index (0...first sensor)</param>
    ''' <returns>Empty string if no error ocured.</returns>
    ''' <remarks></remarks>
    Public Function DontTrackMinMaxValues(ByVal lS As Integer) As String

        mlMinStatus(lS) = 0
        mlMaxStatus(lS) = 0
        mlInRange(lS) = 0
        mlMinTracked(lS) = 0
        mlMaxTracked(lS) = 0
        If glTrackerMode = 2 Then
            ViWo.Send("/Tracker/DontTrackMinMaxValues/" & TStr(lS))
        ElseIf glTrackerMode = 3 Then
            ' TO DO
        Else
            Output.Send("/Tracker/DontTrackMinMaxValues/" & TStr(lS))
        End If
        DontTrackMinMaxValues = ""
    End Function

    Public Function ResetMinMaxValues(ByVal lS As Integer) As String

        mlMinStatus(lS) = 0
        mlMaxStatus(lS) = 0
        mlInRange(lS) = 63

        Return ""

    End Function

    ''' <summary>
    ''' Check if a sensor exceeded the minimum limit.
    ''' </summary>
    ''' <param name="lS">Sensor index (0...first sensor)</param>
    ''' <param name="lStatus"></param>
    ''' <returns></returns>
    ''' <remarks>lStatus is a mask to check the requested dimension. Set to 63 to check all dimensions</remarks>
    Public Function CheckTrackedMinValue(ByVal lS As Integer, ByVal lStatus As Integer) As Integer
        Return mlMinStatus(lS) And mlMinTracked(lS) And lStatus
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="lS">Sensor index (0...first sensor)</param>
    ''' <param name="lStatus"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function CheckTrackedMaxValue(ByVal lS As Integer, ByVal lStatus As Integer) As Integer
        Return mlMaxStatus(lS) And mlMaxTracked(lS) And lStatus
    End Function

    ''' <summary>
    ''' Check if the sensor is within range now
    ''' </summary>
    ''' <param name="lS">Sensor index (0...first sensor)</param>
    ''' <param name="lStatus"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function CheckTrackedInRange(ByVal lS As Integer, ByVal lStatus As Integer) As Integer
        Return mlInRange(lS) And lStatus
    End Function

    Public Function HandleResponse(ByVal szCmd As String, ByVal varArgs() As Object) As String ' OSC messages seem to be received here, even though I cannot find where this function is used
        Dim szErr, szFunc, szX As String
        Dim lX As Integer
        szFunc = ""
        szX = ""

        If gblnTrackerLog Then
            Dim csvX As New CSVParser
            If Not IsNothing(varArgs) Then
                For lX = 0 To varArgs.Length - 1
                    szX = szX & csvX.QuoteCell(varArgs(lX).ToString) & Chr(glCSVDelimiter)
                Next
            End If
            lX = FreeFile()
            On Error Resume Next
            FileOpen(lX, STIM.WorkDir & "\trackerlog.csv", OpenMode.Append)
            'Debug.Print #lX, csvX.QuoteCell(Date) + Chr(glCSVDelimiter) + _
            ''csvX.QuoteCell(Time) + Chr(glCSVDelimiter) + _
            ''szCmd + Chr(glCSVDelimiter) + _
            ''szX
            FileClose(lX)
            csvX = Nothing
            On Error GoTo 0
        End If

        OSC.SeparateCommand(szCmd, szFunc)

        Select Case szFunc
            Case "InitFoB"
                mlSensorRequest = 0
                'frmMain.SetStatus("Tracker: InitFoB received")
            Case "GetFoBStatus"
                'frmMain.SetStatus("Tracker: Status received")
                If varArgs.Length <> 14 Then Return "GetFoBStatus: Invalid number of arguments."
                For lX = 0 To UBound(mtsData)
                    mtsData(lX).lStatus = CInt(varArgs(lX))
                Next
                mlSensorRequest = 0
            Case "GetCurrentValues"
                If IsNumeric(szCmd) Then
                    If glTrackerMode = 3 Then
                        firstFrameReceived = True
                        If CInt(varArgs(1)) = 1 Then
                            ' Rigid Body is being tracked
                            mtsData(0).blnValid = True
                            mtsData(0).nCameras = CInt(varArgs(12))
                            mtsData(0).visible = True
                            mtsData(0).sngX = Val(varArgs(2))
                            mtsData(0).sngY = -Val(varArgs(3)) ' Y axis seems to be inverted
                            mtsData(0).sngZ = Val(varArgs(4))
                            mtsData(0).sngA = -Val(varArgs(7)) ' perpendicular ground plane -> yaw=-roll
                            mtsData(0).sngE = -Val(varArgs(5)) ' perpendicular ground plane -> pitch=-yaw
                            mtsData(0).sngR = Val(varArgs(6)) ' perpendicular ground plane -> roll=pitch
                            'mtsData(0).sngQX = Val(varArgs(8))
                            'mtsData(0).sngQY = Val(varArgs(9))
                            'mtsData(0).sngQZ = Val(varArgs(10))
                            'mtsData(0).sngQW = Val(varArgs(11))
                            ' Calculating Euler angles empirically
                            'Dim CurrentAbsoluteRotation As Double(,) = QuaternionToRotM(mtsData(0).sngQX, mtsData(0).sngQY, mtsData(0).sngQZ, mtsData(0).sngQW)
                            'Dim eul As Double() = RotMToEuler(CurrentAbsoluteRotation)
                            'mtsData(0).sngA = eul(0)
                            'mtsData(0).sngE = eul(1)
                            'mtsData(0).sngR = eul(2)
                            lostTrackingCount = 0
                        Else
                            lostTrackingCount += 1
                            If lostTrackingCount > 100 Then
                                ' Tracking lost. Assign wrong values to indicate so
                                mtsData(0).sngX = 999
                                mtsData(0).sngY = 999
                                mtsData(0).sngZ = 999
                                mtsData(0).sngA = 999
                                mtsData(0).sngE = 999
                                mtsData(0).sngR = 999
                                mtsData(0).blnValid = True
                                mtsData(0).visible = False
                                mtsData(0).nCameras = CInt(varArgs(12))

                                lostTrackingCount = 0
                            End If
                        End If

                        currentTrackerValue.blnValid = mtsData(0).blnValid
                        currentTrackerValue.visible = mtsData(0).visible
                        currentTrackerValue.nCameras = mtsData(0).nCameras

                        ' Calibrate
                        If glTrackerMode = 3 Then
                            If calibrated Then
                                currentTrackerValue.sngX = mtsData(0).sngX - calibrationValue.sngX
                                currentTrackerValue.sngY = mtsData(0).sngY - calibrationValue.sngY
                                currentTrackerValue.sngZ = mtsData(0).sngZ - calibrationValue.sngZ
                                'Dim CurrentAbsoluteRotation As Double(,) = QuaternionToRotM(mtsData(0).sngQX, mtsData(0).sngQY, mtsData(0).sngQZ, mtsData(0).sngQW)
                                Dim CurrentAbsoluteRotation As Double(,) = EulerToRotM(mtsData(0).sngA, mtsData(0).sngE, mtsData(0).sngR)
                                Dim CurrentRelativeRotation As Double(,) = MatrixMult(RotM, CurrentAbsoluteRotation, 3, 3, 3, 3)
                                Dim eul As Double() = RotMToEuler(CurrentRelativeRotation)
                                currentTrackerValue.sngA = eul(2) 'eul(0)
                                currentTrackerValue.sngE = eul(1)
                                currentTrackerValue.sngR = eul(0) 'eul(2)
                            Else
                                currentTrackerValue = mtsData(0)
                            End If
                        End If

                        ' If tracking min/max values, provide them
                        If mlMinTracked IsNot Nothing Then
                            If mlMinTracked(0) > 0 Then
                                If currentTrackerValue.sngX < tsMinT.sngX And ((mlMinTracked(0) And 1) <> 0) Then mlMinStatus(0) = mlMinStatus(0) Or 1 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 0)) - CInt(2 ^ 0) : mlMaxStatus(0) = CShort(mlMaxStatus(0) Or CInt(2 ^ 0)) - CInt(2 ^ 0)
                                If currentTrackerValue.sngX > tsMaxT.sngX And ((mlMaxTracked(0) And 1) <> 0) Then mlMaxStatus(0) = mlMaxStatus(0) Or 1 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 0)) - CInt(2 ^ 0) : mlMinStatus(0) = CShort(mlMinStatus(0) Or CInt(2 ^ 0)) - CInt(2 ^ 0)
                                If ((mlMinStatus(0) Or mlMaxStatus(0)) And 1) = 0 Then
                                    mlInRange(0) = mlInRange(0) Or 1
                                End If
                                If currentTrackerValue.sngY < tsMinT.sngY And ((mlMinTracked(0) And 2) <> 0) Then mlMinStatus(0) = mlMinStatus(0) Or 2 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 1)) - CInt(2 ^ 1) : mlMaxStatus(0) = CShort(mlMaxStatus(0) Or CInt(2 ^ 1)) - CInt(2 ^ 1)
                                If currentTrackerValue.sngY > tsMaxT.sngY And ((mlMaxTracked(0) And 2) <> 0) Then mlMaxStatus(0) = mlMaxStatus(0) Or 2 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 1)) - CInt(2 ^ 1) : mlMinStatus(0) = CShort(mlMinStatus(0) Or CInt(2 ^ 1)) - CInt(2 ^ 1)
                                If ((mlMinStatus(0) Or mlMaxStatus(0)) And 2) = 0 Then
                                    mlInRange(0) = mlInRange(0) Or 2
                                End If
                                If currentTrackerValue.sngZ < tsMinT.sngZ And ((mlMinTracked(0) And 4) <> 0) Then mlMinStatus(0) = mlMinStatus(0) Or 4 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 2)) - CInt(2 ^ 2) : mlMaxStatus(0) = CShort(mlMaxStatus(0) Or CInt(2 ^ 2)) - CInt(2 ^ 2)
                                If currentTrackerValue.sngZ > tsMaxT.sngZ And ((mlMaxTracked(0) And 4) <> 0) Then mlMaxStatus(0) = mlMaxStatus(0) Or 4 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 2)) - CInt(2 ^ 2) : mlMinStatus(0) = CShort(mlMinStatus(0) Or CInt(2 ^ 2)) - CInt(2 ^ 2)
                                If ((mlMinStatus(0) Or mlMaxStatus(0)) And 4) = 0 Then
                                    mlInRange(0) = mlInRange(0) Or 4
                                End If
                                If currentTrackerValue.sngA < tsMinT.sngA And ((mlMinTracked(0) And 8) <> 0) Then mlMinStatus(0) = mlMinStatus(0) Or 8 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 3)) - CInt(2 ^ 3) : mlMaxStatus(0) = CShort(mlMaxStatus(0) Or CInt(2 ^ 3)) - CInt(2 ^ 3)
                                If currentTrackerValue.sngA > tsMaxT.sngA And ((mlMaxTracked(0) And 8) <> 0) Then mlMaxStatus(0) = mlMaxStatus(0) Or 8 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 3)) - CInt(2 ^ 3) : mlMinStatus(0) = CShort(mlMinStatus(0) Or CInt(2 ^ 3)) - CInt(2 ^ 3)
                                If ((mlMinStatus(0) Or mlMaxStatus(0)) And 8) = 0 Then
                                    mlInRange(0) = mlInRange(0) Or 8
                                End If
                                If currentTrackerValue.sngE < tsMinT.sngE And ((mlMinTracked(0) And 16) <> 0) Then mlMinStatus(0) = mlMinStatus(0) Or 16 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 4)) - CInt(2 ^ 4) : mlMaxStatus(0) = CShort(mlMaxStatus(0) Or CInt(2 ^ 4)) - CInt(2 ^ 4)
                                If currentTrackerValue.sngE > tsMaxT.sngE And ((mlMaxTracked(0) And 16) <> 0) Then mlMaxStatus(0) = mlMaxStatus(0) Or 16 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 4)) - CInt(2 ^ 4) : mlMinStatus(0) = CShort(mlMinStatus(0) Or CInt(2 ^ 4)) - CInt(2 ^ 4)
                                If ((mlMinStatus(0) Or mlMaxStatus(0)) And 16) = 0 Then
                                    mlInRange(0) = mlInRange(0) Or 16
                                End If
                                If currentTrackerValue.sngR < tsMinT.sngR And ((mlMinTracked(0) And 32) <> 0) Then mlMinStatus(0) = mlMinStatus(0) Or 32 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 5)) - CInt(2 ^ 5) : mlMaxStatus(0) = CShort(mlMaxStatus(0) Or CInt(2 ^ 5)) - CInt(2 ^ 5)
                                If currentTrackerValue.sngR > tsMaxT.sngR And ((mlMaxTracked(0) And 32) <> 0) Then mlMaxStatus(0) = mlMaxStatus(0) Or 32 : mlInRange(0) = CShort(mlInRange(0) Or CInt(2 ^ 5)) - CInt(2 ^ 5) : mlMinStatus(0) = CShort(mlMinStatus(0) Or CInt(2 ^ 5)) - CInt(2 ^ 5)
                                If ((mlMinStatus(0) Or mlMaxStatus(0)) And 32) = 0 Then
                                    mlInRange(0) = mlInRange(0) Or 32
                                End If
                            End If
                        End If

                    Else
                        lX = CInt(Val(szCmd))
                        If mtsData(lX).blnValid Then Return "GetCurrentValues: Sensor not requested."
                        If UBound(varArgs) <> 5 Then Return "GetCurrentValues: Invalid number of argunments."

                        mtsData(lX).sngX = Val(varArgs(0))
                        mtsData(lX).sngY = Val(varArgs(1))
                        mtsData(lX).sngZ = Val(varArgs(2))
                        mtsData(lX).sngA = Val(varArgs(3))
                        mtsData(lX).sngE = Val(varArgs(4))
                        mtsData(lX).sngR = Val(varArgs(5))
                        mtsData(lX).blnValid = True
                    End If
                End If
            Case "TrackMinMaxValues"
                If IsNumeric(szCmd) Then
                    lX = CInt(Val(szCmd))
                    If lX > UBound(mlMinTracked) Then Return "TrackMinMaxValues: Invalid sensor index"
                    If UBound(varArgs) <> 0 Then Return "TrackMinMaxValues: Invalid number of argunments."
                    If varArgs(0).ToString = "Xmin" And ((mlMinTracked(lX) And 1) <> 0) Then mlMinStatus(lX) = mlMinStatus(lX) Or 1 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 0)) - CInt(2 ^ 0) : mlMaxStatus(lX) = CShort(mlMaxStatus(lX) Or CInt(2 ^ 0)) - CInt(2 ^ 0)
                    If varArgs(0).ToString = "Xmax" And ((mlMaxTracked(lX) And 1) <> 0) Then mlMaxStatus(lX) = mlMaxStatus(lX) Or 1 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 0)) - CInt(2 ^ 0) : mlMinStatus(lX) = CShort(mlMinStatus(lX) Or CInt(2 ^ 0)) - CInt(2 ^ 0)
                    If varArgs(0).ToString = "Xinrange" And ((mlMinTracked(lX) And 1) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 1
                    If varArgs(0).ToString = "Xinrange" And ((mlMaxTracked(lX) And 1) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 1
                    If varArgs(0).ToString = "Ymin" And ((mlMinTracked(lX) And 2) <> 0) Then mlMinStatus(lX) = mlMinStatus(lX) Or 2 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 1)) - CInt(2 ^ 1) : mlMaxStatus(lX) = CShort(mlMaxStatus(lX) Or CInt(2 ^ 1)) - CInt(2 ^ 1)
                    If varArgs(0).ToString = "Ymax" And ((mlMaxTracked(lX) And 2) <> 0) Then mlMaxStatus(lX) = mlMaxStatus(lX) Or 2 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 1)) - CInt(2 ^ 1) : mlMinStatus(lX) = CShort(mlMinStatus(lX) Or CInt(2 ^ 1)) - CInt(2 ^ 1)
                    If varArgs(0).ToString = "Yinrange" And ((mlMinTracked(lX) And 2) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 2
                    If varArgs(0).ToString = "Yinrange" And ((mlMaxTracked(lX) And 2) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 2
                    If varArgs(0).ToString = "Zmin" And ((mlMinTracked(lX) And 4) <> 0) Then mlMinStatus(lX) = mlMinStatus(lX) Or 4 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 2)) - CInt(2 ^ 2) : mlMaxStatus(lX) = CShort(mlMaxStatus(lX) Or CInt(2 ^ 2)) - CInt(2 ^ 2)
                    If varArgs(0).ToString = "Zmax" And ((mlMaxTracked(lX) And 4) <> 0) Then mlMaxStatus(lX) = mlMaxStatus(lX) Or 4 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 2)) - CInt(2 ^ 2) : mlMinStatus(lX) = CShort(mlMinStatus(lX) Or CInt(2 ^ 2)) - CInt(2 ^ 2)
                    If varArgs(0).ToString = "Zinrange" And ((mlMinTracked(lX) And 4) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 4
                    If varArgs(0).ToString = "Zinrange" And ((mlMaxTracked(lX) And 4) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 4
                    If varArgs(0).ToString = "Amin" And ((mlMinTracked(lX) And 8) <> 0) Then mlMinStatus(lX) = mlMinStatus(lX) Or 8 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 3)) - CInt(2 ^ 3) : mlMaxStatus(lX) = CShort(mlMaxStatus(lX) Or CInt(2 ^ 3)) - CInt(2 ^ 3)
                    If varArgs(0).ToString = "Amax" And ((mlMaxTracked(lX) And 8) <> 0) Then mlMaxStatus(lX) = mlMaxStatus(lX) Or 8 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 3)) - CInt(2 ^ 3) : mlMinStatus(lX) = CShort(mlMinStatus(lX) Or CInt(2 ^ 3)) - CInt(2 ^ 3)
                    If varArgs(0).ToString = "Ainrange" And ((mlMinTracked(lX) And 8) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 8
                    If varArgs(0).ToString = "Ainrange" And ((mlMaxTracked(lX) And 8) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 8
                    If varArgs(0).ToString = "Emin" And ((mlMinTracked(lX) And 16) <> 0) Then mlMinStatus(lX) = mlMinStatus(lX) Or 16 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 4)) - CInt(2 ^ 4) : mlMaxStatus(lX) = CShort(mlMaxStatus(lX) Or CInt(2 ^ 4)) - CInt(2 ^ 4)
                    If varArgs(0).ToString = "Emax" And ((mlMaxTracked(lX) And 16) <> 0) Then mlMaxStatus(lX) = mlMaxStatus(lX) Or 16 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 4)) - CInt(2 ^ 4) : mlMinStatus(lX) = CShort(mlMinStatus(lX) Or CInt(2 ^ 4)) - CInt(2 ^ 4)
                    If varArgs(0).ToString = "Einrange" And ((mlMinTracked(lX) And 16) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 16
                    If varArgs(0).ToString = "Einrange" And ((mlMaxTracked(lX) And 16) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 16
                    If varArgs(0).ToString = "Rmin" And ((mlMinTracked(lX) And 32) <> 0) Then mlMinStatus(lX) = mlMinStatus(lX) Or 32 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 5)) - CInt(2 ^ 5) : mlMaxStatus(lX) = CShort(mlMaxStatus(lX) Or CInt(2 ^ 5)) - CInt(2 ^ 5)
                    If varArgs(0).ToString = "Rmax" And ((mlMaxTracked(lX) And 32) <> 0) Then mlMaxStatus(lX) = mlMaxStatus(lX) Or 32 : mlInRange(lX) = CShort(mlInRange(lX) Or CInt(2 ^ 5)) - CInt(2 ^ 5) : mlMinStatus(lX) = CShort(mlMinStatus(lX) Or CInt(2 ^ 5)) - CInt(2 ^ 5)
                    If varArgs(0).ToString = "Rinrange" And ((mlMinTracked(lX) And 32) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 32
                    If varArgs(0).ToString = "Rinrange" And ((mlMaxTracked(lX) And 32) <> 0) Then mlInRange(lX) = mlInRange(lX) Or 32
                End If
            Case "GetResponse"
                glTrackerTriggerResponse = CInt(Val(szCmd))

            Case Else
                Return "Tracker: Unknown response."
        End Select
        Return ""
    End Function

    Public Sub SetRangeData(ByVal lS As Integer, ByVal szDim As String)
        ' used as interface between frmSettings and frmSettingsTrackRange
        mlSensor = lS
        mszDim = szDim
    End Sub

    Public Sub GetRangeData(ByRef lS As Integer, ByRef szDim As String)
        ' used as interface between frmSettings and frmSettingsTrackRange
        lS = mlSensor
        szDim = mszDim
    End Sub

    Public Function CalibrateOptitrack() As String

        calibrated = False
        If firstFrameReceived Then
            calibrationValue = mtsData(0)
            'RotM = QuaternionToRotM(mtsData(0).sngQX, mtsData(0).sngQY, mtsData(0).sngQZ, mtsData(0).sngQW)
            RotM = EulerToRotM(mtsData(0).sngA, mtsData(0).sngE, mtsData(0).sngR)
            RotM = MatrixTranspose(RotM, 3, 3)
            calibrated = True
            MsgBox("Optitrack successfully calibrated to current position!")
        Else
            MsgBox("Calibration failed because no data has been received from Optitrack yet. Please try again in a few seconds or check if the cameras are disconnected.")
        End If

        Return ""

    End Function

    Public Function IsTracking() As Boolean
        If glTrackerMode = 3 Then
            Return firstFrameReceived
        Else
            Return True
        End If
    End Function

    Private Function EulerToRotM(yaw As Double, pitch As Double, roll As Double) As Double(,)

        Dim cy As Double = Math.Cos(yaw * Math.PI / 180)
        Dim cp As Double = Math.Cos(pitch * Math.PI / 180)
        Dim cr As Double = Math.Cos(roll * Math.PI / 180)
        Dim sy As Double = Math.Sin(yaw * Math.PI / 180)
        Dim sp As Double = Math.Sin(pitch * Math.PI / 180)
        Dim sr As Double = Math.Sin(roll * Math.PI / 180)
        Dim R(2, 2) As Double

        ' These are XYZ convention, which Motive uses
        R(0, 0) = cp * cr
        R(0, 1) = -cp * sr
        R(0, 2) = sp
        R(1, 0) = cy * sr + cr * sy * sp
        R(1, 1) = cy * cr - sy * sp * sr
        R(1, 2) = -cp * sy
        R(2, 0) = sy * sr - cy * cr * sp
        R(2, 1) = cr * sy + cy * sp * sr
        R(2, 2) = cy * cp

        ' These are ZYX convention. Not using them.
        'R(0, 0) = cp * cy
        'R(0, 1) = sr * sp * cy - cr * sy
        'R(0, 2) = cr * sp * cy + sr * sy
        'R(1, 0) = cp * sy
        'R(1, 1) = sr * sp * sy + cr * cy
        'R(1, 2) = cr * sp * sy - sr * cy
        'R(2, 0) = -sp
        'R(2, 1) = sr * cp
        'R(2, 2) = cr * cp

        Return R

    End Function

    Private Function RotMToEuler(R As Double(,)) As Double() ' using ZYX convention here

        Dim eul(2) As Double

        ' Find special cases of rotation matrix values that correspond to Euler angle singularities.  
        Dim sy As Double = Math.Sqrt(R(0, 0) * R(0, 0) + R(1, 0) * R(1, 0))
        Dim singular As Boolean = sy < 0.001

        ' Calculate Euler angles
        eul = {Math.Atan2(R(2, 1), R(2, 2)), Math.Atan2(-R(2, 0), sy), Math.Atan2(R(1, 0), R(0, 0))}

        ' Singular matrices need special treatment
        If singular Then
            eul = {Math.Atan2(-R(1, 2), R(1, 1)), Math.Atan2(-R(2, 0), sy), 0}
        End If

        ' Swap the X And Z columns
        Dim temp As Double = eul(2)
        eul(2) = eul(0)
        eul(0) = temp

        ' To degrees
        eul(0) = eul(0) * 180 / Math.PI
        eul(1) = eul(1) * 180 / Math.PI
        eul(2) = eul(2) * 180 / Math.PI

        Return eul

    End Function

    Private Function QuaternionToRotM(x As Double, y As Double, z As Double, w As Double) As Double(,)

        Dim R(2, 2) As Double

        ' This code is different than the Matlab script, but it works for the Motive output

        R(0, 0) = 1 - 2 * (z * z + y * y)
        R(0, 1) = 2 * (x * z - w * y)
        R(0, 2) = 2 * (x * y + w * z)
        R(1, 0) = 2 * (x * z + w * y)
        R(1, 1) = 1 - 2 * (x * x + y * y)
        R(1, 2) = 2 * (z * y - w * x)
        R(2, 0) = 2 * (x * y - w * z)
        R(2, 1) = 2 * (z * y + w * x)
        R(2, 2) = 1 - 2 * (x * x + z * z)

        Return R

    End Function

    Private Function MatrixMult(A As Double(,), B As Double(,), nrows1 As Integer, ncols1 As Integer, nrows2 As Integer, ncols2 As Integer) As Double(,)

        Dim C(nrows1 - 1, ncols2 - 1) As Double

        For i As Integer = 0 To nrows1 - 1
            For j As Integer = 0 To ncols2 - 1
                C(i, j) = 0
                For k As Integer = 0 To nrows2 - 1
                    C(i, j) = C(i, j) + (A(i, k) * B(k, j))
                Next
            Next
        Next

        Return C

    End Function

    Private Function MatrixTranspose(A As Double(,), nrows As Integer, ncols As Integer) As Double(,)

        Dim At(ncols - 1, nrows - 1) As Double

        For i As Integer = 0 To nrows - 1
            For j As Integer = 0 To ncols - 1
                At(j, i) = A(i, j)
            Next
        Next

        Return At

    End Function

End Module