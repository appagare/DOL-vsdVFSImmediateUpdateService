Option Strict On
Option Explicit On 

Imports System.ServiceProcess
Imports WA.DOL.LogEvent.LogEvent
Imports WA.DOL.Data
Imports System.Threading
Imports System.Data.SqlClient

Public Class vsdVFSImmediateUpdate
    Inherits System.ServiceProcess.ServiceBase

    'module level parameters

    'comments
    'DebugMode = 0 - basic application logging, start/end, errors, etc. Best performance.
    'DebugMode = 1 - zero value logging plus transaction performance. Useful for tracking performance at the transaction level.
    'DebugMode = 2 - one value logging plus extra debugging (messages, parsed tran type, requests, responses, etc.). Useful for debugging purposes, but will degrade performance.

    Private Const RESPONSECODE_RECOVERABLE As String = "01"
    Private Const RESPONSECODE_NONRECOVERABLE As String = "02"
    'Private Const RESPONSECODE_RECOVERABLE_WITH_NOTICE As String = "03"
    
    'db connection paramaters
    Private DBConnectRetryDelay As Integer = 30 'delay 30 sec. between initial DB connect attempts unless specified in app.config
    Private DBConnectRetryMax As Integer = 0 'try to obtain DB parameters indefinitely unless specified in app.config

    Private LogEventObject As New WA.DOL.LogEvent.LogEvent 'common LogEvent object
    Private DataObject As WA.DOL.Data.SqlHelper 'common Data object

    Private ErrorDelayMode As Boolean = False
    Private ThreadCount As Integer = 0 'number of threads spawned
    Private Stats As Stats  'common class to hold stat values
    Private ConfigValues As New ConfigValues 'common class to hold all of the common runtime parameters
    Private Credentials As Net.ICredentials 'interface for making secure web service calls
    Private Proxy As System.Net.WebProxy 'option proxy object

    Private PerformanceTestMode As Boolean = False

    'enumeration for state of the service
    Private Enum ServiceStates
        Shutdown = 0
        Paused = 1
        Running = 2
    End Enum
    Private Enum ResponseStatus
        NotSet = -1
        Success = 0
        Recoverable = 1
        NonRecoverable = 2
        '        RecoverableWithEmail = 3
    End Enum

    Friend Enum ExceptionPath
        QueueWrite = 0
        QueueException = 1
    End Enum

    Private ServiceState As ServiceStates = ServiceStates.Paused

#Region " Component Designer generated code "

    Public Sub New()
        MyBase.New()

        ' This call is required by the Component Designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call

    End Sub

    'UserService overrides dispose to clean up the component list.
    Protected Overloads Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If Not (components Is Nothing) Then
                components.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    ' The main entry point for the process
    <MTAThread()> _
    Shared Sub Main()
        Dim ServicesToRun() As System.ServiceProcess.ServiceBase

        ' More than one NT Service may run within the same process. To add
        ' another service to this process, change the following line to
        ' create a second service object. For example,
        '
        '   ServicesToRun = New System.ServiceProcess.ServiceBase () {New Service1, New MySecondUserService}
        '
        ServicesToRun = New System.ServiceProcess.ServiceBase() {New vsdVFSImmediateUpdate}

        System.ServiceProcess.ServiceBase.Run(ServicesToRun)
    End Sub

    'Required by the Component Designer
    Private components As System.ComponentModel.IContainer

    ' NOTE: The following procedure is required by the Component Designer
    ' It can be modified using the Component Designer.  
    ' Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()
        '
        'vsdVFSImmediateUpdate
        '
        Me.ServiceName = "vsdVFSImmediateUpdate"

    End Sub

#End Region

    Protected Overrides Sub OnStart(ByVal args() As String)
        ' Add code here to start your service. This method should set things
        ' in motion so your service can do its work.

        Try
            'read the basic operating parameters
            ReadAppSettings()

        Catch ex As Exception

            'LogEvent, Send E-mail, and quit
            Dim strMessage As String = "Service is unable to proceed. Shutting down. " & ex.Message
            'log the error
            LogEvent("Service_OnStart", strMessage, MessageType.Error, LogType.Standard)

            'initiate stop process
            InitiateStop()
            Exit Sub
        End Try

        'start an endless loop for service processing the queue
        ThreadPool.QueueUserWorkItem(AddressOf ServiceRun)

    End Sub

    Protected Overrides Sub OnStop()
        ' Add code here to perform any tear-down necessary to stop your service.
        'warn threads we are shutting down
        ServiceState = ServiceStates.Shutdown

        'log the fact that we are "starting to stop"
        LogEvent("OnStop", "Begin OnStop", MessageType.Information, LogType.Standard)

        'give threads up to 20 seconds to wrap things up (this should be more than enough time)
        Dim dtEndWait As Date = Now.AddSeconds(20)
        While Now <= dtEndWait
            If ThreadCount = 0 Then
                Exit While
            End If
        End While

        On Error Resume Next
        If ConfigValues.EnableStats = True Then
            'lock the object fetching the log string since
            'the method resets the counts when its time to log
            Dim strStatsMessage As String = ""
            SyncLock Stats
                strStatsMessage = Stats.LogString
            End SyncLock
            If strStatsMessage <> "" Then
                LogEvent("OnStop", strStatsMessage, MessageType.Debug, LogType.Standard)
            End If
        End If


        'log event that we have stopped
        LogEvent("OnStop", "Service Stopped", MessageType.Finish, LogType.Standard)

        Stats = Nothing
        LogEventObject = Nothing
        ConfigValues = Nothing

    End Sub

    Protected Overrides Sub OnShutdown()
        'calls the Windows service OnStop method with the OS shuts down.
        OnStop()
    End Sub
