#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open System.Collections.Generic

let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            loglevel: ERROR
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = localhost
                port = 7001
        }")

let system = ActorSystem.Create("RemoteClient", configuration)
let server = system.ActorSelection("akka.tcp://RemoteActorFactory@localhost:9001/user/SAgent")

//Defualt Variables
let _userID = ""
let _followersList = new List<string>()
let _tweetObj = ("", "")
let _hashtag = ""
let _mention = ""

let rnd = Random()
let mutable testUser1 = ""
let mutable testUser2 = ""
let mutable testUser3 = ""
let testUser1Followers = new List<string>()
let testUser2Followers = new List<string>()

let mutable totalTests = 8
let mutable failedTestCount = 0
let mutable passedTestCount = 0

let setUp() = 
    printfn "Intiating setup...."
    testUser1 <- "user100001"
    testUser2 <- "user100002"
    testUser3 <- "user100003"

    let response1 = Async.RunSynchronously(server <? ("RegisterUser", testUser1, _followersList, ("", ""), "", ""))
    let response2 = Async.RunSynchronously(server <? ("RegisterUser", testUser2, _followersList, ("", ""), "", ""))
    let response3 = Async.RunSynchronously(server <? ("RegisterUser", testUser3, _followersList, ("", ""), "", ""))

    testUser1Followers.Add(testUser2)
    testUser1Followers.Add(testUser3)
    testUser2Followers.Add(testUser3)

    let response4 = Async.RunSynchronously(server <? ("Subscribe", testUser1, testUser1Followers, ("", ""), "", ""))
    let response5 = Async.RunSynchronously(server <? ("Subscribe", testUser2, testUser2Followers, ("", ""), "", ""))

    printfn "Setup done...."
    

let Test1Registration() = 
    printfn "********* Test case 1: User Registration *********"

    let response = Async.RunSynchronously(server <? ("RegisterUser", "userA", _followersList, ("", ""), "", ""))

    if (response = 0) then
        printfn "Failed"
        failedTestCount <- failedTestCount + 1
    else
        let response = Async.RunSynchronously(server <? ("RegisterUser", "userA", _followersList, ("", ""), "", ""))

        if (response = 1) then
            printfn "Failed"
            failedTestCount <- failedTestCount + 1
        else
            printfn "Passed"
            passedTestCount <- passedTestCount + 1    

let Test2LoginAndLogout() = 
    printfn "********* Test case 2: Log-in/Log-out functionality *********"

    let response1 = Async.RunSynchronously(server <? ("LoginUser", "userB", _followersList, ("", ""), "", ""))

    if (response1 = 1) then
        printfn "Failed"
        failedTestCount <- failedTestCount + 1
    else
        let response2 = Async.RunSynchronously(server <? ("RegisterUser", "userB", _followersList, ("", ""), "", ""))

        let response3  = Async.RunSynchronously(server <? ("LogoutUser", "userB", _followersList, ("", ""), "", ""))

        let isLoggedIn = Async.RunSynchronously(server <? ("GetUserStatus", "userB", _followersList, ("", ""), "", ""))

        if isLoggedIn then       
            printfn "Failed"     
            failedTestCount <- failedTestCount + 1
        else
            printfn "Passed"
            passedTestCount <- passedTestCount + 1

let Test3Subscribe() = 
    printfn "********* Test case 3: Subscribe functionality *********"

    let response1 = Async.RunSynchronously(server <? ("RegisterUser", "userC", _followersList, ("", ""), "", ""))
    let response2 = Async.RunSynchronously(server <? ("RegisterUser", "userD", _followersList, ("", ""), "", ""))
    
    let temp = new List<string>()
    temp.Add("userD")
    let mutable followers = new List<string>()
    let mutable following = new List<string>()

    let response3 = Async.RunSynchronously(server <? ("Subscribe", "userC", temp, ("", ""), "", ""))

    followers <- Async.RunSynchronously(server <? ("FetchFollowersList", "userC", _followersList, _tweetObj, _hashtag, _mention))
    following <- Async.RunSynchronously(server <? ("FetchFollowingList", "userD", _followersList, _tweetObj, _hashtag, _mention))

    if (followers.Count = 1 && following.Count = 1) then
        printfn "Passed"
        passedTestCount <- passedTestCount + 1    
    else
        printfn "Failed"
        failedTestCount <- failedTestCount + 1


