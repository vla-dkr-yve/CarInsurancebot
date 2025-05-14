# CarInsuranceBot
CarInsurancebot is a telegram bot that is able to read your passport and vehicle registration data through MindeeAPI and generate policy using AI.
# Dependencies
### Setting telegram bot and getting the API key
To be able to make your bot firstly you have to talk to @BotFather on Telegram create a new bot and acquire the bot token.
Then install package for working with it
```
dotnet add package Telegram.Bot
```
### Setting MindeeAPI
To made bot able to "read" data from photo was used MindeeAPI. In this project 2 API's are used: first for getting data from the passport and second (custom one maid by me) to get the data from vehicle registration document. Getting API key can be made on the following site https://developers.mindee.com/docs/create-api-key.
```
dotnet add package Mindee
```
### Setting AI for generating policy
As soon as user confirms everything bot sends an HTTPRequest to GroqAPI in order to generate the insurance policy using pre-made prompt
### General
After acquiring keys they should be put into appsetings.json / secrets / any other place that can be get from
## Bot showcase
Video showcase of bot workflow and interactions with user: https://youtu.be/bAgI6V2Xn3s

# Bot workflow
This bot has a few states depending on the user's input.
  - Displaying greeting after /start command
  - Displaying menu with all commands after /menu command or any command/text that bot didn't expect to get
  - Showing requirements for getting the insurance (2 photoes)
  - Removing saved data gotten from photoes using /restart
  - Reading the data from the photoes and asking the for confirmation if data is correct
  - If data from both photoes was accepted bot tells user about the price and whether he accept it, if user disagrees then bot explain that it is not possible to change the price
  - If user agrees than bot starts generating the insurance policy using AI
