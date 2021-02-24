#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open System.Diagnostics

// remote actor configuration
let config =  
    Configuration.parse
        @"akka {
            log-config-on-start : on
            loglevel : ERROR
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 9001
            }
        }"

// spawn remote actor system
let system = System.create "RemoteActorFactory" config
let timer = Stopwatch()
let echoNew = system.ActorSelection("akka.tcp://RemoteClient@localhost:8001/user/CallBackAgent")
let rnd = Random()


// default variables
let _tweetObj = ("", "")
let _feedTuple = ("", new List<string * string>())
let _queryResult = ("", ("", new List<string * string>()))

// declare function prototypes
type ServerActorMessages = 
    | RegisterUser of string
    | Subscribe of string * List<string>
    | FetchFeed of string
    | SendTweet of (string * string)
    | Retweet of string
    | QueryHashtag of string * string
    | QueryMention of string * string
    | IsUserRegistered of string
    | GetUserStatus of string
    | LoginUser of string
    | LogoutUser of string
    | LogoutUsers of List<string>
    | FetchFollowersList of string
    | FetchFollowingList of string
    | RetweetSyn of string * (string * string)

let ServerActor (mailbox: Actor<_>) =
    let usersTable     = new Dictionary<string, bool>()
    let followersTable = new Dictionary<string, List<string>>()
    let followingTable = new Dictionary<string, List<string>>()
    let newsFeedTable  = new Dictionary<string, List<(string * string)>>()
    let hashtagsTable  = new Dictionary<string, List<(string * string)>>()
    let mentionsTable  = new Dictionary<string, List<(string * string)>>()

    let rec serverActorLoop () = actor {
        
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()

        match message with

        | RegisterUser (userID) ->
            printfn "RegisterUser API called"

            // add a new user only if the user doesnt exit yet
            if usersTable.ContainsKey(userID) then
                sender <! 0
            else
                usersTable.Add(userID, true) 
                newsFeedTable.Add(userID, new List<(string * string)>())
                followersTable.Add(userID, new List<string>())
                followingTable.Add(userID, new List<string>())

                sender <! 1
                
            echoNew <! ("RegisterUser", 1, _tweetObj, _feedTuple, _queryResult)

        | Subscribe(userID, followersList) ->
            printfn "Subscribe API called"

            for user in followersList do
                followersTable.[userID].Add(user)
                followingTable.[user].Add(userID)

            sender <! 1
            echoNew <! ("Subscribe", 1, _tweetObj, _feedTuple, _queryResult)


        | FetchFeed(userID) -> 
            printfn "FetchFeed API called"

            sender <! newsFeedTable.[userID]

            let mutable tempList = new List<string * string>()
            let mutable tempUser = ""
            tempList <- newsFeedTable.[userID]
            tempUser <- userID
            let tempTuple = (tempUser, tempList)
           
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)


        | SendTweet(tweetObj) ->
            printfn "SendTweet API called"

            // extract hashtags and mentions from tweet
            let userID = fst (tweetObj)
            let tweet = snd (tweetObj)
            let mutable mention = ""
            let mutable hashtag = ""
            let mutable i = 0;

            while i < tweet.Length do
                if tweet.[i] = '#' then
                    // i <- i + 1
                    while i < tweet.Length && tweet.[i] <> ' ' do
                        hashtag <- hashtag + (tweet.[i] |> string)
                        i <- i + 1
                elif tweet.[i] = '@' then
                    // i <- i + 1
                    while i < tweet.Length && tweet.[i] <> ' ' do
                        mention <- mention + (tweet.[i] |> string)
                        i <- i + 1

                i <- i + 1

            // if tweet contained hashtags or mentions, add them to db
            if not (isNull hashtag) then
                if hashtagsTable.ContainsKey(hashtag) then
                    hashtagsTable.[hashtag].Add(tweetObj)
                else
                    hashtagsTable.Add(hashtag, new List<(string * string)>())
                    hashtagsTable.[hashtag].Add(tweetObj)

            if not (isNull mention) then
                if mentionsTable.ContainsKey(mention) then
                    mentionsTable.[mention].Add(tweetObj)
                else
                    mentionsTable.Add(mention, new List<(string * string)>())
                    mentionsTable.[mention].Add(tweetObj)

            let found, feed = newsFeedTable.TryGetValue(userID)
            feed.Add(tweetObj)
            
            let followersList = followersTable.[userID]

            for user in followersList do
                let found, feed = newsFeedTable.TryGetValue(user)
                feed.Add(tweetObj)

            sender <! 1
           
            if(usersTable.[userID]) then
                let mutable tweetObj = ("", "")
                tweetObj <- (userID, tweet)
                echoNew <! ("TweetLive", 1, tweetObj, _feedTuple, _queryResult)
            
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)

        
        | Retweet(userID) -> 
            // fetch one of the tweets for userID
            printfn "Retweet API called"

            let mutable newsFeed = new List<string * string>()
            
            newsFeed <- newsFeedTable.[userID]

            if newsFeed.Count <> 0 then
                let tweetObj = newsFeed.[rnd.Next(newsFeed.Count)]
                
                let user = fst (tweetObj)
                let retweet = snd (tweetObj) + " -- Retweeted by " + userID;
                let retweetObj = (user, retweet)

                let found, feed = newsFeedTable.TryGetValue(userID)
                feed.Add(retweetObj)

                let foundx, followersList = followersTable.TryGetValue(userID)

                for user in followersList do
                    let found2, feed2 = newsFeedTable.TryGetValue(user)
                    feed2.Add(retweetObj)

                if(usersTable.[userID]) then
                    echoNew <! ("TweetLive", 1, retweetObj, _feedTuple, _queryResult)

            sender <! 1
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)


        // | Retweet(userID, tweetObj) -> 
        //     printfn "Retweet API called"

        //     let user = fst (tweetObj)
        //     let retweet = snd (tweetObj) + " -- Retweeted by " + userID;
        //     let retweetObj = (user, retweet)

        //     // newsFeedTable.[userID].Add(retweetObj)
        //     let found, feed = newsFeedTable.TryGetValue(userID)
        //     feed.Add(retweetObj)
        //     // newsFeedTable.[userID] <- feed

        //     let followersList = followersTable.[userID]

        //     for user in followersList do
        //         let found, feed = newsFeedTable.TryGetValue(user)
        //         feed.Add(retweetObj)
        //         // newsFeedTable.[user] <- feed

        //     sender <! 1

        //     if(usersTable.[userID]) then
        //         let mutable tweetObj = ("", "")
        //         tweetObj <- (userID, retweet)
        //         echoNew <! ("TweetLive", 1, tweetObj, _feedTuple, _queryResult)

        //     echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)
             
        | QueryHashtag(userID, hashtag) ->
            printfn "QueryHashtag API called"

            let mutable tempList = new List<string * string>()
            let mutable tempUser = ""
            let mutable tempHashtag = ""
            let mutable queryResult = (tempUser, (tempHashtag, tempList))

            if hashtagsTable.ContainsKey(hashtag) then
                sender <! hashtagsTable.[hashtag]
                tempList <- hashtagsTable.[hashtag]
                tempUser <- userID
                tempHashtag <- hashtag
            else
                sender<! new List<(string * string)>()
                queryResult <- (userID, (hashtag, new List<string * string>()))

            echoNew <! ("QueryFeed", 1, _tweetObj, _feedTuple, queryResult)
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)

        | QueryMention(userID, mention) ->
            printfn "QueryMention API called"
            
            let mutable tempList = new List<string * string>()
            let mutable tempUser = ""
            let mutable tempMention = ""
            let mutable queryResult = (tempUser, (tempMention, tempList))
            if mentionsTable.ContainsKey(mention) then
                sender <! mentionsTable.[mention]
                tempList <- mentionsTable.[mention]
                tempUser <- userID
                tempMention <- mention
            else
                sender<! new List<(string * string)>()
                queryResult <- (userID, (mention, new List<string * string>()))
            
            echoNew <! ("QueryFeed", 1, _tweetObj, _feedTuple, queryResult)
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)

        | IsUserRegistered(userID) ->
            if not (usersTable.ContainsKey(userID)) then
                sender <! 0 // user not registered
            else
                sender <! 1 // user registered

        | GetUserStatus(userID) ->
            if not (usersTable.ContainsKey(userID)) then
                sender <! 0 // user does not exits

            let status = usersTable.[userID]

            sender <! status

        | LoginUser(userID) ->
            printfn "LoginUser API called"

            if not (usersTable.ContainsKey(userID)) then
                sender <! 0 // user does not exits

            usersTable.[userID] <- true

            sender <! 1
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)

        | LogoutUser(userID) ->
            printfn "LogoutUser API called"

            usersTable.[userID] <- false

            sender <! 1
            echoNew <! ("PerformActivity", 1, _tweetObj, _feedTuple, _queryResult)

        | LogoutUsers(listOfUsers) ->
            for user in listOfUsers do
                usersTable.[user] <- false

            sender <! 1
        
        | FetchFollowersList(userID) ->
            sender <! followersTable.[userID]

        | FetchFollowingList(userID) ->
            sender <! followingTable.[userID]

        | RetweetSyn(userID, tweetObj) -> 
            // sync utilitty
            let user = fst (tweetObj)
            let retweet = snd (tweetObj) + " -- Retweeted by " + userID;
            let retweetObj = (user, retweet)

            let found, feed = newsFeedTable.TryGetValue(userID)
            feed.Add(retweetObj)
            newsFeedTable.[userID] <- feed

            let followersList = followersTable.[userID]

            for user in followersList do
                let found, feed = newsFeedTable.TryGetValue(user)
                feed.Add(retweetObj)
                newsFeedTable.[user] <- feed

            sender <! 1

        return! serverActorLoop()
    }

    serverActorLoop ()

