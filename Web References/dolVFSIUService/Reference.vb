﻿'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by a tool.
'     Runtime Version:4.0.30319.42000
'
'     Changes to this file may cause incorrect behavior and will be lost if
'     the code is regenerated.
' </auto-generated>
'------------------------------------------------------------------------------

Option Strict Off
Option Explicit On

Imports System
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Web.Services
Imports System.Web.Services.Protocols
Imports System.Xml.Serialization

'
'This source code was auto-generated by Microsoft.VSDesigner, Version 4.0.30319.42000.
'
Namespace dolVFSIUService
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0"),  _
     System.Diagnostics.DebuggerStepThroughAttribute(),  _
     System.ComponentModel.DesignerCategoryAttribute("code"),  _
     System.Web.Services.WebServiceBindingAttribute(Name:="dolVFSIUServiceSoap", [Namespace]:="http://tempuri.org/")>  _
    Partial Public Class dolVFSIUService
        Inherits System.Web.Services.Protocols.SoapHttpClientProtocol
        
        Private CallMMCONFOperationCompleted As System.Threading.SendOrPostCallback
        
        Private CallMMCUPDOperationCompleted As System.Threading.SendOrPostCallback
        
        Private CallMMVDCUOperationCompleted As System.Threading.SendOrPostCallback
        
        Private CallVFSIUEchoOperationCompleted As System.Threading.SendOrPostCallback
        
        Private useDefaultCredentialsSetExplicitly As Boolean
        
        '''<remarks/>
        Public Sub New()
            MyBase.New
            Me.Url = "http://198.7.86.216/Applications/VSD/HP3000/vsdServiceHPDev6/dolVFSIUservice.asmx"& _ 
                ""
            If (Me.IsLocalFileSystemWebService(Me.Url) = true) Then
                Me.UseDefaultCredentials = true
                Me.useDefaultCredentialsSetExplicitly = false
            Else
                Me.useDefaultCredentialsSetExplicitly = true
            End If
        End Sub
        
        Public Shadows Property Url() As String
            Get
                Return MyBase.Url
            End Get
            Set
                If (((Me.IsLocalFileSystemWebService(MyBase.Url) = true)  _
                            AndAlso (Me.useDefaultCredentialsSetExplicitly = false))  _
                            AndAlso (Me.IsLocalFileSystemWebService(value) = false)) Then
                    MyBase.UseDefaultCredentials = false
                End If
                MyBase.Url = value
            End Set
        End Property
        
        Public Shadows Property UseDefaultCredentials() As Boolean
            Get
                Return MyBase.UseDefaultCredentials
            End Get
            Set
                MyBase.UseDefaultCredentials = value
                Me.useDefaultCredentialsSetExplicitly = true
            End Set
        End Property
        
        '''<remarks/>
        Public Event CallMMCONFCompleted As CallMMCONFCompletedEventHandler
        
        '''<remarks/>
        Public Event CallMMCUPDCompleted As CallMMCUPDCompletedEventHandler
        
        '''<remarks/>
        Public Event CallMMVDCUCompleted As CallMMVDCUCompletedEventHandler
        
        '''<remarks/>
        Public Event CallVFSIUEchoCompleted As CallVFSIUEchoCompletedEventHandler
        
        '''<remarks/>
        <System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://tempuri.org/Call-MMCONF", RequestElementName:="Call-MMCONF", RequestNamespace:="http://tempuri.org/", ResponseElementName:="Call-MMCONFResponse", ResponseNamespace:="http://tempuri.org/", Use:=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle:=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)>  _
        Public Function CallMMCONF(<System.Xml.Serialization.XmlElementAttribute("INPUT-VAL")> ByVal INPUTVAL As String) As <System.Xml.Serialization.XmlElementAttribute("Call-MMCONFResult")> String
            Dim results() As Object = Me.Invoke("CallMMCONF", New Object() {INPUTVAL})
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Function BeginCallMMCONF(ByVal INPUTVAL As String, ByVal callback As System.AsyncCallback, ByVal asyncState As Object) As System.IAsyncResult
            Return Me.BeginInvoke("CallMMCONF", New Object() {INPUTVAL}, callback, asyncState)
        End Function
        
        '''<remarks/>
        Public Function EndCallMMCONF(ByVal asyncResult As System.IAsyncResult) As String
            Dim results() As Object = Me.EndInvoke(asyncResult)
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Overloads Sub CallMMCONFAsync(ByVal INPUTVAL As String)
            Me.CallMMCONFAsync(INPUTVAL, Nothing)
        End Sub
        
        '''<remarks/>
        Public Overloads Sub CallMMCONFAsync(ByVal INPUTVAL As String, ByVal userState As Object)
            If (Me.CallMMCONFOperationCompleted Is Nothing) Then
                Me.CallMMCONFOperationCompleted = AddressOf Me.OnCallMMCONFOperationCompleted
            End If
            Me.InvokeAsync("CallMMCONF", New Object() {INPUTVAL}, Me.CallMMCONFOperationCompleted, userState)
        End Sub
        
        Private Sub OnCallMMCONFOperationCompleted(ByVal arg As Object)
            If (Not (Me.CallMMCONFCompletedEvent) Is Nothing) Then
                Dim invokeArgs As System.Web.Services.Protocols.InvokeCompletedEventArgs = CType(arg,System.Web.Services.Protocols.InvokeCompletedEventArgs)
                RaiseEvent CallMMCONFCompleted(Me, New CallMMCONFCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState))
            End If
        End Sub
        
        '''<remarks/>
        <System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://tempuri.org/Call-MMCUPD", RequestElementName:="Call-MMCUPD", RequestNamespace:="http://tempuri.org/", ResponseElementName:="Call-MMCUPDResponse", ResponseNamespace:="http://tempuri.org/", Use:=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle:=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)>  _
        Public Function CallMMCUPD(<System.Xml.Serialization.XmlElementAttribute("INPUT-VAL")> ByVal INPUTVAL As String) As <System.Xml.Serialization.XmlElementAttribute("Call-MMCUPDResult")> String
            Dim results() As Object = Me.Invoke("CallMMCUPD", New Object() {INPUTVAL})
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Function BeginCallMMCUPD(ByVal INPUTVAL As String, ByVal callback As System.AsyncCallback, ByVal asyncState As Object) As System.IAsyncResult
            Return Me.BeginInvoke("CallMMCUPD", New Object() {INPUTVAL}, callback, asyncState)
        End Function
        
        '''<remarks/>
        Public Function EndCallMMCUPD(ByVal asyncResult As System.IAsyncResult) As String
            Dim results() As Object = Me.EndInvoke(asyncResult)
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Overloads Sub CallMMCUPDAsync(ByVal INPUTVAL As String)
            Me.CallMMCUPDAsync(INPUTVAL, Nothing)
        End Sub
        
        '''<remarks/>
        Public Overloads Sub CallMMCUPDAsync(ByVal INPUTVAL As String, ByVal userState As Object)
            If (Me.CallMMCUPDOperationCompleted Is Nothing) Then
                Me.CallMMCUPDOperationCompleted = AddressOf Me.OnCallMMCUPDOperationCompleted
            End If
            Me.InvokeAsync("CallMMCUPD", New Object() {INPUTVAL}, Me.CallMMCUPDOperationCompleted, userState)
        End Sub
        
        Private Sub OnCallMMCUPDOperationCompleted(ByVal arg As Object)
            If (Not (Me.CallMMCUPDCompletedEvent) Is Nothing) Then
                Dim invokeArgs As System.Web.Services.Protocols.InvokeCompletedEventArgs = CType(arg,System.Web.Services.Protocols.InvokeCompletedEventArgs)
                RaiseEvent CallMMCUPDCompleted(Me, New CallMMCUPDCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState))
            End If
        End Sub
        
        '''<remarks/>
        <System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://tempuri.org/Call-MMVDCU", RequestElementName:="Call-MMVDCU", RequestNamespace:="http://tempuri.org/", ResponseElementName:="Call-MMVDCUResponse", ResponseNamespace:="http://tempuri.org/", Use:=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle:=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)>  _
        Public Function CallMMVDCU(<System.Xml.Serialization.XmlElementAttribute("INPUT-VAL")> ByVal INPUTVAL As String) As <System.Xml.Serialization.XmlElementAttribute("Call-MMVDCUResult")> String
            Dim results() As Object = Me.Invoke("CallMMVDCU", New Object() {INPUTVAL})
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Function BeginCallMMVDCU(ByVal INPUTVAL As String, ByVal callback As System.AsyncCallback, ByVal asyncState As Object) As System.IAsyncResult
            Return Me.BeginInvoke("CallMMVDCU", New Object() {INPUTVAL}, callback, asyncState)
        End Function
        
        '''<remarks/>
        Public Function EndCallMMVDCU(ByVal asyncResult As System.IAsyncResult) As String
            Dim results() As Object = Me.EndInvoke(asyncResult)
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Overloads Sub CallMMVDCUAsync(ByVal INPUTVAL As String)
            Me.CallMMVDCUAsync(INPUTVAL, Nothing)
        End Sub
        
        '''<remarks/>
        Public Overloads Sub CallMMVDCUAsync(ByVal INPUTVAL As String, ByVal userState As Object)
            If (Me.CallMMVDCUOperationCompleted Is Nothing) Then
                Me.CallMMVDCUOperationCompleted = AddressOf Me.OnCallMMVDCUOperationCompleted
            End If
            Me.InvokeAsync("CallMMVDCU", New Object() {INPUTVAL}, Me.CallMMVDCUOperationCompleted, userState)
        End Sub
        
        Private Sub OnCallMMVDCUOperationCompleted(ByVal arg As Object)
            If (Not (Me.CallMMVDCUCompletedEvent) Is Nothing) Then
                Dim invokeArgs As System.Web.Services.Protocols.InvokeCompletedEventArgs = CType(arg,System.Web.Services.Protocols.InvokeCompletedEventArgs)
                RaiseEvent CallMMVDCUCompleted(Me, New CallMMVDCUCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState))
            End If
        End Sub
        
        '''<remarks/>
        <System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://tempuri.org/Call-VFSIUEcho", RequestElementName:="Call-VFSIUEcho", RequestNamespace:="http://tempuri.org/", ResponseElementName:="Call-VFSIUEchoResponse", ResponseNamespace:="http://tempuri.org/", Use:=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle:=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)>  _
        Public Function CallVFSIUEcho(<System.Xml.Serialization.XmlElementAttribute("INPUT-VAL")> ByVal INPUTVAL As String) As <System.Xml.Serialization.XmlElementAttribute("Call-VFSIUEchoResult")> String
            Dim results() As Object = Me.Invoke("CallVFSIUEcho", New Object() {INPUTVAL})
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Function BeginCallVFSIUEcho(ByVal INPUTVAL As String, ByVal callback As System.AsyncCallback, ByVal asyncState As Object) As System.IAsyncResult
            Return Me.BeginInvoke("CallVFSIUEcho", New Object() {INPUTVAL}, callback, asyncState)
        End Function
        
        '''<remarks/>
        Public Function EndCallVFSIUEcho(ByVal asyncResult As System.IAsyncResult) As String
            Dim results() As Object = Me.EndInvoke(asyncResult)
            Return CType(results(0),String)
        End Function
        
        '''<remarks/>
        Public Overloads Sub CallVFSIUEchoAsync(ByVal INPUTVAL As String)
            Me.CallVFSIUEchoAsync(INPUTVAL, Nothing)
        End Sub
        
        '''<remarks/>
        Public Overloads Sub CallVFSIUEchoAsync(ByVal INPUTVAL As String, ByVal userState As Object)
            If (Me.CallVFSIUEchoOperationCompleted Is Nothing) Then
                Me.CallVFSIUEchoOperationCompleted = AddressOf Me.OnCallVFSIUEchoOperationCompleted
            End If
            Me.InvokeAsync("CallVFSIUEcho", New Object() {INPUTVAL}, Me.CallVFSIUEchoOperationCompleted, userState)
        End Sub
        
        Private Sub OnCallVFSIUEchoOperationCompleted(ByVal arg As Object)
            If (Not (Me.CallVFSIUEchoCompletedEvent) Is Nothing) Then
                Dim invokeArgs As System.Web.Services.Protocols.InvokeCompletedEventArgs = CType(arg,System.Web.Services.Protocols.InvokeCompletedEventArgs)
                RaiseEvent CallVFSIUEchoCompleted(Me, New CallVFSIUEchoCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState))
            End If
        End Sub
        
        '''<remarks/>
        Public Shadows Sub CancelAsync(ByVal userState As Object)
            MyBase.CancelAsync(userState)
        End Sub
        
        Private Function IsLocalFileSystemWebService(ByVal url As String) As Boolean
            If ((url Is Nothing)  _
                        OrElse (url Is String.Empty)) Then
                Return false
            End If
            Dim wsUri As System.Uri = New System.Uri(url)
            If ((wsUri.Port >= 1024)  _
                        AndAlso (String.Compare(wsUri.Host, "localHost", System.StringComparison.OrdinalIgnoreCase) = 0)) Then
                Return true
            End If
            Return false
        End Function
    End Class
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0")>  _
    Public Delegate Sub CallMMCONFCompletedEventHandler(ByVal sender As Object, ByVal e As CallMMCONFCompletedEventArgs)
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0"),  _
     System.Diagnostics.DebuggerStepThroughAttribute(),  _
     System.ComponentModel.DesignerCategoryAttribute("code")>  _
    Partial Public Class CallMMCONFCompletedEventArgs
        Inherits System.ComponentModel.AsyncCompletedEventArgs
        
        Private results() As Object
        
        Friend Sub New(ByVal results() As Object, ByVal exception As System.Exception, ByVal cancelled As Boolean, ByVal userState As Object)
            MyBase.New(exception, cancelled, userState)
            Me.results = results
        End Sub
        
        '''<remarks/>
        Public ReadOnly Property Result() As String
            Get
                Me.RaiseExceptionIfNecessary
                Return CType(Me.results(0),String)
            End Get
        End Property
    End Class
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0")>  _
    Public Delegate Sub CallMMCUPDCompletedEventHandler(ByVal sender As Object, ByVal e As CallMMCUPDCompletedEventArgs)
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0"),  _
     System.Diagnostics.DebuggerStepThroughAttribute(),  _
     System.ComponentModel.DesignerCategoryAttribute("code")>  _
    Partial Public Class CallMMCUPDCompletedEventArgs
        Inherits System.ComponentModel.AsyncCompletedEventArgs
        
        Private results() As Object
        
        Friend Sub New(ByVal results() As Object, ByVal exception As System.Exception, ByVal cancelled As Boolean, ByVal userState As Object)
            MyBase.New(exception, cancelled, userState)
            Me.results = results
        End Sub
        
        '''<remarks/>
        Public ReadOnly Property Result() As String
            Get
                Me.RaiseExceptionIfNecessary
                Return CType(Me.results(0),String)
            End Get
        End Property
    End Class
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0")>  _
    Public Delegate Sub CallMMVDCUCompletedEventHandler(ByVal sender As Object, ByVal e As CallMMVDCUCompletedEventArgs)
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0"),  _
     System.Diagnostics.DebuggerStepThroughAttribute(),  _
     System.ComponentModel.DesignerCategoryAttribute("code")>  _
    Partial Public Class CallMMVDCUCompletedEventArgs
        Inherits System.ComponentModel.AsyncCompletedEventArgs
        
        Private results() As Object
        
        Friend Sub New(ByVal results() As Object, ByVal exception As System.Exception, ByVal cancelled As Boolean, ByVal userState As Object)
            MyBase.New(exception, cancelled, userState)
            Me.results = results
        End Sub
        
        '''<remarks/>
        Public ReadOnly Property Result() As String
            Get
                Me.RaiseExceptionIfNecessary
                Return CType(Me.results(0),String)
            End Get
        End Property
    End Class
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0")>  _
    Public Delegate Sub CallVFSIUEchoCompletedEventHandler(ByVal sender As Object, ByVal e As CallVFSIUEchoCompletedEventArgs)
    
    '''<remarks/>
    <System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "4.6.1586.0"),  _
     System.Diagnostics.DebuggerStepThroughAttribute(),  _
     System.ComponentModel.DesignerCategoryAttribute("code")>  _
    Partial Public Class CallVFSIUEchoCompletedEventArgs
        Inherits System.ComponentModel.AsyncCompletedEventArgs
        
        Private results() As Object
        
        Friend Sub New(ByVal results() As Object, ByVal exception As System.Exception, ByVal cancelled As Boolean, ByVal userState As Object)
            MyBase.New(exception, cancelled, userState)
            Me.results = results
        End Sub
        
        '''<remarks/>
        Public ReadOnly Property Result() As String
            Get
                Me.RaiseExceptionIfNecessary
                Return CType(Me.results(0),String)
            End Get
        End Property
    End Class
End Namespace