let Test4SendTweet() =
    printfn "********* Test case 4: Send Tweet functionality *********"

    let newTweet = (testUser1, "Hello World")
    let response1 = Async.RunSynchronously(server <? ("SendTweet", _userID, _followersList, newTweet, _hashtag, _mention))

    if (response1 = 0) then
        printfn "Failed"
        failedTestCount <- failedTestCount + 1
    else
        printfn "Passed"
        passedTestCount <- passedTestCount + 1    

let Test5Retweet() = 
    printfn "********* Test case 5: Retweet functionality *********"

    let mutable user2Feed = new List<string * string>()

    user2Feed <- Async.RunSynchronously(server <? ("FetchFeed", testUser2, _followersList, _tweetObj, _hashtag, _mention))

    let tweetObj = user2Feed.[rnd.Next(user2Feed.Count)]
    let response1 = Async.RunSynchronously(server <? ("Retweet", testUser2, _followersList, tweetObj, _hashtag, _mention))

    if response1 = 0 then
        printfn "Failed"
        failedTestCount <- failedTestCount + 1
    else
        printfn "Passed"
        passedTestCount <- passedTestCount + 1    

let Test6FetchFeed() =
    printfn "********* Test case 6: News Feed functionality *********"

    let mutable user1Feed = new List<string * string>()
    let mutable user2Feed = new List<string * string>()
    let mutable user3Feed = new List<string * string>()

    user1Feed <- Async.RunSynchronously(server <? ("FetchFeed", testUser1, _followersList, _tweetObj, _hashtag, _mention))
    user2Feed <- Async.RunSynchronously(server <? ("FetchFeed", testUser2, _followersList, _tweetObj, _hashtag, _mention))
    user3Feed <- Async.RunSynchronously(server <? ("FetchFeed", testUser3, _followersList, _tweetObj, _hashtag, _mention))

    if (user1Feed.Count = 1 && user2Feed.Count = 2 && user3Feed.Count = 2) then
        printfn "Passed"
        passedTestCount <- passedTestCount + 1    
    else
        printfn "Failed"
        failedTestCount <- failedTestCount + 1

let Test7QueryHashTag() = 
    printfn "********* Test case 7: Query Tweet functionality *********"

    let tweetObj1 = (testUser1, "I took Distributed Operating Systems this fall #DOS")
    let tweetObj2 = (testUser2, "I took #DOS in fall 2019")
    let response1 = Async.RunSynchronously(server <? ("SendTweet", _userID, _followersList, tweetObj1, _hashtag, _mention))
    let response2 = Async.RunSynchronously(server <? ("SendTweet", _userID, _followersList, tweetObj2, _hashtag, _mention))

    let hashtag = "#DOS"
    let mutable hashtagTweets = new List<string * string>()
    hashtagTweets <- Async.RunSynchronously(server <? ("QueryHashtag", _userID, _followersList, _tweetObj, hashtag, _mention)) 
     
    if (hashtagTweets.Count = 2) then
        printfn "Passed"
        passedTestCount <- passedTestCount + 1    
    else
        printfn "Failed"
        failedTestCount <- failedTestCount + 1

let Test8QueryMention() = 
    printfn "********* Test case 8: Query Mention functionality *********"

    let tweetObj1 = (testUser1, "Hi @user100003")
    let tweetObj2 = (testUser2, "@user100003 Welcome to Twitter!")
    let response1 = Async.RunSynchronously(server <? ("SendTweet", _userID, _followersList, tweetObj1, _hashtag, _mention))
    let response2 = Async.RunSynchronously(server <? ("SendTweet", _userID, _followersList, tweetObj2, _hashtag, _mention))

    let mention = "@user100003"
    let mutable mentionTweets = new List<string * string>()
    mentionTweets <- Async.RunSynchronously(server <? ("QueryMention", _userID, _followersList, _tweetObj, _hashtag, mention)) 
     
    if (mentionTweets.Count = 2) then
        printfn "Passed"
        passedTestCount <- passedTestCount + 1    
    else
        printfn "Failed"
        failedTestCount <- failedTestCount + 1

let executeTestCases() =
    setUp()
    printfn "Starting tests...."
    for i = 1 to 8 do
        match i with
        | 1 -> Test1Registration()
        | 2 -> Test2LoginAndLogout()
        | 3 -> Test3Subscribe()
        | 4 -> Test4SendTweet()
        | 5 -> Test5Retweet()
        | 6 -> Test6FetchFeed()
        | 7 -> Test7QueryHashTag()
        | 8 -> Test8QueryMention()
        |_ -> printfn "Unmatched"


executeTestCases()
printfn "Tests completed"
printfn "Total Test cases: 8"
printfn "Total passed: %d" passedTestCount
printfn "Total failed: %d" failedTestCount
system.Terminate() |> ignore
