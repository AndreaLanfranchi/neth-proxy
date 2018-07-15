@echo off
rem =======================================================================================
rem
rem   This file is part of neth-proxy.
rem
rem   neth-proxy is free software: you can redistribute it and/or modify
rem   it under the terms Of the GNU General Public License As published by
rem   the Free Software Foundation, either version 3 Of the License, Or
rem   (at your option) any later version.
rem
rem   neth-proxy is distributed In the hope that it will be useful,
rem   but WITHOUT ANY WARRANTY; without even the implied warranty Of
rem   MERCHANTABILITY Or FITNESS FOR A PARTICULAR PURPOSE.  See the
rem   GNU General Public License For more details.
rem
rem   You should have received a copy Of the GNU General Public License
rem   along with neth-proxy.  If not, see < http://www.gnu.org/licenses/ >.
rem
rem =======================================================================================

rem   Here you can hardcode your arguments if you wish.
SET ARGS=-sp 0x9E431042fAA3224837e9BEDEcc5F4858cf0390B9@eu1.ethermine.org:4444 --report-hashrate --work-timeout 120 --response-timeout 2000 --report-workers --stats-interval 10 --api-bind 3333

rem   If you pass arguments on the command line they will override the above statement
IF NOT "%*"=="" SET ARGS=%*

:start
dotnet exec neth-proxy.dll %ARGS%
rem =======================================================================================
rem Uncomment the following lines if you want proxy to restart after an error
rem =======================================================================================

rem IF "%errorlevel%"=="0" GOTO end
rem timeout /T 5
rem goto start

:end