let ServerActorRef = spawne system "ServerActor" <@ (ServerActor) @> []

let AgentActor (mailbox: Actor<_>) =
    let echoNew = system.ActorSelection("akka.tcp://RemoteClient@localhost:8001/user/CallBackAgent")
    let mutable globalRequestCount = 0
    
    let rec agentActorLoop () = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match message with

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "RegisterUser" ->
                globalRequestCount <- globalRequestCount + 1

                ServerActorRef <! RegisterUser(userID)
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "Subscribe" ->
                globalRequestCount <- globalRequestCount + 1

                ServerActorRef <! Subscribe(userID, listOfUsers)

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "FetchFeed" ->
                globalRequestCount <- globalRequestCount + 1

                ServerActorRef <! FetchFeed(userID)

        // | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "FetchFeedForRetweet" ->
        //         globalRequestCount <- globalRequestCount + 1
        //         let task = ServerActorRef <? FetchFeed(userID)
        //         let response = Async.RunSynchronously(task)
        //         sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "SendTweet" ->
                globalRequestCount <- globalRequestCount + 1
                ServerActorRef <! SendTweet(tweetObj)
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "Retweet" ->
                globalRequestCount <- globalRequestCount + 1
                ServerActorRef <! Retweet(userID)
                        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "QueryHashtag" ->
                globalRequestCount <- globalRequestCount + 1
                ServerActorRef <! QueryHashtag(userID, hashtag)
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "QueryMention" ->
                globalRequestCount <- globalRequestCount + 1
                ServerActorRef <! QueryMention(userID, mention)

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "LoginUser" ->
                globalRequestCount <- globalRequestCount + 1
                ServerActorRef <! LoginUser(userID)

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "LogoutUser" ->
                globalRequestCount <- globalRequestCount + 1
                ServerActorRef <! LogoutUser(userID)
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "GetServerTasksCount" ->
                globalRequestCount <- globalRequestCount + 1
                sender <! (globalRequestCount, timer.ElapsedMilliseconds)
         
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "InitServer" ->
                 timer.Restart()
                 globalRequestCount <- 0
                 globalRequestCount <- globalRequestCount + 1
               
        | _ ->
            printfn "Unknown task!"

        return! agentActorLoop()
    }

    agentActorLoop()

