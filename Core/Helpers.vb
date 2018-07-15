' =======================================================================================
'
'   This file is part of neth-proxy.
'
'   neth-proxy is free software: you can redistribute it and/or modify
'   it under the terms Of the GNU General Public License As published by
'   the Free Software Foundation, either version 3 Of the License, Or
'   (at your option) any later version.
'
'   neth-proxy is distributed In the hope that it will be useful,
'   but WITHOUT ANY WARRANTY; without even the implied warranty Of
'   MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
'   GNU General Public License For more details.
'
'   You should have received a copy Of the GNU General Public License
'   along with neth-proxy.  If not, see < http://www.gnu.org/licenses/ >.
'
' =======================================================================================

Imports System.Net
Imports System.Numerics
Imports System.Json
Imports System.Text.RegularExpressions

Namespace Core

    Partial Public Module Helpers

#Region " Constants"

        ''' <summary>
        ''' Defines the default size for send/receive buffers
        ''' </summary>
        Public Const DEFAULT_BUFFER_SIZE As Integer = 256

        ''' <summary>
        ''' Defines the default number of sockets to add
        ''' to the stack when no available
        ''' </summary>
        Public Const DEFAULT_SOCKET_STACK_INCREASE As Integer = 16

        ''' <summary>
        ''' Sets the max backlog for pending connections
        ''' </summary>
        Public Const DEFAULT_MAX_CONNECTIONS As Integer = 32000

#End Region

