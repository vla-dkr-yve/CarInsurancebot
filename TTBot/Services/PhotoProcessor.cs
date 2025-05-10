using Mindee.Input;
using Mindee.Product.Passport;
using Mindee;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using TTBot.Session;
using TTBot.MindeeData;
using Mindee.Http;
using Mindee.Product.Generated;

namespace TTBot.Services
{
    public class PhotoProcessor
    {
        private static MindeeClient _mindeeClient;

        public PhotoProcessor(MindeeClient mindeeClient)
        {
            _mindeeClient = mindeeClient;
        }

        public static async Task<string> GetThePassportInfoFromThePhotoAsync(MemoryStream memoryStream, UserSession session)
        {
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

            return passportInfoString;
        }

        public static async Task<string> GetTheVehicleInfoFromThePhotoAsync(MemoryStream memoryStream, UserSession session)
        {
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

            return vehicleInfoString;
        }
    }
}
