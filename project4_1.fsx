#r "nuget: Akka.FSharp"
#load "client.fsx"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Diagnostics

let mainTimer = Stopwatch()

let main() =
    try

        (* Command Line input - num of clients and num of tweets *)
        mainTimer.Start()

        let mutable numClients = fsi.CommandLineArgs.[1] |> int
        let numActivities = fsi.CommandLineArgs.[2] |> int

        printfn "********* Command Line Input *********"
        printfn "Number of users = %d and Number of activities per user = %d " numClients numActivities

  
        let createTask = Client.clientAgentRef <? Client.Agent.StartProcess(numClients, numActivities)
        let response = Async.RunSynchronously(createTask)
        let totalTime = mainTimer.ElapsedMilliseconds
        mainTimer.Stop()

        let serverRequestsPerSecond = ((Client.serverTasksCount |> float) / ((totalTime |> float) / 1000.0))


        printfn "******** Performance Characterisitcs of Twitter Clone System ********"
        printfn "Registration Time = %d ms" Client.registrationTime
        printfn "Subscription Time = %d ms" Client.zipfSubscriptionTime
        printfn "Activity Time = %d ms" Client.totalActivityTime
        printfn "Total Time taken = %d ms" totalTime
        printfn "Total requests received by Server = %d" Client.serverTasksCount
        printfn "Rate of requests processed by the server (per second) = %d" (serverRequestsPerSecond |> int)
        printfn "*********************************************************************"
       
        Client.system.Terminate() |> ignore

    
    with :? TimeoutException ->
        printfn "Timeout!"

main()
// System.Console.ReadKey() |> ignore
