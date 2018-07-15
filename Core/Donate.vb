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

Namespace Core

    Partial Public Module Helpers

        ''' <summary>
        ''' Defines the donation %
        ''' </summary>
        ''' <remarks>
        ''' 
        ''' Default level is 0.75% this means that every 100 minutes
        ''' of connection to pool 45 seconds will be dedicated to
        ''' devfee
        ''' 
        ''' If you plan to set this value to 0 please consider you're
        ''' making me loose any revenue from my work.
        ''' 
        ''' If you wish to donate directly here are my donation addresses
        ''' 
        ''' Ethereum          0x9E431042fAA3224837e9BEDEcc5F4858cf0390B9
        ''' Ethereum Classic  0x6e4Aa5064ced1c0e9E20A517B9d7A7dDe32A0dcf
        ''' 
        ''' </remarks>
        Public Const DONATE_LEVEL As Double = 0.75 / 100

    End Module

End Namespace
