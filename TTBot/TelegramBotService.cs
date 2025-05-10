using Microsoft.Extensions.Options;
using Mindee;
using Mindee.Http;
using Mindee.Input;
using Mindee.Product.Generated;
using Mindee.Product.Passport;
using System.Linq.Expressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TTBot.MindeeData;
using TTBot.Options;
using TTBot.Session;

namespace TTBot
{
    public class TelegramBotService : BackgroundService
    {
        private readonly TelegramOptions _telegramOptions;
        private readonly MindeeClient _mindeeClient;

        private static TelegramBotClient? _botClient;

        private readonly Dictionary<long, UserSession> UserSessions = new();

        private const int _insurancePrice = 100;

        private const string _startMessage = """
                <b><u>Hello. I'm a car insurance bot</u></b>
                My purpose is to assist you with
                buying an insurance for your car
                Plese type /menu to get futher instructions
            """;

        private const string _usageMessage = """
                <b><u>Bot menu</u></b>:
                /start   - start conversation
                /send    - get information about required documents
                /restart - remove send data and fill it one more time
            """;

        private const string _sendPhotoMessage = """
                Currently you have to upload 2 photos:
                1) Your passport data
                2) Your vehicle identification document
            """;

        private const string _removeDataMessage = """
                Your data has been cleared.
                Now you can send it one more time
            """;

        private const string _photoesAllreadyMade = """
                Sorry, but you already field and confirmed your data
                if something is wrong and you want to send
                passport and vehickle data one more time
                please type "/restart"
            """;

        private string _paymentMessage = $"""
                Current price for car insurance is {_insurancePrice}$
                Do you agree for this price?
            """;


        public TelegramBotService(IOptions<TelegramOptions> telegramOptions, MindeeClient mindeeClient)
        {
            _telegramOptions = telegramOptions.Value;
            _mindeeClient = mindeeClient;
            _botClient = new TelegramBotClient(_telegramOptions.Token);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var me = await _botClient!.GetMe(stoppingToken);
            Console.WriteLine($"Bot @{me.Username} is running...");

            _botClient!.StartReceiving(
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

            var session = GetSession(callbackQuery.Message.Chat.Id);

            string userResponse = callbackQuery.Data;
            string responseMessage = null;

            await _botClient!.EditMessageReplyMarkup(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                replyMarkup: null
            );

            switch (userResponse)
            {
                case "passportCorrect":
                    session.IsPassportConfirmed = true;
                    responseMessage = """
                        Passport data was confirmed
                        Please send your vehicle identification document photo
                    """;
                    break;
                case "passportIncorrect":
                    session.IsPassportConfirmed = false;
                    session.PassportInfo = null;
                    responseMessage = """
                        Passport data wasn't confirmed
                        Please send your passport photo one more time
                    """;
                    break;
                case "vehicleCorrect":
                    session.IsVehicleConfirmed = true;
                    responseMessage = """
                        Vehicle data was confirmed
                    """;
                    break;
                case "vehicleIncorrect":
                    session.IsVehicleConfirmed = false;
                    session.VehicleInfo = null;
                    responseMessage = """
                        Vehicle data wasn't confirmed
                        Please send your vehicle identification document photo one more time
                    """;
                    break;
                case "paymentAgreed":
                    responseMessage = """
                        Payment confirmed
                    """;
                    break;
                case "paymentDisagreed":
                    responseMessage = $"""
                        Sorry, but currently the only available price
                        is {_insurancePrice}$
                    """;
                    break;
                default:
                    responseMessage = """
                        Unknown
                    """;
                    break;
            }

            await _botClient!.SendMessage(
                chatId: callbackQuery.Message.Chat.Id,
                text: responseMessage!,
                parseMode: ParseMode.Html
            );

            if (session.IsPassportConfirmed && session.IsVehicleConfirmed)
            {
                AskForPayment(callbackQuery.Message);
            }

            await _botClient!.AnswerCallbackQuery(callbackQuery.Id);
        }


        private async Task SavePhoto(Message channelPost)
        {
            if (channelPost is null) return;

            var session = GetSession(channelPost.Chat.Id);

            var photoSize = channelPost.Photo!.Last();
            string fileId = photoSize.FileId;

            var file = await _botClient!.GetFile(fileId);

            using var memoryStream = new MemoryStream();
            await _botClient!.DownloadFile(file.FilePath!, memoryStream);
            memoryStream.Position = 0;

            if (session.PassportInfo is null)
            {
                await GetThePassportInfoFromThePhoto(memoryStream, channelPost);
            }
            else if(session.VehicleInfo is null)
            {
                await GetTheVehicleInfoFromThePhoto(memoryStream, channelPost);
            }
            else
            {
                await _botClient!.SendMessage(
                chatId: channelPost.Chat.Id,
                text: _photoesAllreadyMade,
                parseMode: ParseMode.Html
                );
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
                "/menu" => MenuMessage(message),
                "/send" => SendPhotoMessage(message),
                "/restart" => RemoveVehicleAndPassportData(message),
                _ => Usage(message)
            });
        }

