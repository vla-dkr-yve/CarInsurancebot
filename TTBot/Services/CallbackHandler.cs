using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using TTBot.MindeeData;
using TTBot.Session;
using TTBot.Constants;

namespace TTBot.Services
{
    public static class CallbackHandler
    {
        public static async Task<string> HandleAsync(CallbackQuery query, UserSession session)
        {
            if (query.Message == null || query.Data == null)
                return String.Empty;

            string userResponse = query.Data;
            string responseMessage = string.Empty;

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
                    session.PassportInfo = new PassportData();
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
                    session.VehicleInfo = new VehicleData();
                    responseMessage = """
                        Vehicle data wasn't confirmed
                        Please send your vehicle identification document photo one more time
                    """;
                    break;
                case "paymentAgreed":
                    session.IsPaymentConfirmed = true;
                    responseMessage = """
                        Payment confirmed
                    """;
                    break;
                case "paymentDisagreed":
                    session.IsPaymentConfirmed = false;
                    responseMessage = $"""
                        Sorry, but currently the only available price
                        is {BotConstants._insurancePrice}$
                    """;
                    break;
                default:
                    responseMessage = """
                        Unknown
                    """;
                    break;
            }

            return responseMessage;
        }
    }
}
