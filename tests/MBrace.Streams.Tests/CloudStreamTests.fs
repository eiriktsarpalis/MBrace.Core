﻿namespace MBrace.Streams.Tests

#nowarn "0444" // Disable mbrace warnings
open System
open System.Linq
open System.Collections.Generic
open System.IO
open FsCheck
open NUnit.Framework
open Nessos.Streams
open MBrace.Streams
open MBrace
open MBrace.Store


type Check =
    static member QuickThrowOnFailureConfig(maxNumber) = { Config.QuickThrowOnFailure with MaxTest = maxNumber }

    /// quick check methods with explicit type annotation
    static member QuickThrowOnFail<'T> (f : 'T -> unit, ?maxNumber) = 
        match maxNumber with
        | None -> Check.QuickThrowOnFailure f
        | Some mxrs -> Check.One({ Config.QuickThrowOnFailure with MaxTest = mxrs }, f)

    /// quick check methods with explicit type annotation
    static member QuickThrowOnFail<'T> (f : 'T -> bool, ?maxNumber) = 
        match maxNumber with
        | None -> Check.QuickThrowOnFailure f
        | Some mxrs -> Check.One({ Config.QuickThrowOnFailure with MaxTest = mxrs }, f)

[<TestFixture; AbstractClass>]
type ``CloudStreams tests`` () as self =
    let run (workflow : Cloud<'T>) = self.Run(workflow)
    let runLocal (workflow : Cloud<'T>) = self.RunLocal(workflow)

    abstract Run : Cloud<'T> -> 'T
    abstract RunLocal : Cloud<'T> -> 'T
    abstract FsCheckMaxNumberOfTests : int

    [<Test>]
    member __.``ofArray`` () =
        let f(xs : int []) =
            let x = xs |> CloudStream.ofArray |> CloudStream.length |> run
            let y = xs |> Seq.map ((+)1) |> Seq.length
            Assert.AreEqual(y, int x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``ofCloudVector`` () =
        let f(xs : int []) =
            let CloudVector = run <| CloudVector.New(xs, 100L) 
            let x = CloudVector |> CloudStream.ofCloudVector |> CloudStream.length |> run
            let y = xs |> Seq.map ((+)1) |> Seq.length
            Assert.AreEqual(y, int x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


    [<Test>]
    member __.``toCloudVector`` () =
        let f(xs : int[]) =            
            let x = xs |> CloudStream.ofArray |> CloudStream.map ((+)1) |> CloudStream.toCloudVector |> run
            let y = xs |> Seq.map ((+)1) |> Seq.toArray
            Assert.AreEqual(y, x.ToEnumerable() |> runLocal)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``cache`` () =
        let f(xs : int[]) =
            let v = run <| CloudVector.New(xs, 1024L) 
            v.Cache() |> run 
            let x = v |> CloudStream.ofCloudVector |> CloudStream.map  (fun x -> x * x) |> CloudStream.toCloudVector |> run
            let x' = v |> CloudStream.ofCloudVector |> CloudStream.map (fun x -> x * x) |> CloudStream.toCloudVector |> run
            let y = xs |> Seq.map (fun x -> x * x) |> Seq.toArray
            
            let _x = x.ToEnumerable() |> runLocal |> Seq.toArray
            let _x' = x'.ToEnumerable() |> runLocal |> Seq.toArray
            
            Assert.AreEqual(y, _x)
            Assert.AreEqual(_x', _x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``ofCloudFiles with ReadAllText`` () =
        let f(xs : string []) =
            let cfs = xs 
                     |> Array.map(fun text -> CloudFile.WriteAllText(text))
                     |> Cloud.Parallel
                     |> run

            let x = cfs |> CloudStream.ofCloudFiles CloudFileReader.ReadAllText
                        |> CloudStream.toArray
                        |> run
                        |> Set.ofArray

            let y = cfs |> Array.map (fun f -> __.RunLocal(CloudFile.ReadAllText(f)))
                        |> Set.ofSeq

            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, 10)

    [<Test>]
    member __.``ofCloudFiles with ReadLines`` () =
        let f(xs : string [][]) =
            let cfs = xs 
                     |> Array.map(fun text -> CloudFile.WriteAllLines(text))
                     |> Cloud.Parallel
                     |> run

            let x = cfs |> CloudStream.ofCloudFiles CloudFileReader.ReadLines
                        |> CloudStream.collect Stream.ofSeq
                        |> CloudStream.toArray
                        |> run
                        |> Set.ofArray
            
            let y = cfs |> Array.map (fun f -> __.RunLocal(CloudFile.ReadAllLines(f)))
                        |> Seq.collect id
                        |> Set.ofSeq

            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, 10)

    [<Test>]
    member __.``ofCloudFiles with ReadAllLines`` () =
        let f(xs : string [][]) =
            let cfs = xs 
                     |> Array.map(fun text -> CloudFile.WriteAllLines(text))
                     |> Cloud.Parallel
                     |> run

            let x = cfs |> CloudStream.ofCloudFiles CloudFileReader.ReadAllLines
                        |> CloudStream.collect Stream.ofArray
                        |> CloudStream.toArray
                        |> run
                        |> Set.ofArray

            let y = cfs |> Array.map (fun f -> __.RunLocal(CloudFile.ReadAllLines(f)))
                        |> Seq.collect id
                        |> Set.ofSeq

            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, 10)


    [<Test>]
    member __.``map`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.map (fun n -> 2 * n) |> CloudStream.toArray |> run
            let y = xs |> Seq.map (fun n -> 2 * n) |> Seq.toArray
            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``filter`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.filter (fun n -> n % 2 = 0) |> CloudStream.toArray |> run
            let y = xs |> Seq.filter (fun n -> n % 2 = 0) |> Seq.toArray
            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


    [<Test>]
    member __.``collect`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.collect (fun n -> [|1..n|] |> Stream.ofArray) |> CloudStream.toArray |> run
            let y = xs |> Seq.collect (fun n -> [|1..n|]) |> Seq.toArray
            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``fold`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.map (fun n -> 2 * n) |> CloudStream.fold (+) (+) (fun () -> 0) |> run
            let y = xs |> Seq.map (fun n -> 2 * n) |> Seq.fold (+) 0 
            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``sum`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.map (fun n -> 2 * n) |> CloudStream.sum |> run
            let y = xs |> Seq.map (fun n -> 2 * n) |> Seq.sum
            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``length`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.filter (fun n -> n % 2 = 0) |> CloudStream.length |> run
            let y = xs |> Seq.filter (fun n -> n % 2 = 0) |> Seq.length
            Assert.AreEqual(y, int x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


    [<Test>]
    member __.``countBy`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.countBy id |> CloudStream.toArray |> run
            let y = xs |> Seq.countBy id |> Seq.map (fun (k,c) -> k, int64 c) |> Seq.toArray
            Assert.AreEqual(set y, set x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


    [<Test>]
    member __.``sortBy`` () =
        let f(xs : int[]) =
            let x = xs |> CloudStream.ofArray |> CloudStream.sortBy id 10 |> CloudStream.toArray |> run
            let y = (xs |> Seq.sortBy id).Take(10).ToArray()
            Assert.AreEqual(y, x)
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
    member __.``withDegreeOfParallelism`` () =
        let f(xs : int[]) = 
            let r = xs 
                    |> CloudStream.ofArray
                    |> CloudStream.map (fun _ -> System.Diagnostics.Process.GetCurrentProcess().Id)
                    |> CloudStream.withDegreeOfParallelism 1
                    |> CloudStream.toArray
                    |> run
            let x = r
                    |> Set.ofArray
                    |> Seq.length
            if xs.Length = 0 then x = 0
            else x = 1
        Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

    [<Test>]
        member __.``tryFind`` () =
            let f(xs : int[]) =
                let x = xs |> CloudStream.ofArray |> CloudStream.tryFind (fun n -> n = 0) |> run
                let y = xs |> Seq.tryFind (fun n -> n = 0) 
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

        [<Test>]
        member __.``find`` () =
            let f(xs : int[]) =
                let x = try xs |> CloudStream.ofArray |> CloudStream.find (fun n -> n = 0) |> run with | :? KeyNotFoundException -> -1
                let y = try xs |> Seq.find (fun n -> n = 0) with | :? KeyNotFoundException -> -1
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

        [<Test>]
        member __.``tryPick`` () =
            let f(xs : int[]) =
                let x = xs |> CloudStream.ofArray |> CloudStream.tryPick (fun n -> if n = 0 then Some n else None) |> run
                let y = xs |> Seq.tryPick (fun n -> if n = 0 then Some n else None) 
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

        [<Test>]
        member __.``pick`` () =
            let f(xs : int[]) =
                let x = try xs |> CloudStream.ofArray |> CloudStream.pick (fun n -> if n = 0 then Some n else None) |> run with | :? KeyNotFoundException -> -1
                let y = try xs |> Seq.pick (fun n -> if n = 0 then Some n else None)  with | :? KeyNotFoundException -> -1
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)

        [<Test>]
        member __.``exists`` () =
            let f(xs : int[]) =
                let x = xs |> CloudStream.ofArray |> CloudStream.exists (fun n -> n = 0) |> run
                let y = xs |> Seq.exists (fun n -> n = 0) 
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


        [<Test>]
        member __.``forall`` () =
            let f(xs : int[]) =
                let x = xs |> CloudStream.ofArray |> CloudStream.forall (fun n -> n = 0) |> run
                let y = xs |> Seq.forall (fun n -> n = 0) 
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


        [<Test>]
        member __.``forall/CloudFiles`` () =
            let f(xs : int []) =
                let cfs = xs 
                         |> Array.map (fun x -> CloudFile.WriteAllText(string x))
                         |> Cloud.Parallel
                         |> run
                let x = cfs |> CloudStream.ofCloudFiles CloudFileReader.ReadAllText |> CloudStream.forall (fun x -> Int32.Parse(x) = 0) |> run
                let y = xs |> Seq.forall (fun n -> n = 0) 
                x = y
            Check.QuickThrowOnFail(f, self.FsCheckMaxNumberOfTests)


open MBrace.Runtime.Vagabond
open MBrace.Runtime.Serialization
open MBrace.Runtime.Store


type ``InMemory CloudStreams tests`` () =
    inherit ``CloudStreams tests`` ()

    do VagabondRegistry.Initialize(throwOnError = false)

    let fileStore = FileSystemStore.CreateUniqueLocal()
    let serializer = new FsPicklerBinaryStoreSerializer()
    let objcache = InMemoryCache.Create()
    let fsConfig = CloudFileStoreConfiguration.Create(fileStore, serializer, cache = objcache)
    let imem = MBrace.Client.LocalRuntime.Create(fileConfig = fsConfig)

    override __.Run(workflow : Cloud<'T>) = imem.Run workflow
    override __.RunLocal(workflow : Cloud<'T>) = imem.Run workflow
    override __.FsCheckMaxNumberOfTests = 100