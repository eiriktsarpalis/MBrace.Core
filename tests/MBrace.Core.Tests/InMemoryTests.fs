﻿namespace MBrace.Tests

open System.Threading

open MBrace
open MBrace.Continuation
open MBrace.Runtime
open MBrace.Client

open NUnit.Framework

type InMemoryLogTester () =
    let logs = new ResizeArray<string>()

    interface ILogTester with
        member __.GetLogs () = logs.ToArray()
        member __.Clear () = lock logs (fun () -> logs.Clear())

    interface ICloudLogger with
        member __.Log msg = lock logs (fun () -> logs.Add msg)

type InMemoryCancellationTokenSource() =
    let cts = new CancellationTokenSource()
    member __.Token = cts.Token
    interface ICancellationTokenSource with
        member __.Cancel() = cts.Cancel()

type ``ThreadPool Parallelism Tests`` () =
    inherit ``Parallelism Tests``(nParallel = 100)

    let logger = InMemoryLogTester()
    let imem = LocalRuntime.Create(logger = logger)

    override __.Run(workflow : Cloud<'T>) = Choice.protect (fun () -> imem.Run(workflow))
    override __.Run(workflow : ICancellationTokenSource -> Cloud<'T>) =
        let cts = new InMemoryCancellationTokenSource()
        Choice.protect(fun () ->
            imem.Run(workflow cts, cancellationToken = cts.Token))

    override __.RunLocal(workflow : Cloud<'T>) = imem.Run(workflow)
    override __.IsTargetWorkerSupported = false
    override __.Logs = logger :> _
    override __.FsCheckMaxTests = 100
#if DEBUG
    override __.Repeats = 10
#else
    override __.Repeats = 3
#endif


type ``InMemory CloudAtom Tests`` () =
    inherit ``CloudAtom Tests`` (nParallel = 100)

    let imem = LocalRuntime.Create()

    override __.Run(workflow) = imem.Run workflow
    override __.RunLocal(workflow) = imem.Run workflow
    override __.AtomClient = imem.StoreClient.Atom
#if DEBUG
    override __.Repeats = 10
#else
    override __.Repeats = 3
#endif

type ``InMemory CloudChannel Tests`` () =
    inherit ``CloudChannel Tests`` (nParallel = 100)

    let imem = LocalRuntime.Create()

    override __.Run(workflow) = imem.Run workflow
    override __.RunLocal(workflow) = imem.Run workflow
    override __.ChannelClient = imem.StoreClient.Channel