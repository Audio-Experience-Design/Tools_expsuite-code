Option Strict On
Option Explicit On
#Region "ExpSuite License"
'ExpSuite - software framework for applications to perform experiments (related but not limited to psychoacoustics).
'Copyright (C) 2003-2021 Acoustics Research Institute - Austrian Academy of Sciences; Piotr Majdak and Michael Mihocic
'Licensed under the EUPL, Version 1.2 or � as soon they will be approved by the European Commission - subsequent versions of the EUPL (the "Licence")
'You may not use this work except in compliance with the Licence. 
'You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/software/page/eupl
'Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
'See the Licence for the specific language governing  permissions and limitations under the Licence. 
#End Region
Friend Class frmIR
    Inherits System.Windows.Forms.Form

    'Private mlLeft, mlTop As Integer

    Function MatlabCmd(ByRef szCmd As String) As String

        MatlabCmd = STIM.Matlab(szCmd)

    End Function

    Public Sub SetBusy()

        ' general controls
        For Each ctrX As Windows.Forms.Control In Me.Controls
            If TypeOf ctrX Is System.Windows.Forms.Button Then ctrX.Enabled = False
            If TypeOf ctrX Is System.Windows.Forms.ToolStrip Then ctrX.Enabled = False
            If TypeOf ctrX Is System.Windows.Forms.TextBox Then TextBoxState(DirectCast(ctrX, TextBox), False)
            If TypeOf ctrX Is System.Windows.Forms.CheckBox Then ctrX.Enabled = False
            If TypeOf ctrX Is System.Windows.Forms.GroupBox Then ctrX.Enabled = False
        Next ctrX

    End Sub

    Public Sub SetReady()

        ' general controls
        For Each ctrX As System.Windows.Forms.Control In Me.Controls
            If TypeOf ctrX Is System.Windows.Forms.Button Then ctrX.Enabled = True
            If TypeOf ctrX Is System.Windows.Forms.ToolStrip Then ctrX.Enabled = True
            If TypeOf ctrX Is System.Windows.Forms.TextBox Then TextBoxState(DirectCast(ctrX, TextBox), True)
            If TypeOf ctrX Is System.Windows.Forms.CheckBox Then ctrX.Enabled = True
            If TypeOf ctrX Is System.Windows.Forms.GroupBox Then ctrX.Enabled = True
        Next ctrX

    End Sub

    Public Sub SetStatus(ByRef szStatus As String)
        If lstStatus.Items.Count > 100 Then lstStatus.Items.RemoveAt((0))
        lstStatus.Items.Add((szStatus))
        lstStatus.SelectedIndex = lstStatus.Items.Count - 1
        ToolTip1.SetToolTip(lstStatus, szStatus)
    End Sub


    Private Sub cmdCalchC_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdCalchC.Click

        SetBusy()
        SetStatus("Calculating *C...")
        Select Case glExpType
            Case 0 ' MLS
                MLStoIR(False)
                'MLStoIR((ItemList.SelectedItemFirst + 1), (ItemList.SelectedItemLast + 1))
            Case 1, 3 ' Sweep, hrtf
                ToolboxIR.SweeptoIR(False)
                'ToolboxIR.SweeptoIR((ItemList.SelectedItemFirst + 1), (ItemList.SelectedItemLast + 1), False)
        End Select
        SetStatus("*C ready.")

        SetReady()

    End Sub

    Private Sub cmdClearOldResults_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdClearOldResults.Click
        Dim szX As String

        SetBusy()

        szX = MatlabCmd("clear *X *C stimVec")
        If Len(szX) > 0 Then MsgBox(szX, MsgBoxStyle.Critical, "Error")
        SetStatus("*X, *C and stimVec deleted")

        SetReady()
    End Sub

    Private Sub cmdCopyLatency_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdCopyLatency.Click

        SetBusy()
        frmLatency.ShowDialog()
        SetReady()

    End Sub

    Private Sub cmdFirstCheck_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdFirstCheck.Click
        Dim szX As String
        Dim szErr As String = ""
        Dim lCols, lRows, lX As Integer
        Dim szLevel As String
        Dim dblFirstCheck(,) As Double

        SetBusy()
        SetStatus("First Check of SOFA object in progress...")

        szLevel = txtFirstCheck.Text.ToString
        'szLevel = InputBox("Set Level for First Check of h-M." & vbCrLf & vbCrLf & "All measurements below this value will be detected and can be repeated:", "First Check of h-M", txtFirstCheck.Text.ToString)
        'If Len(szLevel) = 0 Then szErr = "Cancelled: No value entered for First check of h-M!" : GoTo SubError
        If IsNumeric(szLevel) = False Then szErr = "Level Value must be numeric!" : GoTo SubError

        MatlabCmd("clear fc_*;")
        szX = MatlabCmd("fc_idx=find(20*log10(squeeze(sum(abs(Obj.Data.IR(:," & TStr(numFirstCheckChannel.Value) & ",:)),3)))<" & szLevel & ");")
        If Len(szX) <> 0 Then szErr = "First Check of SOFA object: " & szX : GoTo SubError

        'szX = MatlabCmd("fc_idx_old=find(20*log10(squeeze(sum(abs(h-M_old(:,:,1)),1)))<" & szLevel & ");")
        'If Len(szX) <> 0 Then szErr = "First Check of h-M: " & szX : GoTo SubError

        szX = MatlabCmd("fc_jj=1;for fc_ii=1:length(fc_idx); if fc_ii==1; fc_idx2(fc_jj)=meta.itemidx(fc_idx(fc_ii)); fc_jj=fc_jj+1; elseif fc_ii>1; if meta.itemidx(fc_idx(fc_ii)) ~= meta.itemidx(fc_idx(fc_ii-1)); fc_idx2(fc_jj)=meta.itemidx(fc_idx(fc_ii));  fc_jj=fc_jj+1; end ; end;end;")
        If Len(szX) <> 0 Then szErr = "First Check of SOFA object: " & szX : GoTo SubError
        szX = MatlabCmd("if exist('fc_idx2');fc_idx=fc_idx2;end;")
        If Len(szX) > 0 Then MsgBox(szX, MsgBoxStyle.Critical, "Error") : GoTo SubError

        szX = STIM.MatlabGetMatrixSize("fc_idx", lRows, lCols)
        'Debug.Print(szX)
        If Len(szX) <> 0 Then szErr = "First Check of SOFA object: " & szX : GoTo SubError
        If lRows < 1 Or lCols < 1 Then
            SetStatus("First Check of SOFA object finished successfully: No errors found.")
            MsgBox("First Check of SOFA object finished successfully: No errors found.", MsgBoxStyle.OkOnly, "First Check of SOFA object")
            GoTo SubEnd
        End If

        ReDim dblFirstCheck(lRows - 1, lCols - 1)
        szX = STIM.MatlabGetRealMatrix2("fc_idx", dblFirstCheck)
        If Len(szX) <> 0 Then szErr = "First Check of SOFA object: " & szX : GoTo SubError
        szX = MatlabCmd("clear fc_*;")
        If Len(szX) <> 0 Then szErr = "First Check of SOFA object: " & szX : GoTo SubError
        SetStatus("First Check of SOFA object finished: " & TStr(lCols) & " bad measurement(s) found!")
        szErr = "The level of the following " & TStr(lCols) & " measurement(s) is below " & szLevel & " dB in record channel " & TStr(numFirstCheckChannel.Value) & ":" & vbCrLf & vbCrLf & "Index" & vbTab & "Azimuth"
        For lX = 0 To UBound(dblFirstCheck, 2)
            szErr = szErr & vbCrLf & dblFirstCheck(0, lX) & vbTab & ItemList.Item(CInt(dblFirstCheck(0, lX)) - 1, 1)
        Next

        If Len(szX) <> 0 Then szErr = "First Check of SOFA object: " & szX : GoTo SubError

        MsgBox(szErr, MsgBoxStyle.OkOnly Or MsgBoxStyle.Critical, "First Check of SOFA object")
        'szErr = ""

        GoTo SubEnd

