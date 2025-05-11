using Mindee.Extensions.DependencyInjection;
using TTBot.Bot;
using TTBot.Options;
using TTBot.Services;

var builder = WebApplication.CreateBuilder(args);
/* 
            **TO DO**
    -Display message about getting passport and vehicle data +
    -Fix problem with not getting data from Mindee (for car data) +
    -Display collected data to check whether it is correct +
    -Payment
    -Aggrement on payment
    -Insurance policy info (should be accepted by user)
    -Check on correctness (what if passport and vehicle were changed places)
    -???Generate text using OpenAI???
    -Comment most essential parts of the code
 */
// Bind Telegram options from configuration

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddMindeeClient();

builder.Services.AddSingleton<PhotoProcessor>();

// Register Telegram bot background service
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
