# neth-proxy

[![standard-readme compliant](https://img.shields.io/badge/readme%20style-standard-brightgreen.svg)](https://github.com/RichardLitt/standard-readme)

This is a stratum to stratum proxy expressly designed to optimize [ethminer](https://github.com/ethereum-mining/ethminer)'s multi instances: whether you run a single rig with a separate ethminer instance per GPU or you run multiple rigs each with it's own ethminer instance you may want to use neth-proxy.
**If you run only one instance of ethminer then neth-proxy will not give you any benefit**

## Features
* Keeps only one connection to your pool
* Auto stratum mode recognition : whether your pool implements stratum or ethproxy mode is supported. **Nicehash mode is not supported** (yet ...)
* If pool provides multiple ip addresses neth-proxy will pick the one with fastest roundtrip
* Centrally managed wallet configuration : your miners do not need to know the wallet address
* Ensures all connected [ethminer](https://github.com/ethereum-mining/ethminer) miners do work on **non overlapping** ranges of nonces
* Clusters all your miners as if they were a single machine (if you have 5 rigs with 6 GPUs each you will mine as if you had a single rig made of 30 GPUs)
* Jobs are **pushed** immediately to all connected miners. No need to set `--farm-recheck` values on ethminer.
* Reduced payout times by 3% to 5%
* Less stale shares than with eth-proxy
* Customizable `--work-timeout` and `--response-timeout` values to trigger fallback pools
* Instant cumulative info about overall hashrate, connected miners, jobs received and solutions sumbitted with percent values of known stale shares and rejects
* API interface to monitor cluster status or single miner. New methods being added

## Developer Fees

Usage of neth-proxy comes with a fee of 0.75% which means your connected miners will mine for the developer for 30 seconds every 4000 seconds (roughly 1 hour and 7 minutes).
If you do not want to pay such a fee you can set `--no-fee` command line argument on launch. This will make neth-proxy absolutely free but it won't do any segment adjustment for your miners nor it will check they do not overlap.
In any case you will get better results than with eth-proxy.
Alternatively you can modify source code and rebuild the binaries on your own.

If you wish to make a direct donation you're welcome to use either theese addresses:

* ETH 0x9E431042fAA3224837e9BEDEcc5F4858cf0390B9
* ETC 0x6e4Aa5064ced1c0e9E20A517B9d7A7dDe32A0dcf

## Requirements
neth-proxy is built on top of [.NET Core 2.0](https://github.com/dotnet/core) thus working without problems on Windows Linux or Mac. Coding language is VB.Net (_yeah I know know ... keep your comments about VB out of this_).
All connected miner **must** be [ethminer](https://github.com/ethereum-mining/ethminer) min version 0.15.rc2 or better. No other miner willing to connect is currently supported (maybe in future).

**Important. This proxy is NOT a tool to steal or reduce developer's fees for other miners**

## Who uses neth-proxy
Well actually me and my clients. Maybe if you want to submit a review I will publish it.

## How to get started with neth-proxy
1. Install [.NET Core 2.0+](https://github.com/dotnet/core) runtime 
2. Verify .NET core version 2+ To do this, on a command prompt, type ` dotnet --info` and expect an output like this
```
.NET Command Line Tools (2.1.4)

Product Information:
 Version:            2.1.4
 Commit SHA-1 hash:  5e8add2190

Runtime Environment:
 OS Name:     centos
 OS Version:  7
 OS Platform: Linux
 RID:         centos.7-x64
 Base Path:   /usr/share/dotnet/sdk/2.1.4/

Microsoft .NET Core Shared Framework Host

  Version  : 2.0.5
  Build    : 17373eb129b3b05aa18ece963f8795d65ef8ea54
```

If this appears you can skip next step and jump directly to download and install neth-proxy binaries.

## Installing .NET Core Runtime

If you're installing on Linux please check this [list of dependencies](https://github.com/dotnet/docs/blob/master/docs/core/linux-prerequisites.md#linux-distribution-dependencies).

Installation on Linux (Ubuntu 16.04 LTS)

1. Before installing .NET, you'll need to register the Microsoft key, register the product repository, and install required dependencies. This only needs to be done once per machine.
```
$ wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb
$ sudo dpkg -i packages-microsoft-prod.deb
```

2. Update the products available for installation, then install the .NET Core Runtime.
```
$ sudo apt-get install apt-transport-https
$ sudo apt-get update
$ sudo apt-get install dotnet-runtime-2.0.7
```

Installation on Windows

[Download and install](https://www.microsoft.com/net/download/dotnet-core/runtime-2.0.7) appropriate package from Microsoft's site.

When you're done with installation please check your installed version (How to get started with neth-proxy step 2)

## Download latest neth-proxy binary release 

Access the [releases](https://github.com/AndreaLanfranchi/neth-proxy/releases) section of this repository and pick the latest release.
Archive is in .zip format. Expand (uncompress) the archive in a directory of your choice.

## How to start neth-proxy and connect to it
Every release package contains two launch scripts. 
1. neth-proxy.bat to be used in Windows environments
2. neth-proxy.sh to be used in Linux environments

If you're on Linux you might want to mark neth-proxy.sh as executable by
```
$ chmod +x neth-proxy.sh
```

To start neth-proxy ...
on Linux
```
$ ./neth-proxy.sh [command line args]
```
on Windows
```
C:\neth-proxy.bat [command line args]
```

Now you can set your [ethminer](https://github.com/ethereum-mining/ethminer) miners to connect to your local proxy.
**Please NOTE ethminer 0.15.rc2 is minimum required versio with api enabled**

Syntax for connection is :
```
ethminer [..] --api-port <nnnn> -P stratum+tcp://<neth-proxy-ip-address>:<neth-proxy-port>/<workername>/<nnnn>
```
where 
* `<nnnn>` is the port number where ethminer will listen on
* `<neth-proxy-ip-address>` is the ip address of the computer where you're running neth-proxy
* `<neth-proxy-port>` is the portnumber neth-proxy is listening for connections (default is 4444)

You do not need to set a wallet address as it's already configured in neth-proxy

For a detailed explanation of command line arguments please read the following chapter

## neth-proxy Command Line Arguments

To start neth-proxy you need to define at least one connection to a pool of your choice thus the very basic startup is like

```
$ ./neth-proxy.sh -sp 0x9E431042fAA3224837e9BEDEcc5F4858cf0390B9@eu1.ethermine.org:4444
```

where 0x9E431042fAA3224837e9BEDEcc5F4858cf0390B9 have to be replaced with **YOUR** wallet address.

This will instruct neth-proxy to connect to eu1.ethermine.org pool on port 4444 and will listen locally for incoming miners connections on port 4444 (which is the default).
If you want to specify one (or more) failover pools simply add as many -sp arguments you want.

```
$ ./neth-proxy.sh -sp <wallet>@eu1.ethermine.org:4444 -sp <wallet>@eth-eu1.nanopool.org:9999
```

For a detailed list of command line arguments you may want to type
```
$ ./neth-proxy.sh --help
```

and you'll be prompted with a help text
```
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
-sp | --stratumpool       Is the connection to the target pool this proxy has to forward workers
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
```

## Ethash pools tested and supported
 
| Pool Name | Pool Homepage | Details about connection |
| --------- | ------------- | - |
| 2miners.com | <https://2miners.com/> | <https://eth.2miners.com/en/help> |
| dwarfpool.org | <https://dwarfpool.com/> | <https://dwarfpool.com/eth> |
| ethermine.org | <https://ethermine.org/> | <https://ethermine.org/> |
| ethpool.org | <https://www.ethpool.org/> | <https://www.ethpool.org/> |
| f2pool.com | <https://www.f2pool.com/> | <https://www.f2pool.com/help/?#tab-content-eth> |
| miningpoolhub.com | <https://miningpoolhub.com/> | <https://ethereum.miningpoolhub.com/> |
| nanopool.org | <https://nanopool.org/> | <https://eth.nanopool.org/help> |
| sparkpool.com | <https://sparkpool.com/> | <https://eth.sparkpool.com/> |
 
Syntax to connect to those pools is always the same 
 
```
$ ./neth-proxy.sh -sp <wallet>@<pool-host-name>:<pool-port>
```