        private async Task MenuMessage(Message message)
        {
            await _botClient!.SendMessage(chatId: message.Chat.Id,
                    text: _usageMessage,
                    parseMode: ParseMode.Html);
        }

        private async Task RemoveVehicleAndPassportData(Message message)
        {
            var session = GetSession(message.Chat.Id);

            session.PassportInfo = null;
            session.VehicleInfo = null;

            await _botClient!.SendMessage(chatId: message.Chat.Id,
                    text: _removeDataMessage,
                    parseMode: ParseMode.Html);
        }

        private async Task Usage(Message message)
        {
            await _botClient!.SendMessage(
                    chatId: message.Chat.Id,
                    text: _usageMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown uopdate type: {update.Type}");
            await Task.CompletedTask;
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Telegram Bot Error: {exception.Message}");
            return Task.CompletedTask;
        }

        private async Task SendPhotoMessage(Message message)
        {

            await _botClient!.SendMessage(
                    chatId: message.Chat.Id,
                    text: _sendPhotoMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task StartMessage(Message message)
        {
            await _botClient!.SendMessage(
                    chatId: message.Chat.Id,
                    text: _startMessage,
                    parseMode: ParseMode.Html
             );
        }

        private async Task GetThePassportInfoFromThePhoto(MemoryStream memoryStream, Message message)
        {
            var session = GetSession(message.Chat.Id);

            var inputSource = new LocalInputSource(memoryStream, "passport.jpg");

            var response = await _mindeeClient.ParseAsync<PassportV1>(inputSource);

            session.PassportInfo = new PassportData
            {
                FistName = response.Document.Inference.Prediction.GivenNames.FirstOrDefault()?.Value ?? "N/A",
                LastName = response.Document.Inference.Prediction.Surname.Value ?? "N/A"
            };

            Console.WriteLine(response.Document.Inference.Prediction.ToString());

            string passportInfoString = $"""
                <b><u>Passport Data</u></b>
                First name: {session.PassportInfo.FistName}
                Surname: {session.PassportInfo.LastName}
            """;

            await _botClient!.SendMessage(chatId: message.Chat.Id,
                     text: passportInfoString,
                     parseMode: ParseMode.Html);

            InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "passportCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "passportIncorrect")]
                ]);

            var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
            await _botClient!.SendMessage(message.Chat.Id, "Is it correct?", replyMarkup: inlineKeyboard);
            //_botClient.AnswerCallbackQuery(IsPassportDataCorrect);
        }

        private async Task GetTheVehicleInfoFromThePhoto(MemoryStream memoryStream, Message message)
        {
            var session = GetSession(message.Chat.Id);

            var inputSource = new LocalInputSource(memoryStream, "passport.jpg");

            CustomEndpoint endpoint = new CustomEndpoint(
                endpointName: "car_registration_license",
                accountName: "Bernkast"
            );

            dynamic response = await _mindeeClient.EnqueueAndParseAsync<GeneratedV1>(inputSource, endpoint);

            var prediction = response.Document.Inference.Prediction;

            string make = "Undefined";
            string model = "Undefined";

            if (prediction.Fields != null && prediction.Fields.ContainsKey("vehicle_make"))
            {
                var makeField = prediction.Fields["vehicle_make"].AsStringField();
                make = makeField?.Value ?? "Undefined";
            }

            if (prediction.Fields != null && prediction.Fields.ContainsKey("vehicle_model"))
            {
                var modelField = prediction.Fields["vehicle_model"].AsStringField();
                model = modelField?.Value ?? "Undefined";
            }

            session.VehicleInfo = new VehicleData
            {
                Make = make,
                Model = model
            };

            string vehicleInfoString = $"""
                <b><u>Vehicle Data</u></b>
                Make: {session.VehicleInfo.Make}
                Model: {session.VehicleInfo.Model}
            """;

            await _botClient!.SendMessage(chatId: message.Chat.Id,
                     text: vehicleInfoString,
                     parseMode: ParseMode.Html);

            InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "vehicleCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "vehicleIncorrect")]
                ]);

            var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
            await _botClient!.SendMessage(message.Chat.Id, "Is it correct?", replyMarkup: inlineKeyboard);
        }

        private async Task AskForPayment(Message message)
        {
            var session = GetSession(message.Chat.Id);
            if (session.IsPassportConfirmed && session.IsVehicleConfirmed)
            {
                InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes, I agree", "paymentAgreed")],
                    [InlineKeyboardButton.WithCallbackData("No, I disagree", "paymentDisagreed")]
                ]);

                await _botClient!.SendMessage(message.Chat.Id, _paymentMessage, replyMarkup: inlineKeyboard);
            }
        }

        private UserSession GetSession(long chatId)
        {
            if (!UserSessions.TryGetValue(chatId, out var session))
            {
                session = new UserSession();
                UserSessions.Add(chatId, session);
            }

            return session;
        }
    }
}