let AgentActorRef = spawne system "TwitterAgent" <@ (AgentActor) @> []

let SAgentActor (mailbox: Actor<_>) =
    
    let rec SAgentActorLoop () = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        printfn "%A" message
        match message with

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "RegisterUser" ->
                let task = ServerActorRef <? RegisterUser(userID)
                let response = Async.RunSynchronously(task)
                sender <! response
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "Subscribe" ->
                let task = ServerActorRef <? Subscribe(userID, listOfUsers)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "FetchFeed" ->
                let task = ServerActorRef <? FetchFeed(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "FetchFeedForRetweet" ->
                let task = ServerActorRef <? FetchFeed(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "SendTweet" ->
                let task = ServerActorRef <? SendTweet(tweetObj)
                let response = Async.RunSynchronously(task)
                sender <! response
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "Retweet" ->
                let task = ServerActorRef <? RetweetSyn(userID, tweetObj)
                let response = Async.RunSynchronously(task)
                sender <! response
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "QueryHashtag" ->
                let task = ServerActorRef <? QueryHashtag(userID, hashtag)
                let response = Async.RunSynchronously(task)
                sender <! response
        
        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "QueryMention" ->
                let task = ServerActorRef <? QueryMention(userID, mention)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "IsUserRegistered" ->
                let task = ServerActorRef <? IsUserRegistered(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "GetUserStatus" ->
                let task = ServerActorRef <? GetUserStatus(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "LoginUser" ->
                let task = ServerActorRef <? LoginUser(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "LogoutUser" ->
                let task = ServerActorRef <? LogoutUser(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "LogoutUsers" ->
                let task = ServerActorRef <? LogoutUsers(listOfUsers)
                let response = Async.RunSynchronously(task)
                sender <! response               

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "FetchFollowersList" ->
                let task = ServerActorRef <? FetchFollowersList(userID)
                let response = Async.RunSynchronously(task)
                sender <! response

        | (task, userID, listOfUsers, tweetObj, hashtag, mention) when task = "FetchFollowingList" ->
                let task = ServerActorRef <? FetchFollowingList(userID)
                let response = Async.RunSynchronously(task)
                sender <! response
        | _ ->
            printfn "Unknown task!"

        return! SAgentActorLoop()
    }

    SAgentActorLoop()

let SAgentActorRef = spawne system "SAgent" <@ (SAgentActor) @> []

printfn "Twitter Server listening on port 9001...."
Console.ReadKey() |> ignore
0


 