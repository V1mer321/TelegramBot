using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelegramBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly TelegramBotService _botService;

        public Worker(ILogger<Worker> logger, TelegramBotService botService)
        {
            _logger = logger;
            _botService = botService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("TelegramBot Service starting at: {time}", DateTimeOffset.Now);
                await _botService.StartBot();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in TelegramBot Service");
                throw;
            }
        }
    }
}