#Region " Enums"

        ''' <summary>
        ''' Statuses of async TCP connections 
        ''' </summary>
        <Flags>
        Public Enum AsyncSocketStatus

            NotConnected = 1 << 0
            Connecting = NotConnected << 1
            Connected = Connecting << 1
            Disconnecting = Connected << 1

        End Enum

        ''' <summary>
        ''' Status of Pool connection (application level)
        ''' </summary>
        <Flags>
        Public Enum PoolStatus

            NotConnected = 1 << 0
            Connected = NotConnected << 1
            Subscribed = Connected << 1
            Authorized = Subscribed << 1

        End Enum

        ''' <summary>
        ''' Status of Client connection (application level)
        ''' </summary>
        <Flags>
        Public Enum ClientStatus

            NotConnected = 1 << 0
            Connected = NotConnected << 1
            Subscribed = Connected << 1
            Authorized = Subscribed << 1
            ApiAvailable = Authorized << 1
            ApiConnected = ApiAvailable << 1

        End Enum

        ''' <summary>
        ''' Enumeration of stratum modes
        ''' </summary>
        Public Enum StratumModeEnum

            Undefined
            TentativeStratum
            TentativeEthProxy
            Stratum
            Ethproxy

        End Enum

        ''' <summary>
        ''' Define Application Exit Codes
        ''' </summary>
        Public Enum ExitCodes

            Success = 0
            ArgumentsError = 1

        End Enum

#End Region

        ''' <summary>
        ''' Returns a human readable value for hashes scaled to the
        ''' next unit which is lower than 10^3
        ''' </summary>
        ''' <param name="hashSample"></param>
        ''' <returns>A string</returns>
        Public Function ScaleHashes(ByVal hashSample As Double) As String

            Static units As String() = {"h", "Kh", "Mh", "Gh", "Th", "Ph"}
            Static unitIdx As Integer = 0

            unitIdx = 0
            Do
                If hashSample < 1000 OrElse unitIdx = units.Length - 1 Then
                    Return String.Format("{0:N2} {1}", hashSample, units(unitIdx))
                End If
                hashSample = hashSample / 1000
                unitIdx += 1
            Loop

        End Function

        ''' <summary>
        ''' Gets Developer Donation addresses
        ''' </summary>
        ''' <returns></returns>
        Public Function GetFeeAddresses() As Dictionary(Of String, String)
            Static retvar As Dictionary(Of String, String) = New Dictionary(Of String, String) From {
                {"eth", "0x9E431042fAA3224837e9BEDEcc5F4858cf0390B9"},
                {"etc", "0x6e4Aa5064ced1c0e9E20A517B9d7A7dDe32A0dcf"}
            }
            Return retvar
        End Function

        ''' <summary>
        ''' Gets the DevFeeAddress
        ''' </summary>
        ''' <param name="fromPool"></param>
        ''' <returns></returns>
        Public Function GetDevAddress(fromHost As String) As String

            Dim PoolName As String = String.Empty
            Dim PoolPort As Integer = 0

            If String.IsNullOrEmpty(fromHost) Then Return String.Empty
            If fromHost.IndexOf(":", 0) < 0 Then Return String.Empty

            PoolName = fromHost.Split(":", StringSplitOptions.RemoveEmptyEntries)(0)
            PoolPort = CInt(fromHost.Split(":", StringSplitOptions.RemoveEmptyEntries)(1))


            Select Case True

                Case PoolName.EndsWith("2miners.com")

                    Dim dashPos As Integer = PoolName.LastIndexOf("-", 0)
                    Dim coinTicker As String = PoolName.Substring(dashPos + 1)
                    Try
                        Return GetFeeAddresses(coinTicker)
                    Catch ex As Exception
                        Return String.Empty
                    End Try

                Case PoolName.EndsWith("dwarfpool.com")

                    Dim dashPos As Integer = PoolName.IndexOf("-", 0)
                    If dashPos > 2 Then
                        Try
                            Dim coinTicker As String = PoolName.Substring(0, dashPos)
                            Return GetFeeAddresses(coinTicker)
                        Catch ex As Exception
                            Return String.Empty
                        End Try
                    Else
                        Return String.Empty
                    End If

                Case PoolName.EndsWith("ethermine.org")

                    If PoolName.Split(".", StringSplitOptions.RemoveEmptyEntries)(0).EndsWith("etc") Then
                        Return GetFeeAddresses("etc")
                    End If
                    Return GetFeeAddresses("eth")

                Case PoolName.EndsWith("ethpool.org")

                    Return GetFeeAddresses("eth")

                Case PoolName.EndsWith("f2pool.com")

                    Dim coinTicker As String = PoolName.Split(".", StringSplitOptions.RemoveEmptyEntries)(0)
                    Try
                        Return GetFeeAddresses(coinTicker)
                    Catch ex As Exception
                        Return String.Empty
                    End Try

                Case PoolName.EndsWith("miningpoolhub.com")

                    Select Case PoolPort
                        Case 20535
                            Return GetFeeAddresses("eth")
                        Case 20555
                            Return GetFeeAddresses("etc")
                    End Select

                Case PoolName.EndsWith("nanopool.org")

                    Dim dashPos As Integer = PoolName.IndexOf("-", 0)
                    If dashPos > 2 Then
                        Try
                            Dim coinTicker As String = PoolName.Substring(0, dashPos)
                            Return GetFeeAddresses(coinTicker)
                        Catch ex As Exception
                            Return String.Empty
                        End Try
                    Else
                        Return String.Empty
                    End If

                Case PoolName.EndsWith("sparkpool.com")

                    Return GetFeeAddresses("eth")

            End Select

            Return String.Empty

        End Function

        ''' <summary>
        ''' Calculates difficulty
        ''' </summary>
        ''' <param name="inputHex"></param>
        ''' <returns></returns>
        Public Function GetDiffToTarget(ByVal inputHex As String) As Double

            Static diffDividend As BigInteger = BigInteger.Zero
            If diffDividend = BigInteger.Zero Then diffDividend = BigInteger.Parse("10000000000000000000000000000000000000000000000000000000000000000", Globalization.NumberStyles.AllowHexSpecifier, Nothing)

            Dim diffDivisor As BigInteger = BigInteger.Zero
            If inputHex.StartsWith("0x") Then
                inputHex = inputHex.Substring(2)
            End If
            If BigInteger.TryParse(inputHex, Globalization.NumberStyles.AllowHexSpecifier, Nothing, diffDivisor) Then
                Return (diffDividend / diffDivisor)
            End If
            Return 0

        End Function

        ''' <summary>
        ''' Transforms any hexadecimal string into a fixed length 66 chars
        ''' </summary>
        ''' <param name="value">A hexadecimal string</param>
        ''' <returns></returns>
        Public Function H66(value As String) As String

            Dim retVar As BigInteger = BigInteger.Zero
            If value.StartsWith("0x") Then
                value = value.Substring(3)
            End If
            If BigInteger.TryParse(value, Globalization.NumberStyles.AllowHexSpecifier, Nothing, retVar) Then
                Return String.Format("0x{0:x64}", retVar)
            End If
            Return String.Empty

        End Function

        ''' <summary>
        ''' Returns an array of integers from a comma separated string.
        ''' </summary>
        ''' <param name="stringPorts"></param>
        ''' <returns></returns>
        Public Function PortsFromString(stringPorts As String) As Integer()

            If stringPorts = String.Empty Then Return Nothing
            Dim stringValues As String() = stringPorts.Split(",")
            Dim intValues(stringValues.Length) As Integer
            For i As Integer = 0 To stringValues.Length - 1
                intValues(i) = Integer.Parse(stringValues(i))
            Next
            Return intValues

        End Function

        ''' <summary>
        ''' Whether or not the given name is a valid HostName
        ''' </summary>
        ''' <param name="inputString">The string to check</param>
        ''' <returns></returns>
        Public Function IsValidHostName(inputString As String) As Boolean

            Static rgxValidHostname As Regex = Nothing
            If rgxValidHostname Is Nothing Then rgxValidHostname = New Regex("^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z]|[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9])$", RegexOptions.IgnoreCase)
            Return rgxValidHostname.IsMatch(inputString)

        End Function

        ''' <summary>
        ''' Returns wether or not the inputString matches a valid hexadecimal of 64 bytes
        ''' </summary>
        ''' <param name="inputString">The string to be checked</param>
        ''' <returns>True or False</returns>
        Public Function IsHex64(inputString As String) As Boolean

            Static rgxValidHex64 As New Regex("^([a-f0-9]{64})$", RegexOptions.IgnoreCase)
            Return rgxValidHex64.IsMatch(inputString)

        End Function

        ''' <summary>
        ''' Gets wether or not the given input string is an IpV4 address
        ''' </summary>
        ''' <param name="input">The string to be checked</param>
        ''' <returns></returns>
        Public Function IsIPAddressV4(input As String) As Boolean

            Dim _ipAddress As IPAddress = Nothing
            If IPAddress.TryParse(input, _ipAddress) Then
                Return _ipAddress.AddressFamily = Net.Sockets.AddressFamily.InterNetwork
            End If
            Return False

        End Function

        ''' <summary>
        ''' Gets wether or not the given input string is an IpV4 address
        ''' </summary>
        ''' <param name="input">The string to be checked</param>
        ''' <returns></returns>
        Public Function IsIPAddressV6(input As String) As Boolean

            Dim _ipAddress As IPAddress = Nothing
            If IPAddress.TryParse(input, _ipAddress) Then
                Return _ipAddress.AddressFamily = Net.Sockets.AddressFamily.InterNetworkV6
            End If
            Return False

        End Function

        ''' <summary>
        ''' Creates an IPEndPoint from given address of host and port
        ''' </summary>
        ''' <param name="host">A string for an hostname or ip address</param>
        ''' <param name="port">The port number for the socket</param>
        ''' <returns>An <see cref="IPEndPoint"/> object</returns>
        Public Function CreateIPEndPoint(host As String, port As Integer) As IPEndPoint

            Dim _ipAddress As IPAddress = Nothing

            ' Prevent resolve if not needed
            If IsIPAddressV4(host) OrElse IsIPAddressV6(host) Then

                _ipAddress = IPAddress.Parse(host)

            Else

                Dim hostInfo As IPHostEntry = Dns.GetHostEntry(host)
                _ipAddress = hostInfo.AddressList(0)

            End If

            Return New IPEndPoint(_ipAddress, port)


        End Function


        ''' <summary>
        ''' Returns a pseudo-random value among the given range
        ''' </summary>
        Public Function GetRandom(ByVal minValue As Integer, ByVal maxValue As Integer) As Integer

            ' Static prevents same seed generation
            Static rndGenerator As Random = New Random()
            Return rndGenerator.Next(minValue, maxValue)

        End Function

        ''' <summary>
        ''' App title
        ''' </summary>
        ''' <returns>A string</returns>
        Public Function GetTitle() As String

            Static retVar As String = "
  _  _  ____  ____  _   _       ____  ____  _____  _  _  _  _ 
 ( \( )( ___)(_  _)( )_( ) ___ (  _ \(  _ \(  _  )( \/ )( \/ )
  )  (  )__)   )(   ) _ ( (___) )___/ )   / )(_)(  )  (  \  / 
 (_)\_)(____) (__) (_) (_)     (__)  (_)\_)(_____)(_/\_) (__) 

  Yet another stratum proxy proudly written for .Net Core
  Release : " + GetType(Program).Assembly.GetName().Version.ToString() + ChrW(10)

            Return retVar

        End Function

        ''' <summary>
        ''' Help text
        ''' </summary>
        ''' <returns></returns>
        Public Function GetHelpText() As String

            Static retVar As String = "