SubError:
        SetStatus("First Check of SOFA object: Error!")
        MsgBox(szErr, MsgBoxStyle.Critical, "First Check of SOFA object")

SubEnd:
        SetReady()

    End Sub

    Private Sub cmdRecalcToLIN_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdRecalcToLIN.Click
        Dim frmX As New frmResult
        Dim lY, lX, lRow As Integer
        Dim szX, szErr, szY As String
        Dim dblArr(,) As Double
        'Dim lRowBeg, lRowEnd As Integer

        SetBusy()
        SetStatus("Calculating *LIN...")
        'lRowBeg = ItemList.SelectedItemFirst + 1
        'lRowEnd = ItemList.SelectedItemLast + 1
        Dim lArr As Integer() = Nothing
        szErr = GetRangeArray(lArr,False)
        If szErr <> "" Then GoTo SubError

        ' get size of hC{}
        szX = STIM.MatlabGetMatrixSize("hC", lY, lX)
        If Len(szX) > 0 Then
            szErr = "Get Size of hC: " & szX
            GoTo SubError
        End If
        If lX < (lArr(0)+1) Then 'lX or lY ????
            szErr = "IR matrix not defined for items beginning from " & TStr((lArr(0)+1))
            GoTo SubError
        End If
        If lX < lArr(lArr.Length-1) Then 'lRowEnd = lX
            szErr = "IR matrix not defined for items ending at " & TStr(lArr(lArr.Length-1)+1)
        GoTo SubError
        End If
        
        szErr = ""

        ' set ibegQ
        szX = "ibegQ=" & TStr(lArr(0)+1) & ";"
        szY = MatlabCmd(szX)
        If Len(szY) <> 0 Then szErr = szErr & szX & vbCrLf & szY : GoTo SubError
        ' set iendQ
        szX = "iendQ=" & TStr(lArr(lArr.Length-1)+1) & ";"
        szY = MatlabCmd(szX)
        If Len(szY) <> 0 Then szErr = szErr & szX & vbCrLf & szY : GoTo SubError
        ' start CalcLinearMatrix
        szX = "AA_CalcLinearMatrix;"
        szY = MatlabCmd(szX)
        If Len(szY) <> 0 Then szErr = szErr & szX & vbCrLf & szY : GoTo SubError
        SetStatus("Linear data (*LIN) created")

        If glExpType = 3 Then
            szX = "posLIN=AA_CalcPositions(posLIN," & gconstExp(10).varValue & ");"
            szY = MatlabCmd(szX)
            If Len(szY) <> 0 Then szErr = szErr & szX & vbCrLf & szY : GoTo SubError
            SetStatus("Azimuth angles decoded (turntable -> HRTF)")
        End If

        ' get size of itemnrLIN
        szX = STIM.MatlabGetMatrixSize("itemnrLIN", lY, lX)
        If Len(szX) > 0 Then szErr = "Get Size of itemnrLIN: " & szX : GoTo SubError
        If lY < 1 And lX < 1 Then szErr = "itemnrLIN is empty." : GoTo SubError
        ReDim dblArr(lY - 1, lX - 1)
        szX = STIM.MatlabGetRealMatrix2("itemnrLIN", dblArr)
        If Len(szX) > 0 Then szErr = "Get itemnrLIN: " & szX : GoTo SubError
        For lRow = lArr(0) To lArr(lArr.Length-1)
            ItemList.Item(lRow, "STATUS") = "Error"
        Next
        For lRow = 0 To UBound(dblArr, 1)
            lY = CInt(dblArr(lRow, 0))
            ItemList.Item(lY - 1, "STATUS") = "Linear"
        Next

        SetStatus("*LIN calculated.")
        SetReady()
        Exit Sub