#Region "Service Code"

    ''' <summary>
    '''     Returns the content of the <buffer></buffer> element from the complete MSMQ message.
    ''' </summary>
    ''' <param name="Message">Entire MSMQ message.</param>
    ''' <remarks>
    '''     The MSMQ message is an XML string. The actual data that the calling program
    '''     generated is contained within the <buffer></buffer> element.
    '''     This function parses the XML to obtain the contents of the <buffer> element 
    '''     (i.e. - returns the original data that the calling program generated).
    '''     This function doesn't have error handling by design. Should an error occur,
    '''     such as the XML fail to load and/or parse because of bad input, the calling 
    '''     routine will handle the error.
    ''' </remarks>
    Private Function GetBuffer(ByVal Message As String) As String

        Dim XML As New Xml.XmlDocument
        XML.LoadXml(Message)
        Return XML.SelectSingleNode("qmsg/buffer").FirstChild.Value

    End Function
    ' <summary>
    '''     Returns the status value of the web method, based on the code.
    ''' </summary>
    ''' <param name="ResponseCode">Response code that was parsed from the web method call.</param>
    ''' <remarks>
    '''     The ResponseCode is checked against the ResponseValues datatable. If found, the 
    '''     associated value is returned. If not found, the transaction is determined to be 
    '''     recoverable and will be returned to the queue.
    '''     
    '''     Note - This function uses the Find method on the ResponseValues table. "Find" should be 
    '''     thread-safe when reading and it *should* only require synchronization when performing 
    '''     writes. However, If unpredictable results occur, particularly
    '''     under high volumes, consider adding synchronization by uncommenting 
    '''     the SyncLock and End SyncLock lines (synchronization may affect performance).
    '''     SyncLock ConfigValues.ResponseValues
    ''' </remarks>
    Private Function GetStatus(ByVal ResponseCode As String) As ResponseStatus
        Dim ReturnValue As ResponseStatus = ResponseStatus.NotSet
        Dim r As DataRow
        Dim KeyValues(1) As Object
        KeyValues(0) = ConfigValues.ProcessMode
        KeyValues(1) = ResponseCode

        If ConfigValues.DebugMode > 1 Then
            Me.LogEvent("GetStatus", "Debug ResponseCode: [" & ResponseCode & "]", MessageType.Debug, LogType.Standard)
        End If

        'SyncLock ConfigValues.ResponseValues
        r = ConfigValues.ResponseValues.Rows.Find(KeyValues)
        If Not r Is Nothing Then
            ReturnValue = CType(r("ResponseValue"), ResponseStatus)
        Else
            ReturnValue = ResponseStatus.Recoverable
        End If
        'End SyncLock

        'return the result
        Return ReturnValue

    End Function

    ''' <summary>
    '''     Programmatically stop the main thread if we've already started up
    '''     such as during the database connection
    ''' </summary>
    Private Sub InitiateStop()
        Dim sc As New ServiceController(Me.ServiceName)
        sc.Stop()
        sc = Nothing
    End Sub
    ''' <summary>
    '''     Common code for writing an event to the event log database of the specified type.
    ''' </summary>
    ''' <param name="Source">source procedure reporting the event.</param>
    ''' <param name="Message">actual event message.</param>
    ''' <param name="MessageType">LogEvent object indicator specifying whether the message is error, informational, start, finish, or debug.</param>
    ''' <param name="LogType">LogEvent object indicator specifying the type of event to log (Standard, E-mail, etc.)</param>
    ''' <param name="ForceEmail">Forces an e-mail to be sent, regardless of error type or whether or </param>
    ''' <remarks>
    '''     When a LogType is error, an e-mail may be automatically sent. To avoid flooding the AppSupport Inbox, e-mails 
    '''     are only sent once every ConfigValues.EmailFrequency seconds UNLESS the ForceEmail flag is set.
    ''' </remarks>
    Private Sub LogEvent(ByVal Source As String, _
        ByVal Message As String, _
        ByVal MessageType As MessageType, _
        ByVal LogType As LogType, _
        Optional ByVal ForceEmail As Boolean = False)

        'log message
        LogEventObject.LogEvent(Me.ServiceName, Source, Message, MessageType, LogType)

        'if message type is an error, also log an e-mail event if we haven't sent one in awhile
        If ForceEmail = True Or (MessageType = MessageType.Error AndAlso Now >= ConfigValues.LastEmailSent.AddSeconds(ConfigValues.EmailFrequency)) Then

            'send the e-mail
            LogEventObject.LogEvent(Me.ServiceName, Source, Message, MessageType, LogType.Email)

            'update the last email sent time
            ConfigValues.LastEmailSent = Now
        End If
    End Sub
    ''' <summary>
    '''     Worker thread to process the message.
    ''' </summary>
    ''' <param name="State">New thread callback.</param>
    ''' <remarks>
    '''     This runs in a continuous loop until the service is stopped.
    '''     Multiple threads are spawned for each message in the queue, up to the 
    '''     ConfigValue.MaxThreads value. The thread will sleep when no messages are 
    '''     found in the queue or when a recoverable error occurs.
    ''' </remarks>
    Private Sub ProcessMessage(ByVal State As Object)

        Dim datStart As Date = Now 'used for performance testing (DebugMode > 0)
        Dim datWebStart As Date = Now 'used for performance testing (DebugMode > 0)
        Dim datWebEnd As Date = Now 'used for performance testing (DebugMode > 0)
        Dim strMessage As String = DirectCast(State, String) ' the complete MSMQ message
        Dim strTranType As String = "" 'tran type portion of the queue message
        Dim strBuffer As String = "" 'buffer portion of the queue message
        Dim strRequest As String = "" 'web method request
        Dim strResponse As String = "" 'web method response
        Dim bytIdx As Byte = 0 'index for multiple web service calls
        Dim XML As New Xml.XmlDocument 'message in queue is an XML string
        Dim CallStatus As ResponseStatus = ResponseStatus.NotSet
        Dim TranTypeUtil As TranTypeUtility ' utility object for this TranType
        Dim bytUpdateValue As Byte = 0 'status indicator of the transaction 
        Dim bytOnlineUpdateIndicator As Byte = 0 'status indicator of the data record


        Dim ExceptionAction As ExceptionPath = ExceptionPath.QueueException 'determines what action to take when an exception occurs. Start out in queue exception mode

        'log the request
        If ConfigValues.DebugMode > 1 Then
            LogEvent("ProcessMessage", "Debug Message: " & strMessage, MessageType.Debug, LogType.Standard)
        End If

        Try

            'parse buffer from XML document
            'Test Case #4 - if message isn't xml, an exception will be raised, 
            strBuffer = GetBuffer(strMessage) ' buffer portion of the MSMQ message
            strTranType = Trim(Left(strBuffer, 4)) 'get the tran type from the buffer

            'instantiate the TranTypeUtil
            TranTypeUtil = New TranTypeUtility(strTranType, ConfigValues.TranTypes) 'get utility object for this TranType

            'extra debugging
            If ConfigValues.DebugMode > 1 Then
                Me.LogEvent("ProcessMessage", "Debug TranType :[" & strTranType & "]", MessageType.Debug, LogType.Standard)
            End If

            'Test Case #3
            If TranTypeUtil.TranTypeIsValid = False Then
                'unrecognizable TranType
                'write it to the exception table,
                Throw New Exception("Unrecognized TranType (" & strTranType & ").")
            End If

            If ConfigValues.EnableStats = True Then
                'lock the object when updating the count 
                SyncLock Stats
                    Stats.Increment(strTranType)
                End SyncLock
            End If

            'get the message into an XML document so we can add a status node
            XML.LoadXml(strMessage)

            'increment our attempt counter
            strMessage = TranTypeUtil.IncrementAttemptCount(XML)

            'any unhandled exceptions should return the message to the queue
            ExceptionAction = ExceptionPath.QueueWrite

            'if here, TranType is valid, perform all of the calls
            For bytIdx = 0 To CByte(TranTypeUtil.CallCount - 1)

                ' see if this call was successfully made already
                If TranTypeUtil.IsProcessCallCompleted(XML, bytIdx) = False Then
                    'call not yet made or was previously unsuccessful - ok to make the call

                    'build the request string from the queue message
                    strRequest = TranTypeUtil.GetRequest(strBuffer, bytIdx, bytOnlineUpdateIndicator, ExceptionAction)

                    'extra debugging
                    If ConfigValues.DebugMode > 1 Then
                        Me.LogEvent("ProcessMessage", "Debug Request: [" & strRequest & "]", MessageType.Debug, LogType.Standard)
                    End If

                    If strRequest = "" Or bytOnlineUpdateIndicator > bytIdx Then
                        'Test Case #7
                        'the only time TranTypeUtil.GetRequest should return 
                        'an empty string w/out throwing an error or if the OnlineUpdateIndicator is greater than 
                        'the index (but the message's IsProcessCallCompleted indicates the call was completed) 
                        'would be if the stored proc. reports the record has already been updated.
                        'For example, if a message ends up in the exception table, is corrected
                        'and completes with the night batch reload, and then the exception batch 
                        'process reloads the queue.
                        '
                        'this should be a very rare occurrence. The point is, the record's online 
                        'update indicator is already set so we should not re-submit it. However, we 
                        'will log this occurrence, force an e-mail, and exit. Record is neither 
                        'returned to the queue nor the exception table.
                        Me.LogEvent("ProcessMessage", "Stored proc. indicates the transaction has been updated. " & strMessage, MessageType.Information, LogType.Standard, True)
                        Exit Try
                    End If

                    'note to future coders - if online update supports different URLs, 
                    'obtaining the web reference may involve polymorphism or a commmon 
                    'interface to handle different web objects
                    Dim WS As New dolVFSService.dolVFSservice(TranTypeUtil.URL(bytIdx))
                    Dim SPA As New dolSPAService.dolSPAservice(TranTypeUtil.URL(bytIdx))
                    Dim IU As New dolVFSIUService.dolVFSIUService(TranTypeUtil.URL(bytIdx))

                    'if we have credentials, assign them to the web service request
                    If Not Credentials Is Nothing Then
                        WS.Credentials = Credentials
                        SPA.Credentials = Credentials
                        IU.Credentials = Credentials
                    End If

                    'if we have a proxy, assign it to the web service request
                    If Not Proxy Is Nothing Then
                        WS.Proxy = Proxy
                        SPA.Proxy = Proxy
                        IU.Proxy = Proxy
                    End If

                    'clear this before each web service call
                    CallStatus = ResponseStatus.NotSet

                    datWebStart = Now 'capture the start time of the web service call
                    'Test Case #8
                    Select Case UCase(TranTypeUtil.WebMethod(bytIdx))
                        Case "MCTCAP"
                            strResponse = WS.CallMCTCAP(strRequest)
                        Case "MFLCAP"
                            strResponse = WS.CallMFLCAP(strRequest)
                        Case "MMCFWD"
                            strResponse = WS.CallMMCFWD(strRequest)
                        Case "MMCINQ"
                            strResponse = WS.CallMMCINQ(strRequest)
                        Case "MMDEST"
                            strResponse = WS.CallMMDEST(strRequest)
                        Case "MMDOEU"
                            strResponse = WS.CallMMDOEU(strRequest)
                        Case "MMDROS"
                            strResponse = WS.CallMMDROS(strRequest)
                        Case "MMVUOU"
                            strResponse = WS.CallMMVUOU(strRequest)
                        Case "MMZUBO"
                            strResponse = WS.CallMMZUBO(strRequest)
                        Case "MMZUOU"
                            strResponse = WS.CallMMZUOU(strRequest)
                        Case "MVBNCI"
                            strResponse = WS.CallMVBNCI(strRequest)
                        Case "VESVOID"
                            strResponse = WS.CallVESVOID(strRequest)
                        Case "VEHVOID"
                            strResponse = WS.CallVEHVOID(strRequest)
                        Case "MMZADU"
                            strResponse = SPA.CallMMZADU(strRequest)
                        Case "MMVDCU"
                            strResponse = IU.CallMMVDCU(strRequest)
                        Case "MMCUPD"
                            strResponse = IU.CallMMCUPD(strRequest)
                        Case "MMCONF"
                            strResponse = IU.CallMMCONF(strRequest)
                        Case Else
                            'Test Case #9
                            'unknown web method (shouldn't happen unless the luTranType table is "out of whack"
                            'change the exception action and raise an exception to force the exception record to be written.
                            ExceptionAction = ExceptionPath.QueueException
                            Throw New Exception("Unrecognizable WebMethod: [" & UCase(TranTypeUtil.WebMethod(bytIdx)) & "]")
                    End Select

                    'capture the end-time of the web service call
                    datWebEnd = Now

                    'release the web service resources
                    WS = Nothing
                    SPA = Nothing
                    IU = Nothing

                    'extra debugging
                    If ConfigValues.DebugMode > 1 Then
                        Me.LogEvent("ProcessMessage", "Debug Response: [" & strResponse & "]", MessageType.Debug, LogType.Standard)
                    End If

                    'determine the status from the response
                    CallStatus = GetStatus(Mid(strResponse, TranTypeUtil.ResponseOffset(bytIdx), TranTypeUtil.ResponseLength(bytIdx)))

                    Select Case CallStatus
                        Case ResponseStatus.Success
                            'update the queue message with the successful web service call
                            strMessage = TranTypeUtil.SetProcessCallStatus(XML, bytIdx)
                        Case ResponseStatus.Recoverable

                            Me.LogEvent("ProcessMessage", "Recoverable Response: [" & strResponse & "]", MessageType.Debug, LogType.Standard)

                            'if recoverable, check to see if we've given this message a reasonable number of tries to complete
                            If TranTypeUtil.GetAttemptCount(XML) > ConfigValues.MaxQueueMessageProcessAttempts _
                                AndAlso ConfigValues.MaxQueueMessageProcessAttempts > 0 Then
                                'we have a non-zero retry setting, we've tried repeatedly, so this now becomes an exception
                                'write it to the exception table,
                                WriteException(strMessage, "ProcessMessage", "Maximum attempts for this message have been reached: [" & _
                                    strTranType & "] Message: " & strMessage, RESPONSECODE_RECOVERABLE)
                                'bail out of here

                                Exit Try
                            End If

                            'if here, we'll give this message another shot later;
                            'QueueWrite causes the service to sleep
                            QueueWrite(ConfigValues.QueuePath, strMessage)
                            Exit Try
                            'Case ResponseStatus.RecoverableWithEmail
                            '    'if recoverable w/ e-mail, check to see if we've given this message a reasonable number of tries to complete
                            '    If TranTypeUtil.GetAttemptCount(XML) > ConfigValues.MaxQueueMessageProcessAttempts _
                            '        AndAlso ConfigValues.MaxQueueMessageProcessAttempts > 0 Then
                            '        'we have a non-zero setting, we've tried repeatedly, this now becomes an exception
                            '        'write it to the exception table, and force e-mail
                            '        WriteException(strMessage, "ProcessMessage", "NMVTIS brand error. Maximum attempts for this message have been reached: [" & _
                            '            strTranType & "] Message: " & strMessage, RESPONSECODE_RECOVERABLE_WITH_NOTICE, True)
                            '        'bail out of here
                            '        Exit Try
                            '    End If

                            '    'if here, we'll give this message another shot later;
                            '    'but, we always force an e-mail message when this occurs
                            '    Me.LogEvent("ProcessMessage", "NMVTIS brand error. Message: " & strMessage, MessageType.Error, LogType.Standard, True)

                            '    'QueueWrite causes the service to sleep
                            '    QueueWrite(ConfigValues.QueuePath, strMessage)
                            '    Exit Try
                        Case ResponseStatus.NonRecoverable

                            Me.LogEvent("ProcessMessage", "Non-Recoverable Response: [" & strResponse & "]", MessageType.Debug, LogType.Standard)

                            'if not recoverable, write to exception table. Additional messages will continue to be processed

                            WriteException(strMessage, "ProcessMessage", "NonRecoverable Response: [" & _
                                strResponse & "] Request: [" & strRequest & "]", RESPONSECODE_NONRECOVERABLE)
                            Exit Try
                        Case Else
                            'shouldn't happen unless there is coding problem.
                            'if here it means we recognized the tran type, generated a request
                            'but failed to set any value based on the response.
                            WriteException(strMessage, "ProcessMessage", "Unknown Status: " & CallStatus.ToString & _
                            " Response: [" & strResponse & "] Request: [" & strRequest & "]", RESPONSECODE_NONRECOVERABLE)
                            Exit Try
                    End Select
                End If 'ProcessCall already made, skip this
            Next

            'all calls complete
            'update VFS
            For bytIdx = 0 To CByte(TranTypeUtil.CallCount - 1)

                If PerformanceTestMode = True Then
                    'performance test only - bail before updating VFS
                    Exit For
                End If

                Try
                    'for each row associated with this trantype, try to update the database
                    'note - the update value (i.e. - OnlineStatusInd) will be equal to the number of successful 
                    'web service calls, such as 0 if none are successful (but we wouldn't be here if that happens), 
                    '1 if there is one successful call, 2 if there are two successful calls, etc.)
                    'It is also the same as bytIdx + 1
                    TranTypeUtil.MessageComplete(strBuffer, bytIdx, CType(TranTypeUtil.CallCount, Decimal))
                Catch ex As Exception
                    'Test Case #11
                    'if an error occurs updating the database, return to Queue which causes the service to sleep
                    'the next time its read, it will either try again or write to the exception table.
                    Throw New Exception("Error updating Field System record on " & TranTypeUtil.TranType & ". " & ex.Message)
                    'stop trying to update the database if any error occurs
                    Exit For
                End Try
            Next


        Catch ex As Exception
            'Common error handler for this procedure. If here, either an unknown
            'error occurred or the code forced an error based on some business rule.
            'One of two actions will occur here - the message is immediately 
            'written to the exception table OR the message is returned to the 
            'queue (in the event that the message has been returned to the queue 
            'multiple times, it will be written to the exception table.

            'capture all the details we can
            Dim strErrMessage As String = "Error: " & CType(ex.Message, String)

            If CType(strMessage, String) <> "" Then
                strErrMessage &= " Message[" & CType(strMessage, String) & "]"
            End If

            If strTranType <> "" Then
                strErrMessage &= " TranType[" & strTranType & "]"
            End If
            If CType(strBuffer, String) <> "" Then
                strErrMessage &= " Buffer[" & CType(strBuffer, String) & "]"
            End If
            If strRequest <> "" Then
                strErrMessage &= " Request[" & strRequest & "]"
            End If
            If CType(strResponse, String) <> "" Then
                strErrMessage &= " Response[" & CType(strResponse, String) & "]"
            End If
            If Len(strErrMessage) > 5000 Then
                strErrMessage = Left(strErrMessage, 5000) 'field only holds 5Kb, so truncate as necessary
            End If

            If ExceptionAction = ExceptionPath.QueueException Then
                'we should write to the exception table
                'log the error; this will cause an e-mail if we haven't sent one recently
                LogEvent("ProcessMessage (QueueException)", strErrMessage, MessageType.Error, LogType.Standard)
                'write to the exception table
                WriteException(strMessage, "ProcessMessage", strErrMessage, RESPONSECODE_NONRECOVERABLE)

            Else 'ExceptionPath = ErrorProcessPath.QueueWrite 

                'we should return the message to the queue (normal for most cases)
                LogEvent("ProcessMessage (QueueWrite)", strErrMessage, MessageType.Error, LogType.Standard)

                'see if we've repeatedly tried this message
                If Not TranTypeUtil Is Nothing AndAlso Not XML Is Nothing Then
                    'the code progressed to the point of obtaining an XML document and TranTypeUtility object
                    'check to see if we've reached our retry limit 
                    If TranTypeUtil.GetAttemptCount(XML) > ConfigValues.MaxQueueMessageProcessAttempts _
                        AndAlso ConfigValues.MaxQueueMessageProcessAttempts > 0 Then
                        'we have a non-zero setting, we've tried repeatedly, this now becomes an exception so 
                        'write it to the exception table.
                        WriteException(strMessage, "ProcessMessage", "Maximum attempts for this message have been reached. " & _
                            strErrMessage, RESPONSECODE_RECOVERABLE)
                        'bail out of here without writing the message to the queue.
                        Exit Try
                    End If
                End If

                'if here, we'll give this message another shot later;
                'QueueWrite causes the service to sleep
                QueueWrite(ConfigValues.QueuePath, strMessage)
            End If 'QueueWrite or QueueException
        End Try

        'log the response
        If ConfigValues.DebugMode > 0 Then
            'get the overall message processing time
            Dim RunLength As System.TimeSpan = Now.Subtract(datStart)
            Dim Millisecs As Integer = CType(RunLength.TotalMilliseconds, Integer)
            Dim strLogMessage As String = ""
            Dim MillisecsWS As Integer = 0
            strLogMessage = "Tran. Dur: " & Millisecs.ToString & " ms"


            If datWebStart > datStart AndAlso datWebEnd > datWebStart Then
                'get the web service call processing time if we captured it
                RunLength = datWebEnd.Subtract(datWebStart)
                MillisecsWS = CType(RunLength.TotalMilliseconds, Integer)
                strLogMessage &= " Web Dur: " & MillisecsWS.ToString & " ms"
            End If
            LogEvent("ProcessMessage", strLogMessage, MessageType.Debug, LogType.Standard)
        End If

        'clean up
        XML = Nothing
        If Not TranTypeUtil Is Nothing Then
            TranTypeUtil = Nothing
        End If

        'decrement the thread count
        Interlocked.Decrement(ThreadCount)

    End Sub
    ''' <summary>
    '''     Main thread for the service.
    ''' </summary>
    ''' <param name="State">New thread callback.</param>
    ''' <remarks>
    '''     This runs in a continuous loop until the service is stopped.
    '''     Multiple threads are spawned for each message in the queue, up to the 
    '''     ConfigValue.MaxThreads value. The thread will sleep when no messages are 
    '''     found in the queue or when a recoverable error occurs.
    ''' </remarks>
    Protected Sub ServiceRun(ByVal State As Object)

        'make note that we have started
        LogEvent("ServiceRun", "Checking settings.", MessageType.Start, LogType.Standard)

        Try

            Dim QueueObject As WA.DOL.MsmqHelper 'object to see if a message is in the queue
            Dim ts As TimeSpan 'receive message time span

            'get the db settings
            ReadDBSettings()

            'validate settings
            If Not ValidSettings() Then
                'we read all of the values but do not 
                'have valid parameters and thus are unable 
                'to continue. Throw error to drop into exception catch
                '
                Throw New Exception("Invalid settings at start up.")
            End If


            'make note that we started
            LogEvent("ServiceRun", "Settings ok. Starting main loop.", MessageType.Start, LogType.Standard)

            'set our status to run mode
            ServiceState = ServiceStates.Running

            'set some working paramaters based on the ConfigValues
            ts = New TimeSpan(0, 0, 0, 0, ConfigValues.QueueRetrieveWait)

            'set the credentials if present
            If ConfigValues.UseSystemCredentials = True Then
                'use the credentials of the account we are running under
                Credentials = System.Net.CredentialCache.DefaultCredentials()
            End If

            'set the proxy if a name is specified
            If ConfigValues.ProxyName <> "" Then
                Proxy = New System.Net.WebProxy(ConfigValues.ProxyName, True)
                'set the credentials of the proxy if we have credentials
                If Not Credentials Is Nothing Then
                    Proxy.Credentials = Credentials
                End If
            End If

            'loop here while service is running
            While ServiceState = ServiceStates.Running

                Dim intAvailableThreads As Integer = 0
                Dim intIOThreads As Integer = 0

                'check resource availability
                ThreadPool.GetAvailableThreads(intAvailableThreads, intIOThreads)

                Try

                    If ConfigValues.EnableStats = True Then
                        'lock the object fetching the log string since
                        'the method resets the counts when its time to log
                        Dim strStatsMessage As String = ""
                        SyncLock Stats
                            strStatsMessage = Stats.LogString
                        End SyncLock
                        If strStatsMessage <> "" Then
                            LogEvent("ServiceRun", strStatsMessage, MessageType.Debug, LogType.Standard)
                        End If
                    End If
                    '
                    'if we have resources, start reading the queue
                    If ThreadCount < intAvailableThreads AndAlso ThreadCount <= ConfigValues.MaxThreads Then

                        'before we start, see if a thread set error mode
                        If ErrorDelayMode = True Then
                            'Test Case #12
                            'if ErrorDelayMode is set, either some thread set it or 
                            'VFS is unavailable (most likely case). Either way, 
                            'Exit Try and we'll drop into extended sleep mode.
                            Exit Try
                        End If

                        'unless shutdown or the ErrorDelay has been signaled by a thread (QueueWrite or VFS became unavailable), check for messages
                        Do While ServiceState = ServiceStates.Running AndAlso ErrorDelayMode = False

                            'see if anything is present in the queue
                            Dim Message As String = QueueObject.ReceiveMessage(ConfigValues.QueuePath, ts)

                            'make sure we still have resource availability - necessary within the loop since 
                            'changes may occur on separate threads after entering the loop
                            ThreadPool.GetAvailableThreads(intAvailableThreads, intIOThreads)

                            If ThreadCount >= intAvailableThreads OrElse ThreadCount >= ConfigValues.MaxThreads OrElse Message Is Nothing Then
                                'no threads are available or max hit during loop;
                                'this will ultimately drop us into Sleep mode

                                'we have a message; log this condition and return the message to the queue
                                If Not Message Is Nothing Then
                                    If ConfigValues.DebugMode > 0 Then
                                        LogEvent("ServiceRun", "ThreadCount:" & ThreadCount.ToString & _
                                                                                " AvailableThreads: " & intAvailableThreads.ToString & _
                                                                                " ProcessMaxThreads: " & ConfigValues.MaxThreads.ToString, _
                                                                                MessageType.Information, LogType.Standard, False)
                                    End If
                                    QueueWrite(ConfigValues.QueuePath, Message)

                                    'must clear this because QueueWrite sets it, causing a 30 sec. delay
                                    ErrorDelayMode = False
                                End If

                                Exit Do
                            End If

                            'increment the thread count (each thread will decrement this when its done)
                            Interlocked.Increment(ThreadCount)

                            'process each message on a separate thread
                            ThreadPool.QueueUserWorkItem(AddressOf ProcessMessage, Message)
                        Loop 'thread resource or ErrorDelay signal

                    End If 'thread resources availability

                    'queue is empty or currently at the max thread count or VFS is not available, so take nap
                    Thread.Sleep(ConfigValues.SleepWhenQueueEmpty)

                Catch ex As Exception
                    'Test Case #2
                    'log an event and send an e-mail if applicable, then sleep if an exception other than NightBatch in progress
                    Dim strMessage As String = ex.Message & vbCrLf & vbCrLf & "Available Threads: " & CStr(intAvailableThreads) & vbCrLf & _
                            "Thread Count: " & CStr(ThreadCount) & vbCrLf & vbCrLf & _
                            "Queue: " & ConfigValues.QueuePath

                    LogEvent("ServiceRun", "Err: " & strMessage, MessageType.Error, LogType.Standard)
                    'signal error sleep mode
                    ErrorDelayMode = True
                End Try

                If ErrorDelayMode = True Then
                    'a thread signaled an error that should trigger a sleep or VFS is unavailable
                    'reset and sleep
                    ErrorDelayMode = False
                    Thread.Sleep(ConfigValues.SleepWhenError)
                End If

            End While 'main loop - ServiceStates.Running

        Catch ex As Exception

            'LogEvent, Send E-mail, and quit
            Dim strMessage As String = "Service is unable to proceed. Shutting down. " & ex.Message
            'log the error
            LogEvent("Service_OnStart", strMessage, MessageType.Error, LogType.Standard, True)

            'initiate stop process

            Dim sc As New ServiceController(Me.ServiceName)
            sc.Stop()

            OnStop()
            Exit Sub
        End Try
    End Sub
    ''' <summary>
    '''     Sends a message to the queue.
    ''' </summary>
    ''' <param name="QueuePath">The full pathname to the queue.</param>
    ''' <param name="Message">String containing the entire queue message.</param>
    ''' <remarks>
    '''     Only supports private queues.
    '''     The size of the Message parameter cannot exceed 4 MB.
    ''' </remarks>
    Private Sub QueueWrite(ByVal QueuePath As String, _
        ByVal Message As String)

        'The MsmqHelper class cannot be inherited from, thus no New constructor
        Dim MSMQHelper As WA.DOL.MsmqHelper

        Try
            'the simplest method is to pass the complete server\queue name and the queue message
            MSMQHelper.SendMessage(QueuePath, Message, True)
        Catch ex As Exception
            'if this process errors, we should create an exception 
            'up to the calling process to be handled.
            WriteException(Message, "QueueWrite", "Error returning a message to the queue: [" & _
                    QueuePath & "] Error: " & ex.Message, RESPONSECODE_RECOVERABLE)
        End Try

        'signal sleep delay 
        ErrorDelayMode = True

    End Sub

    ''' <summary>
    '''     Retrieve a single parameter from app.config.
    ''' </summary>
    ''' <param name="Key">The name of the key being retrieved.</param>
    Private Function ReadAppSetting(ByVal Key As String) As String

        On Error Resume Next
        Dim AppSettingsReader As New System.Configuration.AppSettingsReader
        Dim strReturnValue As String = ""
        Key = Trim(Key)
        If Key <> "" Then
            'get the value
            strReturnValue = CType(AppSettingsReader.GetValue(Key, GetType(System.String)), String)
        End If
        AppSettingsReader = Nothing
        Return strReturnValue
    End Function
    ''' <summary>
    '''     Reads the basic app.config values.
    ''' </summary>
    Private Sub ReadAppSettings()
        'Purpose:   Read the basic app.config settings

        'set mode equal to Service Name
        ConfigValues.ProcessMode = Me.ServiceName

        'get DB connect string key
        ConfigValues.ConnectionKey = ReadAppSetting("DatabaseKey") 'get connect string key

        'get DB connect delay
        If IsNumeric(ReadAppSetting("CriticalConnectionRetry")) AndAlso _
            CType(ReadAppSetting("CriticalConnectionRetry"), Integer) > 0 Then
            DBConnectRetryDelay = CType(ReadAppSetting("CriticalConnectionRetry"), Integer)
        End If

        'get DB connect max
        If IsNumeric(ReadAppSetting("CriticalConnectionRetryMax")) AndAlso _
            CType(ReadAppSetting("CriticalConnectionRetryMax"), Integer) > 0 Then
            DBConnectRetryMax = CType(ReadAppSetting("CriticalConnectionRetryMax"), Integer)
        End If

        'Performance test mode
        If ReadAppSetting("PerformanceTest") = "1" Then
            PerformanceTestMode = True
        End If

    End Sub
    ''' <summary>
    '''     Connect to the vsdVFSImmediateUpdate database to obtain the operating parameters.
    '''     This will try a pre-determined number of times as defined by the app.config file.
    ''' </summary>
    Private Sub ReadDBSettings()

        On Error Resume Next 'start local error handling to handle db connect retries

        Dim intDBConnectAttempt As Integer = 0 'db connect counter
        Dim dsSettings As New DataSet
        Dim r As DataRow
        Dim DBConnectOK As Boolean = False

        Do While DBConnectOK = False
            'get the db app. settings
            dsSettings = DataObject.ExecuteDataset(ConfigValues.ConnectionKey, CommandType.StoredProcedure, _
                "selAppConfig", New SqlClient.SqlParameter("@strProcess", ConfigValues.ProcessMode))

            If Err.Number = 0 Then
                'we were able to connect to the db, so we can retrieve the settings 
                DBConnectOK = True

                'LastEmailSent is initialized as an "old" day upon instantiation 
                'However, if the DB didn't connect on the first try, we may have sent an e-mail so 
                'reset the LastEmailSent value so any new transactions errors generate e-mails immediately
                ConfigValues.LastEmailSent = Now.AddDays(-1)

                On Error GoTo 0 'resume normal error handling. 
                'Any errors here should now bubble up the stack through ServiceRun 
                'to OnStart, log the fatal exception and initiate shutdown

                For Each r In dsSettings.Tables(0).Rows
                    'Me.LogEvent("debug", LCase(CType(r("Name"), String)) & "=" & CType(r("Value"), String), MessageType.Debug, LogType.Standard)
                    Select Case LCase(CType(r("Name"), String))
                        Case "debugmode"
                            ConfigValues.DebugMode = CType(r("Value"), Byte)

                        Case "emailfrequency"
                            ConfigValues.EmailFrequency = CType(r("Value"), Integer)

                        Case "enablestats"
                            If CType(r("Value"), Integer) = 1 Then
                                ' ignore all other values
                                ConfigValues.EnableStats = True
                            End If
                        Case "errordelay"
                            ConfigValues.SleepWhenError = CType(r("Value"), Integer) * 1000 'in seconds

                        Case "maxconcurrentthreads"
                            ConfigValues.MaxThreads = CType(r("Value"), Integer)

                        Case "maxmessageattempts"
                            ConfigValues.MaxQueueMessageProcessAttempts = CType(r("Value"), Integer)

                        Case "queuesleepwhenempty"
                            ConfigValues.SleepWhenQueueEmpty = CType(r("Value"), Integer) 'milliseconds

                        Case "queuereadwaittime"
                            ConfigValues.QueueRetrieveWait = CType(r("Value"), Integer) 'milliseconds

                        Case "proxy"
                            ConfigValues.ProxyName = CType(r("Value"), String)

                        Case "vsipoconnectionkey"
                            ConfigValues.IPOConnectionKey = CType(r("Value"), String)

                        Case "usesystemcredentials"
                            If CType(r("Value"), String) = "0" Then
                                ConfigValues.UseSystemCredentials = False
                            Else
                                'should usually be true
                                ConfigValues.UseSystemCredentials = True
                            End If
                    End Select
                Next

                'get the queue list to process - (note - for now, this should return a single row per Process name 
                'but db design supports expansion with service code changes).
                ConfigValues.QueueTable = DataObject.ExecuteDataset(ConfigValues.ConnectionKey, CommandType.StoredProcedure, _
                    "selQueueList", New SqlClient.SqlParameter("@strProcess", ConfigValues.ProcessMode)).Tables(0)

                'get the tran code to web service calls cross-reference
                ConfigValues.TranTypes = DataObject.ExecuteDataset(ConfigValues.ConnectionKey, CommandType.StoredProcedure, _
                    "selTranTypes", New SqlClient.SqlParameter("@strProcess", ConfigValues.ProcessMode)).Tables(0)

                'get the response values 

                'index this datatable so we can do multi-threaded Find calls without fear of conflicts
                Dim Keys(1) As DataColumn

                ConfigValues.ResponseValues = DataObject.ExecuteDataset(ConfigValues.ConnectionKey, CommandType.StoredProcedure, _
                    "selResponseValues", New SqlClient.SqlParameter("@strProcess", ConfigValues.ProcessMode)).Tables(0)

                Keys(0) = ConfigValues.ResponseValues.Columns(0)
                Keys(1) = ConfigValues.ResponseValues.Columns(1)
                ConfigValues.ResponseValues.PrimaryKey = Keys

                If ConfigValues.EnableStats = True Then
                    'initialize the stats
                    Stats = New Stats(ConfigValues.TranTypes)
                End If

                Exit Do 'not really necessary since DBConnectOK is now true
            Else
                'Test Case #1
                'error connecting to db; handle retry loop

                'increment our counter
                intDBConnectAttempt += 1

                'log an event (which will send an e-mail, if appropriate)
                LogEvent("ReadDBSettings", "Attempt " & intDBConnectAttempt.ToString & " - " & _
                    Err.Description, MessageType.Error, LogType.Standard)

                If DBConnectRetryMax > 0 AndAlso intDBConnectAttempt >= DBConnectRetryMax Then
                    'we have a DB connect attempt limit and which reached it.

                    On Error GoTo 0 'resume normal error handling. 
                    'Throw exception which should bubble up the stack through ServiceRun 
                    'to OnStart, log the fatal exception, and initiate shutdown.
                    Throw New Exception("Unable to connect to database after " & DBConnectRetryMax.ToString & " attempts.")
                    Exit Sub
                End If

                'sleep for awhile (DBConnectRetryDelay is in seconds, so multiply)
                Thread.Sleep(DBConnectRetryDelay * 1000)

            End If
        Loop ' DBConnectOK = False

    End Sub

    ''' <summary>
    '''     If the Message has a process attempt value, this resets it.
    '''     This is called prior to writing an exception message.
    ''' </summary>
    ''' <param name="Message">MSMQ XML string.</param>
    Friend Function ResetAttemptCount(ByVal Message As String) As String

        On Error GoTo Err_Handler
        Dim XML As New Xml.XmlDocument
        Dim strReturnValue As String = Message ' default to the original value so no matter what, 
        'load the XML message
        XML.LoadXml(Message)
        'if the message has a process node, reset the value
        'note - if the message does not have a process node, we don't need to do anything
        If Not XML.SelectSingleNode("qmsg/process") Is Nothing Then
            'create the process node if it doesn't exist
            XML.SelectSingleNode("qmsg/process/@attempt").FirstChild.Value = "0"
            'update the value to return
            strReturnValue = XML.OuterXml
        End If
        XML = Nothing

Err_Handler:
        'return either the original value or the reset value
        Return strReturnValue
    End Function

    ''' <summary>
    '''     Verify we have the basic info to proceed. If so, this also sets the QueueName 
    '''     and QueueServer values from the QueueTable.
    ''' </summary>
    Private Function ValidSettings() As Boolean

        If ConfigValues.ProcessMode = "" Or ConfigValues.ConnectionKey = "" Or _
            ConfigValues.QueueTable.Rows.Count <> 1 Or ConfigValues.TranTypes.Rows.Count < 1 Or _
            ConfigValues.SleepWhenError < 1 Or ConfigValues.SleepWhenQueueEmpty < 1 _
            Or ConfigValues.IPOConnectionKey = "" Then
            Return False
        Else
            'for now, the queue table should only have one row. This may change in the future and, if so, 
            'this code will need to be modified
            ConfigValues.QueueServer = CType(ConfigValues.QueueTable.Rows(0)("Server"), String)
            ConfigValues.QueueName = CType(ConfigValues.QueueTable.Rows(0)("Queue"), String)
            If ConfigValues.ProxyName <> "" Then
                Proxy = New System.Net.WebProxy(ConfigValues.ProxyName)
            End If
        End If
        Return True
    End Function

    ''' <summary>
    '''     Writes a record to the exception table to be manually processed.
    ''' </summary>
    ''' <param name="Message">The complete MSMQ record - ready to be put back into the queue if necessary.</param>
    ''' <param name="Source">String indicating the source location that caused the record to be placed here.</param>
    ''' <param name="Description">String detailing why the record is here.</param>
    Private Sub WriteException(ByVal Message As String, ByVal Source As String, _
        ByVal Description As String, _
        ByVal ResponseCode As String, _
        Optional ByVal ForceEmail As Boolean = False)

        'log an event
        Me.LogEvent(Source, Description, MessageType.Error, _
                    LogType.Standard, ForceEmail)

        'reset the attempt count if necessary
        Message = ResetAttemptCount(Message)

        Try
            'write the message to the exception table
            DataObject.ExecuteNonQuery(ConfigValues.ConnectionKey, CommandType.StoredProcedure, _
                            "insException", _
                            New SqlClient.SqlParameter("@strProcess", ConfigValues.ProcessMode), _
                            New SqlClient.SqlParameter("@strMessage", Message), _
                            New SqlClient.SqlParameter("@strSource", Source), _
                            New SqlClient.SqlParameter("@strResponseCode", ResponseCode), _
                            New SqlClient.SqlParameter("@strErrDesc", Description))

        Catch ex As Exception
            'should happen, but we have a record that we can't put in the queue and were unable 
            'to write to the Exception table. Last ditch effort - log another message as an error event
            Me.LogEvent(Source, "Error writing message to exception table. Message:" & Message & _
                " Original Error:" & Description & _
                " New Error:" & ex.Message, MessageType.Error, LogType.Standard)
        End Try
    End Sub

#End Region
End Class

''' <summary>
'''     This friend class contains all of the operating values required by the service and threads. The
'''     service populates this class once at startup.
''' </summary>
Friend Class ConfigValues

    Private _ConnectionKey As String = "" 'vsdVFSImmediateUpdate connection string key
    Private _DebugMode As Byte = 0 'debugging indicator
    Private _EmailFrequency As Integer = 900 'number of seconds between error e-mails (db setting updates this value)
    Private _EnableStats As Boolean = False 'indicates whether we log hourly stats
    Private _IPOConnectionKey As String = "" 'connection string key used for the Message Processor when checking for VFS availability
    Private _LastEmailSent As Date = Now.AddDays(-1) 'initialize it to an "old" day
    Private _MaxThreads As Integer = 20 'max concurrent threads
    Private _ProcessMode As String = "" 'included to allow multiple instances of the service if necessary
    Private _MaxMessageProcessAttempts As Integer = 0 'the number of times a recoverable message should be attempted before becoming an exception
    Private _ProxyName As String = "" 'optional proxy name for the web service calls
    Private _QueueName As String = "" 'the name of the queue to read/write
    Private _QueueRetrieveWait As Integer = 500 'number of milliseconds to wait for a message
    Private _QueueServer As String = "" 'the name of the queue server 
    Private _SleepWhenError As Integer = 10000 'number of milliseconds to pause the process when a recoverable error occurs
    Private _SleepWhenQueueEmpty As Integer = 500 'number of milliseconds to wait when the queue is empty before checking again
    Private _UseSystemCredentials As Boolean = True 'web service security
    Private _QueuePath As String = "" 'full pathname to the private queue

    Private _QueueTable As New DataTable 'table containing the queue(s) to process 
    '(will likely be one queue but db schema design allows for possible multiple queues)
    Private _TranTypes As New DataTable 'table containing TranType cross reference info
    Private _ResponseValues As New DataTable 'table containing response values

    ''' <summary>
    '''     This property sets/returns the vsdVFSImmediateUpdate connection string key.
    ''' </summary>
    Friend Property ConnectionKey() As String
        Get
            Return _ConnectionKey
        End Get
        Set(ByVal Value As String)
            _ConnectionKey = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns a debug logging value. 0 equals basic debugging. A value of 1 
    '''     equals extra debugging. A value of 2 will also log each message request/response value.
    ''' </summary>
    Friend Property DebugMode() As Byte
        Get
            Return _DebugMode
        End Get
        Set(ByVal Value As Byte)
            _DebugMode = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the number of seconds that should pass between e-mail error notifications.
    ''' </summary>
    Friend Property EmailFrequency() As Integer
        Get
            Return _EmailFrequency
        End Get
        Set(ByVal Value As Integer)
            _EmailFrequency = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the whether hourly stats are enabled or not.
    ''' </summary>
    Friend Property EnableStats() As Boolean
        Get
            Return _EnableStats
        End Get
        Set(ByVal Value As Boolean)
            _EnableStats = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the vsIPO connection string key (used for checking HP Availability).
    ''' </summary>
    Friend Property IPOConnectionKey() As String
        Get
            Return _IPOConnectionKey
        End Get
        Set(ByVal Value As String)
            _IPOConnectionKey = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the date that the last e-mail was sent.
    ''' </summary>
    Friend Property LastEmailSent() As Date
        Get
            Return _LastEmailSent
        End Get
        Set(ByVal Value As Date)
            _LastEmailSent = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the number of attempts at processing successfully that a single message is given before becoming an exception.
    ''' </summary>
    Friend Property MaxQueueMessageProcessAttempts() As Integer
        Get
            Return _MaxMessageProcessAttempts
        End Get
        Set(ByVal Value As Integer)
            _MaxMessageProcessAttempts = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the number of message processing threads that the service will spawn.
    ''' </summary>
    Friend Property MaxThreads() As Integer
        Get
            Return _MaxThreads
        End Get
        Set(ByVal Value As Integer)
            _MaxThreads = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the process name value.
    ''' </summary>
    Friend Property ProcessMode() As String
        Get
            Return _ProcessMode
        End Get
        Set(ByVal Value As String)
            _ProcessMode = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the optional proxy name value to use when making the web service calls.
    ''' </summary>
    Friend Property ProxyName() As String
        Get
            Return _ProxyName
        End Get
        Set(ByVal Value As String)
            _ProxyName = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the name of the queue. 
    '''     The initial design supports a single value. The full queue pathname
    '''     is set when the QueueServer and QueueName properties are set.
    ''' </summary>
    Friend Property QueueName() As String
        Get
            Return _QueueName
        End Get
        Set(ByVal Value As String)
            _QueueName = Value
            _SetQueuePath()
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the full queue pathname.
    ''' </summary>
    Friend ReadOnly Property QueuePath() As String
        Get
            Return _QueuePath
        End Get
    End Property
    ''' <summary>
    '''     This property sets/returns the number of milliseconds the queue object should 
    '''     wait for a queue message to be received.
    ''' </summary>
    Friend Property QueueRetrieveWait() As Integer
        Get
            Return _QueueRetrieveWait
        End Get
        Set(ByVal Value As Integer)
            _QueueRetrieveWait = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the name of the queue server. 
    '''     The initial design supports a single value. The full queue pathname
    '''     is set when the QueueServer and QueueName properties are set.
    ''' </summary>
    Friend Property QueueServer() As String
        Get
            Return _QueueServer
        End Get
        Set(ByVal Value As String)
            _QueueServer = Value
            _SetQueuePath()
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the data table containing information about the queue.
    '''     The initial design supports a single queue but the db schema supports multiple queues.
    ''' </summary>
    Friend Property QueueTable() As DataTable
        Get
            Return _QueueTable
        End Get
        Set(ByVal Value As DataTable)
            _QueueTable = Value
        End Set
    End Property
    ''' <summary>
    '''     This property contains the data table of Response values.
    ''' </summary>
    Friend Property ResponseValues() As DataTable
        Get
            Return _ResponseValues
        End Get
        Set(ByVal Value As DataTable)
            _ResponseValues = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the number of milliseconds the main thread sleeps when a recoverable error occurs.
    ''' </summary>
    Friend Property SleepWhenError() As Integer
        Get
            Return _SleepWhenError
        End Get
        Set(ByVal Value As Integer)
            _SleepWhenError = Value
        End Set
    End Property
    ''' <summary>
    '''     This property sets/returns the number of milliseconds the main thread sleeps when the queue is empty.
    ''' </summary>
    Friend Property SleepWhenQueueEmpty() As Integer
        Get
            Return _SleepWhenQueueEmpty
        End Get
        Set(ByVal Value As Integer)
            _SleepWhenQueueEmpty = Value
        End Set
    End Property
    ''' <summary>
    '''     This property contains the data table of TranTypes.
    ''' </summary>
    Friend Property TranTypes() As DataTable
        Get
            Return _TranTypes
        End Get
        Set(ByVal Value As DataTable)
            _TranTypes = Value
        End Set
    End Property
    ''' <summary>
    '''     This property determines whether the web service method is called in the context of the service.
    '''     Normally, this should be True. If False, the web service calls will be anonymous.
    ''' </summary>
    Friend Property UseSystemCredentials() As Boolean
        Get
            Return _UseSystemCredentials
        End Get
        Set(ByVal Value As Boolean)
            _UseSystemCredentials = Value
        End Set
    End Property
    ''' <summary>
    '''     Private function that creates the Private Queue pathname from the server and queue names.
    '''     This is called automatically when either the QueueName or QueueServer properties are set.
    ''' </summary>
    Private Sub _SetQueuePath()

        Dim Server As String = _QueueServer
        Dim Queue As String = _QueueName

        If Server <> "" AndAlso Queue <> "" Then

            Server = Replace(Replace(Trim(Server), "PRIVATE$", "", , , CompareMethod.Text), "\", "")
            Queue = Replace(Replace(Trim(Queue), "PRIVATE$", "", , , CompareMethod.Text), "\", "")
            _QueuePath = Server & "\PRIVATE$\" & Queue
        Else
            _QueuePath = ""
        End If

    End Sub

    Public Sub New()

    End Sub
End Class