Usage : dotnet neth-proxy.dll <options>

Where <options> are : (switches among square brackets are optional)

   -b | --bind [<localaddress>:]<portnumber>
  -ab | --api-bind [<localaddress>:]<portnumber>
  -sp | --stratum-pool [<authid>][:<password>][.<workername>]@<hostname-or-ipaddress>:<portnumber>[,<portnumber>]
 [-np | --no-probe ]
 [-wt | --work-timeout <numseconds> ]
 [-rt | --response-timeout <milliseconds>]
 [-rh | --report-hashrate ]
 [-rw | --report-workkers ]
 [-ws | --workers-spacing ]
 [-ns | --no-stats]
 [-si | --stats-interval <numseconds>]
 [-nc | --no-console]
 [-nf | --no-fee]
 [-ll | --log-level <0-9>]
  [-h | --help ]

Description of arguments
-----------------------------------------------------------------------------------------------------------------------
-b  | --bind              Sets the LOCAL address this proxy has to listen for incoming connections. 
                          Default is any local address port 4444
-ab | --api-bind          Sets the LOCAL address this proxy has to listen for incoming connections on API interface.
                          Default is not enabled.
-sp | --stratum-pool      Is the connection to the target pool this proxy has to forward workers
-np | --no-probe          By default before connection to the pool each ip address bound to the hostname is pinged to determine
                          which responds faster. If you do not want to probe all host's ip then set this switch