SubError:
        MsgBox(szErr, MsgBoxStyle.Critical, "Calc linear: Error")
        SetReady()
        Exit Sub

    End Sub

    'Private Sub cmdReshapeToM_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdReshapeToM.Click

    '    Dim szErr As String = h-MtoSOFA()
    '    If Len(szErr) > 0 Then MsgBox(szErr, MsgBoxStyle.Critical, "Error") : SetReady() : Exit Sub

    '    SetStatus("hLIN reshaped to structured meta data; hLIN and idxLIN deleted")
    '    SetStatus("Structured meta data created in SOFA format")

    '    SetReady()
    'End Sub




    Private Sub cmdCancel_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdCancel.Click
        gblnCancel = True
        Me.Close()
    End Sub

    Private Sub cmdOK_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdOK.Click

        cmdOK.Enabled = False
        gIRFlags.CalcResults = CBool(chkCalcResults.CheckState)
        gIRFlags.ShowLatency = CBool(chkShowLatency.CheckState)
        gIRFlags.PlotIR = CBool(chkPlotIR.CheckState)
        gIRFlags.SaveIR = CBool(chkSaveIR.CheckState)
        gIRFlags.CalcLinear = CBool(chkCalcLinear.CheckState)
        gIRFlags.SaveLinearToMAT = CBool(chkSaveLinearToMAT.CheckState)
        gIRFlags.SaveLinearToWAV = CBool(chkSaveLinearToWAV.CheckState)
        gIRFlags.KeepVar = CBool(chkKeepVar.CheckState)
        gIRFlags.KeepTemp = CBool(chkKeepTemp.CheckState)
        'gIRFlags.GaborMult = CBool(chkGaborMult.CheckState)
        gIRFlags.CreateMatrix = CBool(chkCreateMatrix.CheckState)
        'gIRFlags.PlotMatrix = CBool(chkPlotMatrix.CheckState)
        'gIRFlags.SaveWorkspace = CBool(chkSaveWorkspace.CheckState)
        gIRFlags.CopyLatency = CBool(chkCopyLatency.CheckState)
        gIRFlags.FirstCheck = CBool(chkFirstCheck.CheckState)
        gIRFlags.SaveM = CBool(chkSaveM.CheckState)
        gIRFlags.SaveSOFA = CBool(chkSaveSOFA.CheckState)
        gIRFlags.ProcessItemRange = CBool(chkProcessItemRange.CheckState)
        If chkProcessItemRange.CheckState = CheckState.Checked Then 'Check manual range parameters
            If Len(txtItemRangeMin.Text) = 0 Or Not IsNumeric(txtItemRangeMin.Text) Or _
            Len(txtItemRangeMax.Text) = 0 Or Not IsNumeric(txtItemRangeMax.Text) Then
                MsgBox("Insert valid numeric values for Item Range.", MsgBoxStyle.Exclamation, "Process item range")
                cmdOK.Enabled = True
                Exit Sub
            ElseIf CInt(Val(txtItemRangeMax.Text)) > ItemList.ItemCount Then
                MsgBox("Item Range: Index higher than item count! (Max. possible value: " & TStr(ItemList.ItemCount) & ")", MsgBoxStyle.Exclamation, "Process item range")
                cmdOK.Enabled = True
                Exit Sub
            ElseIf CInt(Val(txtItemRangeMin.Text)) > CInt(Val(txtItemRangeMax.Text)) Then
                MsgBox("Item Range: Lower range value must be lower then upper range value!", MsgBoxStyle.Exclamation, "Process item range")
                cmdOK.Enabled = True
                Exit Sub
            End If
        End If
        gblnCancel = False

        If gIRFlags.SaveSOFA Then 'Save as SOFA?
            'Start SOFA
            Dim szX As String = "AA_SOFAstart"
            szX = MatlabCmd(szX)
            If InStr(LCase(szX), "error") <> 0 Then
                MsgBox("SOFAstart cannot be found! For now you cannot save structured meta data as SOFA!" & vbCrLf & vbCrLf & "Please download the SOFA package from:" & vbCrLf & "https://sourceforge.net/projects/sofacoustics/" & _
                    vbCrLf & "and extract the files to your \MATLAB\SOFA subfolder." & vbCrLf & vbCrLf & vbCrLf & szX, MsgBoxStyle.Critical, "Load SOFA: Error when starting SOFA")
                cmdOK.Enabled = True
                Exit Sub
            End If

        End If

        If gIRFlags.PlotIR Then
            frmShowStimulus.ShowDialog()
            If Not gblnShowStimulus Then gblnCancel = True
        End If
        cmdOK.Enabled = True
        Me.Close()
    End Sub

    Private Sub cmdSaveM_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdSaveM.Click
        Dim szVer, szX, szY As String
        Dim lX As Integer
        Dim lY As Integer = 0
        Dim szListItems() As String = Nothing

        SetBusy()
        szX = "version('-release');"
        szVer = STIM.Matlab(szX)

        szX = Dir(STIM.WorkDir & "\" & STIM.ID & "_M_*.mat")
        If Len(szX) > 0 Then
            szY = "Files with following postfixes could be found until now:" & vbCrLf
        Else
            szY = "No files available until now."
        End If
        'While Len(szX) > 0
        '    lX = Len(STIM.ID & "_M_")
        '    szY = szY & vbTab & Mid(szX, lX + 1, Len(szX) - lX - Len(".mat")) & vbCrLf
        '    szX = Dir()
        'End While

        'szY = InputBox("Input the postfix of the file: (File name will be: ID_postfix.mat)" & vbCrLf & szY, "Save *M as .MAT", "test")
        'If Len(szY) = 0 Then SetReady() : Exit Sub

        While Len(szX) > 0
            lX = Len(STIM.ID & "_M_")
            szY = szY & vbTab & Mid(szX, lX + 1, Len(szX) - lX - Len(".mat")) & vbCrLf
            ReDim Preserve szListItems(lY)
            szListItems(lY) = Mid(szX, lX + 1, Len(szX) - lX - Len(".mat"))

            szX = Dir()
            lY += 1

        End While
        frmListbox.txtName.Text = "" 'clear text box of form:
        szY = frmListbox.Init("Input the postfix of the file (File Name will be: ID_postfix.mat)" & vbCrLf & vbCrLf & "Files with following postfixes are available:", "Save *M to .MAT", szListItems,"")
        If Len(szY) = 0 Then
            If frmListbox.DialogResult = Windows.Forms.DialogResult.OK Then MsgBox("*M NOT saved to .MAT!", MsgBoxStyle.Exclamation, "Save *M to .MAT")
            SetReady()
            Exit Sub
        End If


        STIM.Matlab("stimPar.WorkDir = '" & STIM.WorkDir & "';")
        STIM.Matlab("stimPar.ID = '" & STIM.ID & "';")

        SetStatus("Saving data to " & szY & ".mat file...")
        'don't save WorkDir in stimPar
        'szX = "TempWorkDir=stimPar.WorkDir;" 'temp folder name
        'szX = MatlabCmd(szX)
        'If Len(szX) <> 0 Then MsgBox(szX, MsgBoxStyle.Critical, "Save *M to MAT: Error when setting WorkDir!") : SetReady() : Exit Sub

        'szX = "stimPar.WorkDir='';" 'clear original folder name
        'szX = MatlabCmd(szX)
        'If Len(szX) <> 0 Then MsgBox(szX, MsgBoxStyle.Critical, "Save *M to MAT: Error when setting WorkDir!" & vbCrLf & "Current WorkDir may be wrong!") : SetReady() : Exit Sub
        If Val(szVer) < 14 Then
            szX = "AA_SaveMat([TempWorkDir '\' stimPar.ID '_M_" & szY & ".mat'],Obj,stimPar,meta,'');"
        Else
            szX = "AA_SaveMat([TempWorkDir '\' stimPar.ID '_M_" & szY & ".mat'],Obj,stimPar,meta,'-V6');"
        End If
        szX = MatlabCmd(szX)
        If Len(szX) <> 0 Then MsgBox(szX, MsgBoxStyle.Critical, "Save *M to MAT: Error") : SetReady() : Exit Sub
        'szX = "stimPar.WorkDir=TempWorkDir;" 'restore original folder name
        'szX = MatlabCmd(szX)
        'If Len(szX) <> 0 Then MsgBox(szX, MsgBoxStyle.Critical, "Save *M to MAT: Error when resetting WorkDir!" & vbCrLf & "Current WorkDir may be wrong!") : SetReady() : Exit Sub
        SetStatus("SOFA object and structured meta data saved to " & STIM.WorkDir & "\" & STIM.ID & "_M_" & szY & ".mat")
        SetReady()
    End Sub


    Private Sub frmIR_Load(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles MyBase.Load
        'Dim bClearOldData As Boolean
        Me.Icon = frmMain.Icon
        gblnCancel = True

        'if ItemList.SelectedItemLast - ItemList.SelectedItemFirst + 1 > ItemList.SelectedItems.Count Then bClearOldData = True

        'set check boxes
        chkCalcResults.CheckState = DirectCast(-CInt(gIRFlags.CalcResults), CheckState)
        chkShowLatency.CheckState = DirectCast(-CInt(gIRFlags.ShowLatency), CheckState)
        chkPlotIR.CheckState = DirectCast(-CInt(gIRFlags.PlotIR), CheckState)
        chkSaveIR.CheckState = DirectCast(-CInt(gIRFlags.SaveIR), CheckState)
        chkCalcLinear.CheckState = DirectCast(-CInt(gIRFlags.CalcLinear), CheckState)
        chkSaveLinearToMAT.CheckState = DirectCast(-CInt(gIRFlags.SaveLinearToMAT), CheckState)
        chkSaveLinearToWAV.CheckState = DirectCast(-CInt(gIRFlags.SaveLinearToWAV), CheckState)

        'If bClearOldData Then
        '    chkKeepVar.CheckState = CheckState.Checked
        '    chkKeepTemp.CheckState = CheckState.Checked
        'Else
        chkKeepTemp.CheckState = DirectCast(-CInt(gIRFlags.KeepTemp), CheckState)
        chkKeepVar.CheckState = DirectCast(-CInt(gIRFlags.KeepVar), CheckState)
        'End If

        'chkGaborMult.CheckState = DirectCast(-CInt(gIRFlags.GaborMult), CheckState)
        'chkGaborMult.Enabled = CBool(chkCalcResults.CheckState)
        chkCreateMatrix.CheckState = DirectCast(-CInt(gIRFlags.CreateMatrix), CheckState)
        'chkPlotMatrix.CheckState = DirectCast(-CInt(gIRFlags.PlotMatrix), CheckState)
        'chkSaveWorkspace.CheckState = DirectCast(-CInt(gIRFlags.SaveWorkspace), CheckState)
        chkCopyLatency.CheckState = DirectCast(-CInt(gIRFlags.CopyLatency), CheckState)
        chkFirstCheck.CheckState = DirectCast(-CInt(gIRFlags.FirstCheck), CheckState)
        chkSaveM.CheckState = DirectCast(-CInt(gIRFlags.SaveM), CheckState)
        chkSaveSOFA.CheckState = DirectCast(-CInt(gIRFlags.SaveSOFA), CheckState)
        chkProcessItemRange.CheckState = DirectCast(-CInt(gIRFlags.ProcessItemRange), CheckState)
        txtItemRangeMin.Enabled = CBool(chkProcessItemRange.CheckState)
        txtItemRangeMax.Enabled = CBool(chkProcessItemRange.CheckState)
        If Len(txtItemRangeMin.Text) = 0 Then 'Set default item range values
            chkCalcResults.CheckState = CheckState.Checked
            chkCalcLinear.CheckState = CheckState.Checked
            chkCreateMatrix.CheckState = CheckState.Checked
            'chkKeepVar.CheckState = CheckState.Checked
            'chkKeepTemp.CheckState = CheckState.Checked
            If ItemList.ItemCount > 144 Then 'default list yellow
                txtItemRangeMin.Text = TStr(ItemList.ItemCount - 144)
            Else
                txtItemRangeMin.Text = "1"
            End If
        End If
        'chkGaborMult.Enabled = CBool(chkCalcResults.CheckState)
        If Len(txtItemRangeMax.Text) = 0 Then
            If ItemList.ItemCount > 144 Then
                txtItemRangeMax.Text = TStr(ItemList.ItemCount - 1)
            Else
                txtItemRangeMax.Text = TStr(ItemList.ItemCount)
            End If
        End If
        If val(txtItemRangeMax.Text) > ItemList.ItemCount Then txtItemRangeMax.Text=TStr(Val(txtItemRangeMax.Text))

        If grectfrmIR.Left <> 0 And grectfrmIR.Top <> 0 Then
            Me.Left = CInt(Val(VB6.TwipsToPixelsX(grectfrmIR.Left)))
            Me.Top = CInt(Val(VB6.TwipsToPixelsY(grectfrmIR.Top)))
        Else
            Me.Left = frmMain.Left + CInt(Me.Width / 10)
            Me.Top = frmMain.Top + CInt(Me.Height / 10)
        End If
        if gRecStream IsNot Nothing
            numFirstCheckChannel.Maximum = Math.Max(1,gRecStream.Length)
        End If
    End Sub

    Private Sub frmIR_FormClosing(ByVal eventSender As System.Object, ByVal eventArgs As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        Dim Cancel As Boolean = eventArgs.Cancel
        Dim UnloadMode As System.Windows.Forms.CloseReason = eventArgs.CloseReason
        grectfrmIR.Left = CInt(Val(VB6.PixelsToTwipsX(Me.Left)))
        grectfrmIR.Top = CInt(Val(VB6.PixelsToTwipsY(Me.Top)))
        eventArgs.Cancel = Cancel
    End Sub

    Private Sub chkProcessItemRange_CheckedChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles chkProcessItemRange.CheckedChanged
        txtItemRangeMin.Enabled = CBool(chkProcessItemRange.CheckState)
        txtItemRangeMax.Enabled = CBool(chkProcessItemRange.CheckState)
    End Sub


    'Private Sub chkCalcResults_CheckedChanged(sender As Object, e As EventArgs) Handles chkCalcResults.CheckedChanged
    '    'chkGaborMult.Enabled = CBool(chkCalcResults.CheckState)
    '    chkGaborMult.Enabled = False
    'End Sub

    Private Sub btnOpenDocFolder_Click(sender As Object, e As EventArgs) Handles btnOpenDocFolder.Click
        Dim DocPath As String = Nothing

        If Strings.Right(My.Application.Info.DirectoryPath, Len("\bin")) = "\bin" Then
            DocPath = Strings.Left(My.Application.Info.DirectoryPath, Len(My.Application.Info.DirectoryPath) - Len("\bin"))
        ElseIf Strings.Right(My.Application.Info.DirectoryPath, Len("\obj")) = "\obj" Then
            DocPath = Strings.Left(My.Application.Info.DirectoryPath, Len(My.Application.Info.DirectoryPath) - Len("\obj"))
        Else
            DocPath = My.Application.Info.DirectoryPath ' & "\doc"
        End If

        'Dim HistoryFile As String = HistoryPath & "\history.txt"
        Process.Start(DocPath & "\doc")
    End Sub

    Private Sub cmdSaveSOFA_Click(sender As System.Object, e As System.EventArgs) Handles cmdSaveSOFA.Click

        'Start SOFA
        Dim szX As String = "AA_SOFAstart"
        szX = MatlabCmd(szX)
        If InStr(LCase(szX), "error") <> 0 Then MsgBox("SOFAstart cannot be found! Please download the SOFA package from:" & vbCrLf & "https://sourceforge.net/projects/sofacoustics/" & _
            vbCrLf & "and extract the files to your \MATLAB\SOFA subfolder." & vbCrLf & vbCrLf & vbCrLf & szX, MsgBoxStyle.Critical, "Load SOFA: Error when starting SOFA")

        frmPostProcessing.SOFAsave()

    End Sub


    Private Sub cmdReshapeToM_Click(ByVal eventSender As System.Object, ByVal eventArgs As System.EventArgs) Handles cmdReshapeToM.Click

        Dim szErr As String = hMtoSOFA()
        If Len(szErr) > 0 Then MsgBox(szErr, MsgBoxStyle.Critical, "Error") : SetReady() : Exit Sub

        SetStatus("hLIN reshaped to SOFA; hLIN and idxLIN deleted")
        SetStatus("Structured meta data created in SOFA format")

        SetReady()
    End Sub

    Private Function hMtoSOFA() As String
        Dim szX As String

        'szX = MatlabCmd("h-M_old=reshape(hLIN,idxLIN(1,2),[]," & TStr(UBound(gRecStream) + 1) & ");")
        'If Len(szX) > 0 Then Return szX

        szX = MatlabCmd("gRecStream=" & TStr(UBound(gRecStream)) & ";")
        If Len(szX) > 0 Then Return szX

        'Add some h-M SOFA parameters
        szX = MatlabCmd("AA_hM;")
        If Len(szX) > 0 Then Return szX

        szX = MatlabCmd("clear hLIN idxLIN")
        If Len(szX) > 0 Then Return szX

        'no errors
        Return ""
    End Function

End Class