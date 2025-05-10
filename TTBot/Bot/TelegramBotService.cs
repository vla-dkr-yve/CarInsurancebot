using Microsoft.Extensions.Options;
using Mindee;
using Mindee.Http;
using Mindee.Input;
using Mindee.Product.Generated;
using Mindee.Product.NutritionFactsLabel;
using Mindee.Product.Passport;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TTBot.Constants;
using TTBot.MindeeData;
using TTBot.Options;
using TTBot.Services;
using TTBot.Session;

namespace TTBot.Bot
{
    public class TelegramBotService : BackgroundService
    {
        private readonly TelegramOptions _telegramOptions;
        private TelegramBotClient BotClient { get; init; }

        public TelegramBotService(IOptions<TelegramOptions> telegramOptions, MindeeClient mindeeClient)
        {
            _telegramOptions = telegramOptions.Value;
            BotClient = new TelegramBotClient(_telegramOptions.Token);
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
            var session = UserSessionManager.GetOrCreateSession(callbackQuery.Message.Chat.Id);

            var responseMessage = await CallbackHandler.HandleAsync(callbackQuery, session);

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


        private async Task SavePhoto(Message channelPost)
        {
            if (channelPost is null) return;

            var session = UserSessionManager.GetOrCreateSession(channelPost.Chat.Id);

            var photoSize = channelPost.Photo!.Last();
            string fileId = photoSize.FileId;

            var file = await BotClient!.GetFile(fileId);

            using var memoryStream = new MemoryStream();
            await BotClient!.DownloadFile(file.FilePath!, memoryStream);
            memoryStream.Position = 0;

            if (session.PassportInfo is null)
            {
                await PassportInfoFromThePhoto(memoryStream, channelPost);
            }
            else if (session.VehicleInfo is null)
            {
                await VehicleInfoFromThePhoto(memoryStream, channelPost);
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
            await BotClient.SendMessage(chatId: message.Chat.Id,
                    text: BotConstants._usageMessage,
                    parseMode: ParseMode.Html);
        }

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

        private async Task PassportInfoFromThePhoto(MemoryStream memoryStream, Message message)
        {
            var session = UserSessionManager.GetOrCreateSession(message.Chat.Id);

            var inputSource = new LocalInputSource(memoryStream, "passport.jpg");

            var passportInfoString = await PhotoProcessor.GetThePassportInfoFromThePhotoAsync(memoryStream, session);

            await BotClient.SendMessage(chatId: message.Chat.Id,
                     text: passportInfoString,
                     parseMode: ParseMode.Html);

            InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "passportCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "passportIncorrect")]
                ]);

            var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
            await BotClient.SendMessage(message.Chat.Id, "Is it correct?", replyMarkup: inlineKeyboard);
        }

        private async Task VehicleInfoFromThePhoto(MemoryStream memoryStream, Message message)
        {
            var session = UserSessionManager.GetOrCreateSession(message.Chat.Id);

            var vehicleInfoString = await PhotoProcessor.GetTheVehicleInfoFromThePhotoAsync(memoryStream, session);

            await BotClient.SendMessage(chatId: message.Chat.Id,
                     text: vehicleInfoString,
                     parseMode: ParseMode.Html);

            InlineKeyboardMarkup inlineKeyboard = new(
                [
                    [InlineKeyboardButton.WithCallbackData("Yes", "vehicleCorrect")],
                    [InlineKeyboardButton.WithCallbackData("No", "vehicleIncorrect")]
                ]);

            var button = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode");
            await BotClient.SendMessage(message.Chat.Id, "Is it correct?", replyMarkup: inlineKeyboard);
        }

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

        private async Task PolicyGeneration(Message message)
        {
            var session = UserSessionManager.GetOrCreateSession(message.Chat.Id);

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
