module Client

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open System.Diagnostics

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            loglevel: ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                deployment {
                    /remoteecho {
                        remote = ""akka.tcp://RemoteClient@localhost:9001""
                    }
                }
            }
            remote {
                helios.tcp {
                    port = 8001
                    hostname = localhost
                }
            }
        }")

let system = System.create "RemoteClient" configuration
let server = system.ActorSelection("akka.tcp://RemoteActorFactory@localhost:9001/user/TwitterAgent")

let rnd = Random()
let timer = Stopwatch()

let actorMap = new Dictionary<string, IActorRef>()

let mutable registrationTime = 0L 
let mutable zipfSubscriptionTime = 0L
let mutable totalActivityTime = 0L
let mutable serverTasksCount = 0
let mutable serverTotalTime = 0L

type TwitterServer =
    | InitServer
    | RegisterUser of string


type Client =
    | Register of string
    | Subscribe of int
    | SendTweet
    | ReTweet
    | QueryTweet
    | FetchFeed
    | Login
    | Logout
    | PerformActivities of int
    | ReceiveMyFeed of List<string * string>
    | ReceiveQueryFeed of (string * List<string * string>)

type Agent =
    | StartProcess of int * int
    | CallForSubscription
    | InitiateActivity
    | TrackActivityCompletion
    | GetServerTasksCount

let userActorsList = new List<IActorRef>()
let userIDList = new List<string>()
let mutable totalClients = 0
let mutable totalActivities = 0
let mutable activityCompletedCount = 0
let mutable mainProcessRef = Unchecked.defaultof<_>
let mutable clientAgentRef = Unchecked.defaultof<_>
let mutable callBackAgentRef = Unchecked.defaultof<_>

//Defualt Variables
let _followersList = new List<string>()
let _tweetObj = ("", "")
let _hashtag = ""
let _mention = ""
let _feedTuple = ("", new List<string * string>())
let _queryResult = ("", ("", new List<string * string>()))


//************ Registration and Subscription *************************//

let followersByZipfDistribution(followersCount, clientID) =
    let followersList = new List<string>()
    let tempClientList = new List<string>()
    for user in userIDList do
        tempClientList.Add(user)

    tempClientList.Remove(clientID)|> ignore
    for i = 1 to followersCount do
        let follower = tempClientList.[rnd.Next(tempClientList.Count)]
        tempClientList.Remove(follower) |> ignore
        followersList.Add(follower)
    
    followersList
      
let zipfDistribution(count) = 
    if count = 1 then
        totalClients-1
    else
        Math.Ceiling((totalClients|>float)/(count|>float)) |>int

//***************** User Login and Logout ********///

let logout(user) =
    server <! ("LogoutUser", user, _followersList, _tweetObj, _hashtag, _mention)
    1

let login(user) = 
    server <! ("LoginUser", user, _followersList, _tweetObj, _hashtag, _mention)
    1


//************ Send Tweet *************************//

let listOfTweets = ["Tweet1"; "Tweet2"; "Tweet3"; "Tweet4"; "Tweet5"; "Tweet6"; 
                    "Tweet7"; "Tweet8"; "Tweet9"; "Tweet10"]

let hashtags = ["#COP5615"; "#DOS"; "#TwitterClone"; "#UF"; "#CISE"]

let sendTweet(user) =
    let tweetType = rnd.Next(4)
    let mutable tweet = listOfTweets.[rnd.Next(listOfTweets.Length)]

    //A tweet with mention and hashtag    
    if tweetType = 0 then
        let mention = "@" + userIDList.[rnd.Next(userIDList.Count)]
        let hashtag = hashtags.[rnd.Next(hashtags.Length)]
        tweet <- mention + " " + tweet + " " + hashtag

    //A tweet with a mention
    elif tweetType = 1 then
        let mention = "@" + userIDList.[rnd.Next(userIDList.Count)]
        tweet <- mention + " " + tweet

    //A tweet with a hashtag
    elif tweetType = 2 then
        let hashtag = hashtags.[rnd.Next(hashtags.Length)]
        tweet <- tweet + " " + hashtag

    let tweetObj = (user, tweet)
    server <! ("SendTweet", user, _followersList, tweetObj, _hashtag, _mention)

