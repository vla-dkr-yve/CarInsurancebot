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
        public static async Task<string> RequestPolicyGeneration(Message message, UserSession session, IConfiguration configuration)
        {
            string policyRequest = $"Instruction: Do not include disclaimers, introductions, or explanations. Only output the requested policy content. " +
                $"Generate very short insurance policy document on name {session.PassportInfo.FirstName} {session.PassportInfo.LastName} " +
                $"on the vehicle {session.VehicleInfo.Model} made by {session.VehicleInfo.Make}, insurance price is {BotConstants._insurancePrice}.";

            var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configuration.GetValue<string>("Groq:ApiKey"));

            var requestBody = new
            {
                model = "llama3-8b-8192",
                messages = new[]
                {
            new { role = "system", content = "You are a helpful assistant that generates insurance policy documents." },
            new { role = "user", content = policyRequest }
        }
            };

            var response = await httpClient.PostAsJsonAsync("https://api.groq.com/openai/v1/chat/completions", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Error from Groq: {error}";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                             .GetProperty("choices")[0]
                             .GetProperty("message")
                             .GetProperty("content")
                             .GetString();

            return content;
        }
    }
}
