# neth-proxy
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

## Requirements
neth-proxy is built on top of [.NET Core 2.0](https://github.com/dotnet/core) thus working without problems on Windows Linux or Mac. Coding language is VB.Net (_yeah I know know ... keep your comments about VB out of this_).
All connected miner **must** be [ethminer](https://github.com/ethereum-mining/ethminer) min version 0.15.rc2 or better. No other miner willing to connect is currently supported (maybe in future).

**Important. This proxy is NOT a tool to steal or reduce developer's fees for other miners**

## Who uses neth-proxy
Well actually me and my clients. Maybe if you want to submit a review I will publish it.

## How to get started with neth-proxy
1. Install [.NET Core 2.0](https://github.com/dotnet/core) runtime 
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
3. From the release section of this repository download the most recent version and unzip it to a directory of your choice