//******* Fetch Feed*******//
let fetchFeed(user) =
    server <! ("FetchFeed", user, _followersList, _tweetObj, _hashtag, _mention)

//************ Retweet *************************//

// let fetchOneOfmyTweet(user) =
//     let task = server <? ("FetchFeedForRetweet", user, _followersList, _tweetObj, _hashtag, _mention)
//     let mutable newsFeed = new List<string * string>()
//     newsFeed <- Async.RunSynchronously(task)
//     newsFeed

let retweet(user:string, newsFeed: List<string*string>) =
    server <! ("Retweet", user, _followersList, _tweetObj, _hashtag, _mention)


//************ QueryHashtag *************************//

let queryHashtag(user, hashtag) = 
    server <! ("QueryHashtag", user, _followersList, _tweetObj, hashtag, _mention)


//************ QueryMention *************************//

let queryMention(user, mention) = 
    server <! ("QueryMention", user, _followersList, _tweetObj, _hashtag, mention)

//***** Client Master and Agent ***********//

let clientMaster (mailbox: Actor<_>) = 
    let mutable clientID = ""
    let mutable activityCounter = 0
    let mutable clientStatus = true
    let mutable activityNum = 0
    let mutable myFeed = new List<string * string>()

    // let listOfActivities = [SendTweet; ReTweet; QueryTweet; FetchFeed; Logout;]
    let listOfActivities = [SendTweet; ReTweet; FetchFeed; QueryTweet; Logout;]
    let listOfQueries = ["queryHashtag"; "queryMention"]
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()

        match msg with
        | Register (userID) ->
            clientID <- userID
            server <! ("RegisterUser", clientID, _followersList, ("", ""), "", "")
        
        | Subscribe (clientPos) ->
            let followersCount = zipfDistribution(clientPos)
            let followersList = followersByZipfDistribution(followersCount, clientID)
            server <! ("Subscribe", clientID, followersList, ("", ""), "", "")

        | SendTweet ->
            sendTweet(clientID)

        | ReTweet ->
            retweet(clientID, myFeed)

        | QueryTweet ->
            let queryType = listOfQueries.[rnd.Next(listOfQueries.Length)]
            if queryType = "queryHashtag" then
                let hashtag = hashtags.[rnd.Next(hashtags.Length)]
                let queryResult = queryHashtag(clientID, hashtag)
                printf ""
            else
                let mention = "@" + userIDList.[rnd.Next(userIDList.Count)]
                let queryResult = queryMention(clientID, mention)
                printf ""
                
        | FetchFeed ->
            fetchFeed(clientID)


        | Login ->
            let response = login(clientID)
            if response = 1 then
                clientStatus <- true

        | Logout ->
            let response = logout(clientID)
            if response = 1 then
                clientStatus <- false

        | PerformActivities (count) ->
            if activityCounter <> count then

                if clientStatus then         
                    mailbox.Self <! listOfActivities.[activityNum]
                    activityNum <- activityNum + 1

                    if (activityNum = listOfActivities.Length) then
                        activityNum <- 0
                else
                    mailbox.Self <! Login

                activityCounter <- activityCounter + 1
                mailbox.Self <! PerformActivities(count)
        
        | ReceiveMyFeed(newsFeed: List<string * string>) ->
            printfn "ReceiveMyFeed called"
            myFeed <- newsFeed
            mailbox.Self <! ReTweet

        | ReceiveQueryFeed(result) ->
            if((snd(result)).Count <> 0) then
                printfn "[%s: Latest tweet for current query - '%s']: %A....." clientID (fst(result)) (snd(result)).[0]
            else
               printfn "[%s: Latest tweet for current query - '%s']: []" clientID (fst(result))
        
        |_ ->
            printfn ""

        return! loop()
    }
    loop()


