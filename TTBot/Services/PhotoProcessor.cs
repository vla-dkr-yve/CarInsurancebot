using Mindee.Input;
using Mindee.Product.Passport;
using Mindee;
using TTBot.Session;
using TTBot.MindeeData;
using Mindee.Http;
using Mindee.Product.Generated;
using TTBot.Constants;

namespace TTBot.Services
{
    public class PhotoProcessor
    {
        private MindeeClient _mindeeClient;

        public PhotoProcessor(MindeeClient mindeeClient)
        {
            _mindeeClient = mindeeClient;
        }

        public async Task<string> GetThePassportInfoFromThePhotoAsync(MemoryStream memoryStream, UserSession session)
        {
            var inputSource = new LocalInputSource(memoryStream, "passport.jpg");

            var response = await _mindeeClient.ParseAsync<PassportV1>(inputSource);

            session.PassportInfo = new PassportData
            {
                FirstName = response.Document.Inference.Prediction.GivenNames.FirstOrDefault()?.Value ?? "Undefined",
                LastName = response.Document.Inference.Prediction.Surname.Value ?? "Undefined"
            };

            string passportInfoString = String.Empty;

            if (session.PassportInfo.FirstName == "Undefined" || session.PassportInfo.LastName == "Undefined")
            {
                session.PassportInfo = new PassportData();
                passportInfoString = BotConstants._notAbleToProcessThePhoto;
            }
            else
            {
                passportInfoString = $"""
                    <b><u>Passport Data</u></b>
                    First name: {session.PassportInfo.FirstName}
                    Surname: {session.PassportInfo.LastName}
                """;
            }

            return passportInfoString;
        }

        public async Task<string> GetTheVehicleInfoFromThePhotoAsync(MemoryStream memoryStream, UserSession session)
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

            var vehicleInfoString = String.Empty;

            if (make == "Undefined" || model == "Undefined")
            {
                session.VehicleInfo = new VehicleData();
                vehicleInfoString = BotConstants._notAbleToProcessThePhoto;
            }
            else
            {
                session.VehicleInfo = new VehicleData
                {
                    Make = make,
                    Model = model
                };
            }

            vehicleInfoString = $"""
                <b><u>Vehicle Data</u></b>
                Make: {session.VehicleInfo.Make}
                Model: {session.VehicleInfo.Model}
            """;

            return vehicleInfoString;
        }
    }
}
