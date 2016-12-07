Public Class Stats
    Private NextWrite As Date = Now.AddHours(1)
    Private Stats As New Hashtable
        ''' <summary>
        '''     Increments a tran type's counter
        ''' </summary>
        ''' <remarks>
        '''     This should be called within SyncLock for accuracy
        ''' </remarks>
    Friend Sub Increment(ByVal TranType As String)
        'ignore errors in case someone passes us junk
        On Error Resume Next
        Stats(TranType) = CType(Stats(TranType), Integer) + 1
    End Sub
    ''' <summary>
    '''     Once per hour (unless restarted), this will return a comma delimited string for the stats log. It then resets the timer and counters.
    '''     If the method has been called within the last hour, an empty string is returned.
    ''' </summary>
    ''' <remarks>
    '''     This should be called within SyncLock for accuracy
    ''' </remarks>
    Friend Function LogString() As String

        Dim strReturnValue As String = "" 'return string
        Dim en As IDictionaryEnumerator = Stats.GetEnumerator ' enumerator for building the string
        Dim i As Integer = 0 'index for clearing values
        Dim Keys(Stats.Keys.Count - 1) As String 'collection of key names

        If Now <= NextWrite Then
            'return an empty string - we shouldn't log anything yet
            Return strReturnValue
        End If

        'if here, it is time to return a string to log
        'reset the log time time
        NextWrite = Now.AddHours(1)
        strReturnValue = "Stats," 'comma delimited stats string

        'report the stats
        While en.MoveNext
            strReturnValue &= en.Key & "=" & en.Value.ToString & "," 'string to return
            Keys(i) = en.Key 'collection of key names so we can reset the values
            i += 1 'increment the counter for our key name collection
        End While

        'remove any trailing commas
        If Right(strReturnValue, 1) = "," Then
            strReturnValue = Left(strReturnValue, Len(strReturnValue) - 1)
        End If

        'reset the counts
        For i = 0 To Stats.Count - 1
            'reset the count (note - this can't be done within the first pass
            'because changing the value causes an "Collection was modified; enumeration operation may not execute" error.
            'Work arounds from MS includes an "isolation enumerator" class 
            'http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp01212002.asp
            'but that is a lot of work for a collection w/ ~ 1/2 dozen items.
            'So, use the brute force method.
            Stats(Keys(i)) = 0
        Next

        'return the fruits of our labor
        Return strReturnValue
    End Function

    Public Sub New(ByVal TranTypes As DataTable)
        'initialize the hash table
        Dim r As DataRow
        Stats = New Hashtable
        For Each r In TranTypes.Rows
            Stats.Add(CType(r("TranType"), String), 0)
        Next
    End Sub
End Class
