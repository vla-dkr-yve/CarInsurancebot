using System.Text.Json;
using System.Text;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using TTBot.Session;
using TTBot.Constants;

namespace TTBot.Services
{
    public static class PolicyGenerator
    {
        public static async Task<string> RequestPolicyGeneration(Message message, UserSession session)
        {
            string PolicyRequest = $"Instruction: Do not include disclaimers, introductions, or explanations. Only output the requested policy content. " +
                $"Generate very short insurance policy document on name {session.PassportInfo.FirstName} {session.PassportInfo.LastName}" +
                $" on the vehicle {session.VehicleInfo.Model} made by {session.VehicleInfo.Make}, insurance price is {BotConstants._insurancePrice}";

            var httpClient = new HttpClient();
            var requestBody = new
            {
                model = "llama3",
                prompt = PolicyRequest,
                stream = true
            };

            using var response = await httpClient.PostAsJsonAsync("http://localhost:11434/api/generate", requestBody);
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var fullText = new StringBuilder();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line != null && line.StartsWith("{"))
                {
                    var chunk = JsonDocument.Parse(line);
                    var part = chunk.RootElement.GetProperty("response").GetString();
                    fullText.Append(part);
                }
            }

            return fullText.ToString();
        }
    }
}
