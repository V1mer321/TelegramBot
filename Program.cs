using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace TelegramBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "TelegramBot Service";
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Add configuration
                    IConfiguration configuration = hostContext.Configuration;

                    // Configure TelegramBotService with settings from appsettings.json
                    services.AddSingleton(provider => new TelegramBotService(
                        configuration["TelegramBot:Token"] ?? throw new InvalidOperationException("Telegram Bot Token is not configured."),
                        configuration["OpenWeatherMap:ApiKey"] ?? throw new InvalidOperationException("OpenWeatherMap API Key is not configured.")
                    ));

                    // Add the background service
                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
