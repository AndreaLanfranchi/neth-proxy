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

Imports nethproxy.Core
Imports System.Runtime.Loader
Imports System.Json
Imports System.Text
Imports System.Threading

Module Program

    Dim signalReceived As New ManualResetEventSlim()
    Dim terminated As New ManualResetEventSlim()

    <MTAThread()>
    Sub Main(args As String())


        App.Instance.Init()
        Console.OutputEncoding = Encoding.ASCII
        Console.Out.WriteLine(Core.Helpers.GetTitle)

        Dim culture As Globalization.CultureInfo = Globalization.CultureInfo.CreateSpecificCulture("en-US")
        Globalization.CultureInfo.DefaultThreadCurrentCulture = culture
        Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture

        Dim argsErr As New Queue(Of String)

        ' -----------------------------------------
        ' Parse arguments
        ' -----------------------------------------
        If args.Length > 0 Then
            Dim argIdx As Integer = 0
            Do
                Select Case args(argIdx).ToLower

                    Case "-ll", "--log-level"

                        argIdx += 1
                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for log level specification", args(argIdx)))
                            Continue Do
                        End If

                        Dim intLevel As Integer = 0
                        If Integer.TryParse(args(argIdx), intLevel) Then
                            If intLevel >= 2 AndAlso intLevel <= 9 Then
                                App.Instance.Settings.LogVerbosity = intLevel
                            End If
                        Else
                            argsErr.Enqueue(String.Format("Error : Log level specification {0} is invalid", args(argIdx)))
                        End If



                    Case "-b", "--bind"

                        argIdx += 1
                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for binding specification", args(argIdx)))
                            Continue Do
                        End If

                        ' Ip address and port to bind to
                        ' Only possible form is <ipaddress>:<portnumber>

                        If Not args(argIdx).IndexOf(":") < 0 Then

                            Dim pIpAddress As String = String.Empty
                            Dim pPort As String = String.Empty
                            Try
                                pIpAddress = args(argIdx).Split(":")(0)
                                pPort = args(argIdx).Split(":")(1)
                            Catch ex As Exception
                            End Try

                            Dim tmpIpaddress As Net.IPAddress = Nothing
                            Dim tmpPortNumber As Integer

                            If (pIpAddress = String.Empty OrElse Net.IPAddress.TryParse(pIpAddress, tmpIpaddress) = False) OrElse
                           (pPort = String.Empty OrElse Integer.TryParse(pPort, tmpPortNumber) = False) Then
                                argsErr.Enqueue(String.Format("Error : Binding address {0} is invalid", args(argIdx)))
                            Else
                                App.Instance.Settings.ListenerEndPoint = New Net.IPEndPoint(tmpIpaddress, tmpPortNumber)
                            End If

                        Else

                            Dim pPort As String = args(argIdx)
                            Dim tmpIpaddress As Net.IPAddress = Net.IPAddress.Any
                            Dim tmpPortNumber As Integer

                            If (pPort = String.Empty OrElse Integer.TryParse(pPort, tmpPortNumber) = False) Then
                                argsErr.Enqueue(String.Format("Error : Api Binding Address {0} is invalid", args(argIdx)))
                            Else
                                App.Instance.Settings.ListenerEndPoint = New Net.IPEndPoint(tmpIpaddress, tmpPortNumber)
                            End If

                        End If

                    Case "-ab", "--api-bind"

                        argIdx += 1
                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for api binding specification", args(argIdx)))
                            Continue Do
                        End If

                        ' Ip address and port to bind api server to
                        ' Only possible form is <ipaddress>:<portnumber>

                        If Not args(argIdx).IndexOf(":") < 0 Then

                            Dim pIpAddress As String = String.Empty
                            Dim pPort As String = String.Empty
                            Try
                                pIpAddress = args(argIdx).Split(":")(0)
                                pPort = args(argIdx).Split(":")(1)
                            Catch ex As Exception
                            End Try

                            Dim tmpIpaddress As Net.IPAddress = Nothing
                            Dim tmpPortNumber As Integer

                            If (pIpAddress = String.Empty OrElse Net.IPAddress.TryParse(pIpAddress, tmpIpaddress) = False) OrElse
                                (pPort = String.Empty OrElse Integer.TryParse(pPort, tmpPortNumber) = False) Then
                                argsErr.Enqueue(String.Format("Error : Api Binding Address {0} is invalid", args(argIdx)))
                            Else
                                App.Instance.Settings.ApiListenerEndPoint = New Net.IPEndPoint(tmpIpaddress, tmpPortNumber)
                            End If

                        Else

                            Dim pPort As String = args(argIdx)
                            Dim tmpIpaddress As Net.IPAddress = Net.IPAddress.Any
                            Dim tmpPortNumber As Integer

                            If (pPort = String.Empty OrElse Integer.TryParse(pPort, tmpPortNumber) = False) Then
                                argsErr.Enqueue(String.Format("Error : Api Binding Address {0} is invalid", args(argIdx)))
                            Else
                                App.Instance.Settings.ApiListenerEndPoint = New Net.IPEndPoint(tmpIpaddress, tmpPortNumber)
                            End If

                        End If


                    Case "-sp", "--stratum-pool"

                        argIdx += 1
                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for stratum pool specification", args(argIdx)))
                            Continue Do
                        End If
                        Dim argValue As String = args(argIdx).ToLower

                        ' Possible cases
                        ' Only pool in the form <host-or-ip-address>:<portnumber>
                        ' Pool with specific authentication and generic password "x" : <authenticationid>@<host-or-ip-address>:<portnumbers>
                        ' Pool with specific authentication and specific password  : <authenticationid>:<password>@<host-or-ip-address>:<portnumbers>
                        ' Pool with specific authentication and workername and specific password  : <authenticationid>.<workername>:<password>@<host-or-ip-address>:<portnumbers>
                        ' Pool with specific authentication and workername : <authenticationid>.<workername>@<host-or-ip-address>:<portnumbers>

                        Dim patterns As String() = {
                            "^(?<host>[\w\.\-]{3,})\:(?<ports>[\d,]{1,})$",
                            "^(?<stratumlogin>\w{1,})\@(?<host>[\w\.\-]{3,})\:(?<ports>[\d,]{1,})$",
                            "^(?<stratumlogin>\w{1,})\:(?<stratumpassword>\S{1,})\@(?<host>[\w\.\-]{3,})\:(?<ports>[\d,]{1,})$",
                            "^(?<stratumlogin>\w{1,})\.(?<workername>\w{1,})\:(?<stratumpassword>\S{1,})\@(?<host>[\w\.\-]{3,})\:(?<ports>[\d,]{1,})$",
                            "^(?<stratumlogin>\w{1,})\.(?<workername>\w{1,})\@(?<host>[\w\.\-]{3,})\:(?<ports>[\d,]{1,})$"
                            }

                        Dim pHost As String = String.Empty
                        Dim pPorts As String = String.Empty
                        Dim pStratumLogin As String = String.Empty
                        Dim pStratumPassw As String = "x"
                        Dim pStratumWorker As String = String.Empty

                        For patternIdx As Integer = 0 To patterns.Length - 1
                            Dim matches As RegularExpressions.MatchCollection = RegularExpressions.Regex.Matches(argValue, patterns(patternIdx), RegularExpressions.RegexOptions.IgnoreCase)
                            If matches.Count > 0 Then
                                With matches(0)
                                    If .Groups(0).Success Then
                                        For groupIdx As Integer = 1 To .Groups.Count
                                            If .Groups(groupIdx).Success Then
                                                Select Case .Groups(groupIdx).Name
                                                    Case "host"
                                                        pHost = .Groups(groupIdx).Value.ToLower
                                                    Case "ports"
                                                        pPorts = .Groups(groupIdx).Value.ToLower
                                                    Case "stratumlogin"
                                                        pStratumLogin = .Groups(groupIdx).Value.Trim
                                                    Case "stratumpassword"
                                                        pStratumPassw = .Groups(groupIdx).Value.Trim
                                                    Case "workername"
                                                        pStratumWorker = .Groups(groupIdx).Value.Trim
                                                End Select
                                            End If
                                        Next
                                    End If
                                End With
                                Exit For
                            End If
                        Next

                        If Not pHost = String.Empty AndAlso pHost.EndsWith("nicehash.com") = False AndAlso Not pPorts = String.Empty Then
                            Dim newPoolEndPoint As New Pools.Pool(pHost, Core.Helpers.PortsFromString(pPorts)) With {
                                .StratumLogin = pStratumLogin,
                                .StratumPassw = pStratumPassw,
                                .StratumWorker = pStratumWorker,
                                .IsPrimary = False,
                                .DevFeeAddress = GetDevAddress($"{pHost}:{Core.Helpers.PortsFromString(pPorts)(0).ToString}")
                                }
                            App.Instance.PoolMgr.AddPool(newPoolEndPoint)
                        Else
                            argsErr.Enqueue(String.Format("Error : Pool specification {0} is invalid", argValue))
                        End If


                    Case "-si", "--stats-interval"

                        argIdx += 1
                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for stats interval specification", args(argIdx)))
                            Continue Do
                        End If


                        Dim intLevel As Integer = 0
                        If Integer.TryParse(args(argIdx), intLevel) Then
                            If (intLevel >= 10) Then
                                App.Instance.Settings.StatsInterval = intLevel
                            ElseIf (intLevel = 0) Then
                                App.Instance.Settings.StatsEnabled = False
                            Else
                                argsErr.Enqueue(String.Format("Error : Stats interval specification {0} is invalid (Min 10 seconds or 0)", args(argIdx)))
                            End If
                        Else
                            argsErr.Enqueue(String.Format("Error : Stats interval specification {0} is invalid (Min 10 seconds or 0)", args(argIdx)))
                        End If


                    Case "-sl", "--stratum-login"

                        argIdx += 1

                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for stratum login specification", args(argIdx)))
                            Continue Do
                        End If


                        ' Possible cases
                        ' Login only
                        ' Login and password
                        ' Login and workername and password
                        ' Login and workername

                        Dim patterns As String() = {
                            "^(?<stratumlogin>\w{1,})$",
                            "^(?<stratumlogin>\w{1,})\:(?<stratumpassword>\S{1,})$",
                            "^(?<stratumlogin>\w{1,})\.(?<workername>\S{1,})\:(?<stratumpassword>\S{1,})$",
                            "^(?<stratumlogin>\w{1,})\.(?<workername>\S{1,})$"
                            }

                        Dim pStratumLogin As String = String.Empty
                        Dim pStratumPassw As String = "x"
                        Dim pStratumWorker As String = String.Empty

                        For patternIdx As Integer = 0 To patterns.Length - 1
                            Dim matches As RegularExpressions.MatchCollection = RegularExpressions.Regex.Matches(args(argIdx), patterns(patternIdx), RegularExpressions.RegexOptions.IgnoreCase)
                            If matches.Count > 0 Then
                                With matches(0)
                                    If .Groups(0).Success Then
                                        For groupIdx As Integer = 1 To .Groups.Count
                                            If .Groups(groupIdx).Success Then
                                                Select Case .Groups(groupIdx).Name
                                                    Case "stratumlogin"
                                                        pStratumLogin = .Groups(groupIdx).Value.Trim
                                                    Case "stratumpassword"
                                                        pStratumPassw = .Groups(groupIdx).Value.Trim
                                                    Case "workername"
                                                        pStratumWorker = .Groups(groupIdx).Value.Trim
                                                End Select
                                            End If
                                        Next
                                    End If
                                End With
                                Exit For
                            End If
                        Next

                        If Not pStratumLogin = String.Empty Then
                            App.Instance.Settings.PoolsStratumLogin = pStratumLogin
                            App.Instance.Settings.PoolsStratumPassword = pStratumPassw
                            App.Instance.Settings.PoolsStratumWorker = pStratumWorker
                        Else
                            argsErr.Enqueue(String.Format("Error : Stratum Login {0} is invalid", args(argIdx)))
                        End If

                    Case "-rh", "--report-hashrate"

                        App.Instance.Settings.PoolsReportHashRate = True

                    Case "-rw", "--report-workers"

                        App.Instance.Settings.PoolsReportWorkerNames = True

                    Case "-nc", "--no-console"

                        App.Instance.Settings.NoConsole = True

                    Case "-np", "--no-probe"

                        App.Instance.Settings.PoolsNoProbe = True

                    Case "--no-fee"

                        App.Instance.Settings.NoFee = True

                    Case "-wt", "--work-timeout"

                        argIdx += 1

                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for work timeout specification", args(argIdx)))
                            Continue Do
                        End If

                        Dim intLevel As Integer = 0
                        If Integer.TryParse(args(argIdx), intLevel) Then
                            If (intLevel >= 30 AndAlso intLevel <= 300) Then
                                App.Instance.Settings.PoolsWorkTimeout = intLevel
                            End If
                        Else
                            argsErr.Enqueue(String.Format("Error : Work Timeout specification {0} is invalid (Min 30 Max 300 seconds)", args(argIdx)))
                        End If

                    Case "-ws", "--workers-spacing"

                        argIdx += 1

                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for workers spacing specification", args(argIdx)))
                            Continue Do
                        End If

                        Dim intLevel As Integer = 0
                        If Integer.TryParse(args(argIdx), intLevel) Then
                            If (intLevel >= 16 AndAlso intLevel <= 40) Then
                                App.Instance.Settings.WorkersSpacing = intLevel
                            End If
                        Else
                            argsErr.Enqueue(String.Format("Error : Workers spacing specification {0} is invalid (Min 16 Max 40)", args(argIdx)))
                        End If

                    Case "-rt", "--response-timeout"

                        argIdx += 1
                        If (argIdx >= args.Length) Then
                            argsErr.Enqueue(String.Format("Error : Missing value for response timeout specification", args(argIdx)))
                            Continue Do
                        End If

                        Dim intLevel As Integer = 0
                        If Integer.TryParse(args(argIdx), intLevel) Then
                            If (intLevel >= 10 AndAlso intLevel <= 30000) Then
                                App.Instance.Settings.PoolsResponseTimeout = intLevel
                            End If
                        Else
                            argsErr.Enqueue(String.Format("Error : Response Timeout specification {0}ms is invalid (Min 10 Max 30000 ms)", args(argIdx)))
                        End If


                    Case "-h", "--help"

                        Console.Out.WriteLine(Core.GetHelpText)
                        Environment.Exit(0)

                    Case Else

                        argsErr.Enqueue(String.Format("Error : Unknown command argument {0}", args(argIdx)))

                End Select
                argIdx += 1
            Loop While argIdx < args.Length
        End If

        ' -----------------------------------------
        ' Validate arguments
        ' -----------------------------------------
        With App.Instance.PoolMgr

            If .PoolsQueue.Where(Function(p) p.IsValid).Count < 1 Then
                argsErr.Enqueue("Error : No valid pools available for connection")
            End If

            If .StratumLogin = String.Empty Then
                If .PoolsQueue.Where(Function(p) p.StratumLogin <> String.Empty).Count < 1 Then
                    argsErr.Enqueue("Error : No valid stratum logins were found")
                End If
            End If

        End With


        If argsErr.Count > 0 Then
            Do While argsErr.Count > 0
                Console.Error.WriteLine(argsErr.Dequeue)
            Loop
            Console.Out.WriteLine("Try using --help")
            Console.Out.WriteLine("Terminating ...")
            Environment.Exit(ExitCodes.ArgumentsError)
        End If

        ' -----------------------------------------
        ' Check DevFee is set
        ' -----------------------------------------
        If App.Instance.PoolMgr.PoolsQueue.Where(Function(p) String.IsNullOrEmpty(p.DevFeeAddress) = True).Count > 0 Then
            App.Instance.Settings.NoFee = True
            Console.WriteLine("  One or more pools not available for donation fee.")
            Console.WriteLine("  Setting --no-fee")
        End If
        If App.Instance.Settings.NoFee = True Then
            Console.WriteLine("  --no-fee is set. No developer fee will be applied.")
            Console.WriteLine("  Developer looses all his revenues.")
            Console.WriteLine("  This proxy will NOT do any optimization.")
            Console.WriteLine(" ")
        Else

            Console.WriteLine($"  Developer fee set to {Helpers.DONATE_LEVEL * 100}%. Thank you.")
            Console.WriteLine(" ")
        End If


        ' -----------------------------------------
        ' Start
        ' -----------------------------------------
        StartProxy()
        Environment.Exit(ExitCodes.Success)

    End Sub

    Sub StartProxy()

        ' Start working
        ' ThreadPool.QueueUserWorkItem(New Threading.WaitCallback(AddressOf App.Start))
        ' Dim thListener As Threading.Thread = New Threading.Thread(AddressOf App.PoolMgr.Start)
        App.Instance.Start()

        'thListener.Start()
        If App.Instance.Settings.NoConsole Then

            AddHandler AssemblyLoadContext.Default.Unloading, AddressOf OnSignalReceived
            signalReceived.Wait()
            Logger.Log(0, "Signal intercepted ...", "Main")
            terminated.Wait()

        Else

            Do

                If Console.KeyAvailable Then
                    Dim keyPressed As ConsoleKeyInfo = Console.ReadKey(True)
                    Select Case True

                        Case keyPressed.Key = ConsoleKey.Q

                            ' Quit
                            Logger.Log(0, "Shutting down ...")
                            App.Instance.Stop()
                            Logger.Log(0, "All done !")
                            Exit Do

                        Case keyPressed.Key = ConsoleKey.S

                            ' Switch pool
                            Logger.Log(0, "Switching pool ...")
                            App.Instance.PoolMgr.SwitchPool()

                        Case keyPressed.Key = ConsoleKey.R

                            ' Reconnect
                            Logger.Log(0, "Reconnecting ...")
                            App.Instance.PoolMgr.Reconnect()

                        Case keyPressed.Key = ConsoleKey.Subtract

                            ' Decrease log verbosity
                            If App.Instance.Settings.LogVerbosity > 1 Then
                                Interlocked.Decrement(App.Instance.Settings.LogVerbosity)
                            End If
                            Logger.Log(0, "Log verbosity set to " + App.Instance.Settings.LogVerbosity.ToString)

                        Case keyPressed.Key = ConsoleKey.Add

                            ' Increase log verbosity
                            If App.Instance.Settings.LogVerbosity < 9 Then
                                Interlocked.Increment(App.Instance.Settings.LogVerbosity)
                            End If
                            Logger.Log(0, "Log verbosity set to " + App.Instance.Settings.LogVerbosity.ToString)

                        Case Else
                            Logger.Log(0, "Unrecongnized input key command")
                    End Select
                End If
                Thread.Sleep(100)
            Loop

        End If


    End Sub

    Public Sub OnSignalReceived(e As AssemblyLoadContext)

        signalReceived.Set()

        Logger.Log(0, "Shutting down ...")
        App.Instance.Stop()
        Logger.Log(0, "All done !")

        terminated.Set()


    End Sub

End Module
