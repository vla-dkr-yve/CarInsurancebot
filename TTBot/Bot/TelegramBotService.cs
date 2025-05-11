using Microsoft.Extensions.Options;
using Mindee;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TTBot.Constants;
using TTBot.MindeeData;
using TTBot.Options;
using TTBot.Services;

namespace TTBot.Bot
{
    public class TelegramBotService : BackgroundService
    {
        private readonly TelegramOptions _telegramOptions;
        private TelegramBotClient BotClient { get; init; }

        private PhotoProcessor _photoProcessor;

        public TelegramBotService(IOptions<TelegramOptions> telegramOptions, MindeeClient mindeeClient, PhotoProcessor photoProcessor)
        {
            _telegramOptions = telegramOptions.Value;
            BotClient = new TelegramBotClient(_telegramOptions.Token);
            _photoProcessor = photoProcessor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var me = await BotClient.GetMe(stoppingToken);
            Console.WriteLine($"Bot @{me.Username} is running...");

            BotClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                cancellationToken: stoppingToken
            );

            // Keep the background service alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        // Handles incoming updates from Telegram
        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            cancellationToken.ThrowIfCancellationRequested();

            // If the message contains a photo, handle it
            if (update.Message?.Photo != null && update.Message.Photo.Any())
            {
                await SavePhoto(update.Message);
                return;
            }

            // Handle other update types using pattern matching
            await (update switch
            {
                { Message: { } message } => OnMessage(message),
                { EditedMessage: { } message } => OnMessage(message),
                { CallbackQuery: { } callbackQuery } => HandleCallbackQuery(callbackQuery),
                _ => UnknownUpdateHandlerAsync(update)
            });
        }

        // Handles inline keyboard button callback queries
        private async Task HandleCallbackQuery(CallbackQuery callbackQuery)
        {
            var session = UserSessionManager.GetOrCreateSession(callbackQuery.Message.Chat.Id);

            var responseMessage = await CallbackHandler.HandleAsync(callbackQuery, session);

            // Removes the inline keyboard from the chat
            await BotClient.EditMessageReplyMarkup(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                replyMarkup: null
            );

            await BotClient.SendMessage(
                chatId: callbackQuery.Message.Chat.Id,
                text: responseMessage!,
                parseMode: ParseMode.Html
            );

            if (session.IsPassportConfirmed && session.IsVehicleConfirmed && !session.IsPaymentConfirmed)
            {
                await AskForPayment(callbackQuery.Message);
            }

            if (session.IsPassportConfirmed && session.IsVehicleConfirmed && session.IsPaymentConfirmed)
            {
                await PolicyGeneration(callbackQuery.Message);
            }

            await BotClient.AnswerCallbackQuery(callbackQuery.Id);
        }

        //Processes an uploaded photo
        private async Task SavePhoto(Message channelPost)
        {
            if (channelPost is null) return;
            
            var session = UserSessionManager.GetOrCreateSession(channelPost.Chat.Id);
            
            var photoSize = channelPost.Photo?.Last();
            string fileId = photoSize.FileId;
            
            var file = await BotClient.GetFile(fileId);
            
            using var memoryStream = new MemoryStream();
            await BotClient.DownloadFile(file.FilePath!, memoryStream);
            memoryStream.Position = 0;
            
            if (session.PassportInfo.FirstName == String.Empty || session.PassportInfo.LastName == String.Empty)
            {
                var response = await _photoProcessor.GetThePassportInfoFromThePhotoAsync(memoryStream, session);

                await BotClient!.SendMessage(
                chatId: channelPost.Chat.Id,
                text: response,
                parseMode: ParseMode.Html
                );

                if (response == BotConstants._notAbleToProcessThePhoto) return;

                InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "passportCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "passportIncorrect")]
                ]);

