using Microsoft.Extensions.Options;
using Mindee;
using Mindee.Http;
using Mindee.Input;
using Mindee.Product.Generated;
using Mindee.Product.Passport;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TTBot.MindeeData;
using TTBot.Options;

namespace TTBot
{
    public class TelegramBotService : BackgroundService
    {
        private readonly TelegramOptions _telegramOptions;
        private readonly MindeeClient _mindeeClient;

        private static TelegramBotClient _botClient;

        private PassportData? passportInfo;
        private VehicleData? vehicleInfo;


        private const string _startMessage = """
                <b><u>Hello. I'm a car insurance bot</u></b>
                My purpose is to assist you with
                buying an insurance for your car
                Plese type /send to get futher instructions
            """;

        private const string _usageMessage = """
                <b><u>Bot menu</u></b>:
                /start  - start conversation
                /send   - send a photo of you document
            """;

        private const string _sendPhotoMessage = """
                Currently you have to upload 2 photos:
                1) Your passport data
                2) Your vehicle identification document
            """;

        public TelegramBotService(IOptions<TelegramOptions> telegramOptions, MindeeClient mindeeClient)
        {
            _telegramOptions = telegramOptions.Value;
            _mindeeClient = mindeeClient;
            _botClient = new TelegramBotClient(_telegramOptions.Token);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var me = await _botClient.GetMe(stoppingToken);
            Console.WriteLine($"Bot @{me.Username} is running...");

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                cancellationToken: stoppingToken
            );

            // Keep the background service alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            cancellationToken.ThrowIfCancellationRequested();

            if (update.Message?.Photo != null && update.Message.Photo.Any())
            {
                await SavePhoto(update.Message);
                return;
            }

            await (update switch
            {
                { Message: { } message } => OnMessage(message),
                { EditedMessage: { } message } => OnMessage(message),
                { CallbackQuery: { } callbackQuery } => HandleCallbackQuery(callbackQuery),
                _ => UnknownUpdateHandlerAsync(update)
            });
        }

        private async Task HandleCallbackQuery(CallbackQuery callbackQuery)
        {

            if (callbackQuery.Message == null)
                return;

            string userResponse = callbackQuery.Data;

            await _botClient.EditMessageReplyMarkup(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                replyMarkup: null
            );

            string responseMessage = userResponse switch
            {
                "passportCorrect" => """
                Passport data was confirmed
                Please send your vehicle identification document photo
                """,

                "passportIncorrect" => """
                Passport data wasn't confirmed
                Please send your passport photo one more time
                """,

                "vehicleCorrect" => """
                Vehicle data was confirmed
                """,

                "vehicleIncorrect" => """
                Passport data wasn't confirmed
                Please send your vehicle identification document photo one more time
                """,

                _ => "Unknown response."
            };

            await _botClient.SendMessage(
                chatId: callbackQuery.Message.Chat.Id,
                text: responseMessage,
                parseMode: ParseMode.Html
            );

            await _botClient.AnswerCallbackQuery(callbackQuery.Id);
        }


        private async Task SavePhoto(Message channelPost)
        {
            if (channelPost is null) return;

            var photoSize = channelPost.Photo.Last();
            string fileId = photoSize.FileId;

            var file = await _botClient.GetFile(fileId);

            using var memoryStream = new MemoryStream();
            await _botClient.DownloadFile(file.FilePath!, memoryStream);
            memoryStream.Position = 0;

            if (passportInfo is null)
            {
                await GetThePassportInfoFromThePhoto(memoryStream, channelPost);
            }
            else
            {
                await GetTheVehicleInfoFromThePhoto(memoryStream, channelPost);
            }
        }

        private async Task OnMessage(Message message)
        {
            Console.WriteLine($"Received message from {message.Chat.Id}: {message.Text}");

            if (message.Text is not { } messageText)
            {
                return;
            }

            await (messageText.Split(' ')[0] switch
            {
                "/start" => StartMessage(message),
                "/send" => SendPhotoMessage(message),
                _ => Usage(message)
            });
        }

        private async Task Usage(Message message)
        {
            await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: _usageMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown uopdate type: {update.Type}");
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Telegram Bot Error: {exception.Message}");
            return Task.CompletedTask;
        }

        private async Task SendPhotoMessage(Message message)
        {

            await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: _sendPhotoMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task StartMessage(Message message)
        {
            await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: _startMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task GetThePassportInfoFromThePhoto(MemoryStream memoryStream, Message message)
        {
            var inputSource = new LocalInputSource(memoryStream, "passport.jpg");

            var response = await _mindeeClient.ParseAsync<PassportV1>(inputSource);

            passportInfo = new PassportData
            {
                FistName = response.Document.Inference.Prediction.GivenNames.FirstOrDefault()?.Value ?? "N/A",
                LastName = response.Document.Inference.Prediction.Surname.Value ?? "N/A"
            };

            Console.WriteLine(response.Document.Inference.Prediction.ToString());

            string passportInfoString = $"""
                <b><u>Passport Data</u></b>
                First name: {passportInfo.FistName}
                Surname: {passportInfo.LastName}
            """;

            await _botClient.SendMessage(chatId: message.Chat.Id,
                     text: passportInfoString,
                     parseMode: ParseMode.Html);

            InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "passportCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "passportIncorrect")]
                ]);

            var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
            await _botClient.SendMessage(message.Chat.Id, "Is it correct?", replyMarkup: inlineKeyboard);
            //_botClient.AnswerCallbackQuery(IsPassportDataCorrect);
        }

        private async Task GetTheVehicleInfoFromThePhoto(MemoryStream memoryStream, Message message)
        {
            var inputSource = new LocalInputSource(memoryStream, "passport.jpg");

            CustomEndpoint endpoint = new CustomEndpoint(
                endpointName: "car_registration_license",
                accountName: "Bernkast"
            );

            dynamic response = await _mindeeClient.EnqueueAndParseAsync<GeneratedV1>(inputSource, endpoint);

            System.Console.WriteLine(response.Document.Inference.Prediction.ToString());

            dynamic prediction = response.Document.Inference.Prediction;

            // Option 1: Direct access (if you're sure the structure matches)
            vehicleInfo.Make = response.Document.Inference.Pages[0].Prediction.vehicle_make.Value;
            vehicleInfo.Model = response.Document.Inference.Pages[0].Prediction.vehicle_model.Value;

            string vehicleInfoString = $"""
                <b><u>Vehicle Data</u></b>
                Make: {vehicleInfo.Make}
                Model: {vehicleInfo.Model}
            """;

            await _botClient.SendMessage(chatId: message.Chat.Id,
                     text: vehicleInfoString,
                     parseMode: ParseMode.Html);

            InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "vehicleCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "vehicleIncorrect")]
                ]);

            var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
            await _botClient.SendMessage(message.Chat.Id, "Is it correct?", replyMarkup: inlineKeyboard);
        }
    }
}
