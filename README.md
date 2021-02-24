# Twitter Engine

* Simulated Twitter Engine with a client-server architecture implemented using AKKA remote actor facility.
* Functionalities implemented:
  * Twitter Engine
  * Twitter Client
  * Twitter Engine and Client in separate processes
  * Register account
  * Subscribe users (Based on Zipf Distribution)
  * User Login and Logout
  * Send tweet (Tweets can have hashtags or mentions)
  * Retweet
  * If the user is connected, deliver the above types of tweets live
  * Query tweet based on mentions and hashtag

* Upon registering and subscribing, each user performs activities such as Send Tweet, Retweet, Query Tweets etc. User logs-in and logs-out intermittently while performing these activities.
* Largest input tested:
  * Number of Users: 1000
  * Number of Activites per User: 10

### To Run the Application:
  * Open a terminal at project folder and start the server by running: \
    &nbsp;&nbsp;&nbsp; dotnet fsi --langversion:preview server.fsx
  * Open another terminal at project folder and start the application by running: \
    &nbsp;&nbsp;&nbsp; ```dotnet fsi --langversion:preview project4_1.fsx arg1 arg2```
     * arg1: Number of Users
     * arg2: Number of Activites per User