let clientAgent (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()

        match msg with
        | StartProcess(numUsers:int, numActivities:int) ->
            mainProcessRef <- sender
            totalClients <- numUsers
            totalActivities <- numActivities

            server <! ("InitServer", "", _followersList, ("", ""), "", "")
            
            timer.Start()
            for i = 1 to numUsers do
                let userKey = "user" + string(i)
                let clientRef = spawn system (userKey) clientMaster
                actorMap.Add(userKey, clientRef)
                userIDList.Add(userKey)
                clientRef <! Register(userKey)
                userActorsList.Add(clientRef)
        
        | CallForSubscription ->
            let mutable count = 1

            timer.Start()
            for userActor in userActorsList do
                userActor <! Subscribe(count)
                count <- count + 1

        | InitiateActivity ->
            printfn "****************************************************************************"
            printfn "Client Activities Started..."
           
            timer.Start()
  
            for userActor in userActorsList do
                userActor <! PerformActivities(totalActivities)
                            
        | TrackActivityCompletion ->
            activityCompletedCount <- activityCompletedCount + 1
            if activityCompletedCount = totalClients then
                mainProcessRef <! "All Tasks Completed"
        
        | GetServerTasksCount ->
             let task = server <? ("GetServerTasksCount", "", _followersList, ("", ""), "", "")
             let response = Async.RunSynchronously(task)
             serverTasksCount <- fst(response)
             serverTotalTime <- snd(response)

             mainProcessRef <! "All Tasks Completed"

        return! loop()
    }
    loop()

clientAgentRef <- spawne system "clientAgent" <@ (clientAgent) @> []

//**** Callback Agent *****///
let callBackAgent (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()

        let mutable myFeed = ("", new List<string * string> ())
        let mutable queryFeed = ("", ("", new List<string * string>()))

        match msg with
        | (task, response, tweetObj, feedTuple, queryResult) when task = "RegisterUser" ->
            if response = 1 then
                activityCompletedCount <- activityCompletedCount + 1
           
            if activityCompletedCount = totalClients then
                printfn "Users registration completed"
                registrationTime <- timer.ElapsedMilliseconds
                timer.Stop()
                timer.Reset()
                activityCompletedCount <- 0
                clientAgentRef <! CallForSubscription

        | (task, response, tweetObj, feedTuple, queryResult) when task = "Subscribe" ->
            if response = 1 then
                activityCompletedCount <- activityCompletedCount + 1
            
            if activityCompletedCount = totalClients then
                printfn "Users zipf subscription Completed"
                zipfSubscriptionTime <- timer.ElapsedMilliseconds
                timer.Stop()
                timer.Reset()
                activityCompletedCount <- 0
                clientAgentRef <! InitiateActivity
        
        | (task, response, tweetObj, feedTuple, queryResult) when task = "TweetLive" ->
            printfn "[LiveTweet] %s: %s" (fst(tweetObj)) (snd(tweetObj))
         
        | (task, response, tweetObj, feedTuple, queryResult) when task = "FetchFeed" ->
            printfn "FetchFeed Callback"
            myFeed <- feedTuple
            let userPath = "/user/" + fst(feedTuple)  
            system.ActorSelection(userPath) <! ReTweet
        
        | (task, response, _, _, queryResult) when task = "QueryFeed" ->
            queryFeed <- queryResult
            let userPath = "/user/" + fst(queryResult)
            let user = fst(queryResult)
            system.ActorSelection(userPath) <! ReceiveQueryFeed(snd(queryResult))

        | (task, response, tweetObj, feedTuple, queryResult) when task = "PerformActivity" ->
             if response = 1 then
                activityCompletedCount <- activityCompletedCount + 1

             if activityCompletedCount >= (totalClients * totalActivities) then
                totalActivityTime <- timer.ElapsedMilliseconds          
                clientAgentRef <! GetServerTasksCount
                timer.Stop()
                timer.Reset()
        
        |_ ->
            printfn "Unknown Task"
        

        return! loop()
    }
    loop()

callBackAgentRef <- spawne system "CallBackAgent" <@ (callBackAgent) @> []