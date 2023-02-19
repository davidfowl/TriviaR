## TriviaR

A sample showing how to use client results in SignalR to build a turn based game.

This repository has 3 projects:
- [TriviaR](TriviaR) - The server side
- [TriviaR.Web](TriviaR.Web) - A Blazor WASM based client for the game
- [TriviaR.Console](TriviaR.Console) - A console based client for the game

Each game requires 4 players to start, the trivia game is 5 questions and players get 20 seconds to answer each question (these values are all configurable). Trivia questions are retrived from the https://the-trivia-api.com/ API.

This game requires [.NET 7](https://dotnet.microsoft.com/en-us/download). Run the TriviaR application to play the game, and open up 4 browser windows to join 4 players to the game.