-wt | --work-timeout      Sets the number of seconds within each new work from the pool must come in. If no work within this number
                          of seconds the proxy disconnects and reconnects to next ip or next pool. Default is 120 seconds
-rt | --response-timeout  Sets the time (in milliseconds) the pool should reply to a submission request. Should the response
                          exceed this amount of time then proxy will reconnect to other ip or other pool.
                          Default is 2000 (2 seconds)
-rh | --report-hashrate   Submit hashrate to pool for each workername. Implies --report-workers
-rw | --report-workers    Forward separate workernames to pool
-ws | --workers-spacing   Sets the exponent in the power of 2 which expresses the spacing among workers segments
                          Default is 24 which means 2^24 nonces will be the minimum space among workers segments
-si | --stats-interval    Sets the interval for stats printout. Default is 60 seconds. Min is 10 seconds. Set it to 0 to
                          disable stats printout completely.
-nc | --no-console        Prevents reading from console so you can launch neth-proxy with output redirection to file
-nf | --no-fee            Disables developer fee (0.75%). I will loose all my revenues but proxy won't do some optimization tasks.
-ll | --log-level         Sets log verbosity 0-9. Default is 4
-h  | --help              Prints this help message

How to connect your ethminer's instances to this proxy
-----------------------------------------------------------------------------------------------------------------------
ethminer 0.15.rc2 is minimum version required with API support enabled

ethminer -P stratum+tcp://<neth-proxy-ipaddress>:<neth-proxy-bindport>/<workername>/<nnnn> --api-port <nnnn>

where <nnnn> is the API port ethminer is listening on

"
            Return retVar


        End Function

        Public Async Function WaitNetworkAvailable() As Task

            ' Check available ping against Google public DNS
            Dim pingQry As NetworkInformation.Ping = New NetworkInformation.Ping()
            Dim pingRep As NetworkInformation.PingReply
            Dim googleAddress As IPAddress = IPAddress.Parse("8.8.8.8")

            Do
                Try
                    pingRep = Await pingQry.SendPingAsync(address:=googleAddress, timeout:=1000)
                    Return
                Catch ex As Exception
                    Logger.Log(0, "Internet not available. Waiting 3 seconds ...")
                    Threading.Thread.Sleep(3000)
                End Try
            Loop

        End Function

#Region " Json Helpers"

        Public Function NewJsonRpc() As JsonObject

            Return New JsonObject From {
                New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0")
                }

        End Function

        Public Function NewJsonRpc(id As Integer) As JsonObject

            Return New JsonObject From {
                New KeyValuePair(Of String, JsonValue)("id", id),
                New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0")
                }

        End Function

        Public Function NewJsonRpc(id As Integer, method As String) As JsonObject

            Return New JsonObject From {
                New KeyValuePair(Of String, JsonValue)("id", id),
                New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
                New KeyValuePair(Of String, JsonValue)("method", method)
                }

        End Function

        Public Function NewJsonRpcResOk(id As Integer)

            Return New JsonObject From {
            New KeyValuePair(Of String, JsonValue)("id", id),
            New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
            New KeyValuePair(Of String, JsonValue)("result", True)
            }

        End Function

        Public Function NewJsonRpcResErr(ByVal id As Long, ByVal errText As String) As Json.JsonObject

            Return New JsonObject From {
            New KeyValuePair(Of String, JsonValue)("id", id),
            New KeyValuePair(Of String, JsonValue)("jsonrpc", "2.0"),
            New KeyValuePair(Of String, JsonValue)("result", False),
            New KeyValuePair(Of String, JsonValue)("error", errText)
            }

        End Function

#End Region

    End Module

End Namespace