                await BotClient!.SendMessage(
                chatId: channelPost.Chat.Id,
                text: "Is passport data correct?",
                replyMarkup: inlineKeyboard,
                parseMode: ParseMode.Html
                );
            }
            else if (session.VehicleInfo.Make == String.Empty || session.VehicleInfo.Model == String.Empty)
            {
                var response = await _photoProcessor.GetTheVehicleInfoFromThePhotoAsync(memoryStream, session);

                await BotClient!.SendMessage(
                chatId: channelPost.Chat.Id,
                text: response,
                parseMode: ParseMode.Html
                );

                if (response == BotConstants._notAbleToProcessThePhoto) return;

                InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "vehicleCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "vehicleIncorrect")]
                ]);

                await BotClient!.SendMessage(
                chatId: channelPost.Chat.Id,
                text: "Is vehicle data correct?",
                replyMarkup: inlineKeyboard,
                parseMode: ParseMode.Html
                );
            }
            else
            {
                await BotClient!.SendMessage(
                chatId: channelPost.Chat.Id,
                text: BotConstants._photoesAllreadyMade,
                parseMode: ParseMode.Html
                );
            }
        }

        // Handles incoming text messages
        private async Task OnMessage(Message message)
        {
            Console.WriteLine($"Received message from {message.Chat.Id}: {message.Text}");

            if (message.Text is not { } messageText)
            {
                return;
            }

            // Route commands to the appropriate handler
            await (messageText.Split(' ')[0] switch
            {
                "/start" => StartMessage(message),
                "/menu" => MenuMessage(message),
                "/send" => SendPhotoMessage(message),
                "/restart" => RemoveVehicleAndPassportData(message),
                _ => Usage(message)
            });
        }

        private async Task MenuMessage(Message message)
        {
            await BotClient.SendMessage(chatId: message.Chat.Id,
                    text: BotConstants._usageMessage,
                    parseMode: ParseMode.Html);
        }

        // Clears user session data and flags
        private async Task RemoveVehicleAndPassportData(Message message)
        {
            var session = UserSessionManager.GetOrCreateSession(message.Chat.Id);

            session.PassportInfo = new PassportData();
            session.VehicleInfo = new VehicleData();

            session.IsPassportConfirmed = false;
            session.IsVehicleConfirmed = false;
            session.IsPaymentConfirmed = false;


            await BotClient.SendMessage(chatId: message.Chat.Id,
                    text: BotConstants._removeDataMessage,
                    parseMode: ParseMode.Html);
        }

        private async Task Usage(Message message)
        {
            await BotClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: BotConstants._usageMessage,
                    parseMode: ParseMode.Html
             );
        }

        // Handles unexpected update types
        private async Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            await Task.CompletedTask;
        }

        // Logs errors encountered during update handling
        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Telegram Bot Error: {exception.Message}");
            return Task.CompletedTask;
        }

        private async Task SendPhotoMessage(Message message)
        {
            await BotClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: BotConstants._sendPhotoMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task StartMessage(Message message)
        {
            await BotClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: BotConstants._startMessage,
                    parseMode: ParseMode.Html
             );
        }

        // Asks user to confirm the payment step
        private async Task AskForPayment(Message message)
        {
            var session = UserSessionManager.GetOrCreateSession(message.Chat.Id);

            string _paymentMessage = $"""
                Current price for car insurance is {BotConstants._insurancePrice}$
                Do you agree for this price?
            """;

        InlineKeyboardMarkup inlineKeyboard = new(
            [
                [InlineKeyboardButton.WithCallbackData("Yes, I agree", "paymentAgreed")],
                [InlineKeyboardButton.WithCallbackData("No, I disagree", "paymentDisagreed")]
            ]);

            await BotClient.SendMessage(message.Chat.Id, _paymentMessage, replyMarkup: inlineKeyboard);
        }

        // Sends the final insurance policy response
        private async Task PolicyGeneration(Message message)
        {
            var session = UserSessionManager.GetOrCreateSession(message.Chat.Id);

            // Simulate "typing..." while waiting for policy generation
            var typingCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!typingCts.IsCancellationRequested)
                {
                    await BotClient.SendChatAction(message.Chat.Id, ChatAction.Typing);
                    await Task.Delay(4000, typingCts.Token);
                }
            });

            var response = await PolicyGenerator.RequestPolicyGeneration(message, session);

            typingCts.Cancel();
            await BotClient.SendMessage(message.Chat.Id, response);

        }
    }
}
