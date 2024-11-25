using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.ReplyMarkups;
using PuppeteerSharp;
using Newtonsoft.Json.Linq;
using System.Text;
using System.IO;

namespace TelegramBot
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _weatherApiKey;
        private IBrowser? _browser;
        private ReplyKeyboardMarkup _mainMenu;

        public TelegramBotService(string botToken, string weatherApiKey)
        {
            _botClient = new TelegramBotClient(botToken);
            _weatherApiKey = weatherApiKey;

            // Initialize main menu
            _mainMenu = new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    new KeyboardButton("🌤 Погода"),
                    new KeyboardButton("🔍 Поиск")
                },
                new[]
                {
                    new KeyboardButton("⏰ Время"),
                    new KeyboardButton("ℹ️ Помощь")
                },
                new[]
                {
                    new KeyboardButton("🔄 Перезапуск")
                }
            })
            {
                ResizeKeyboard = true
            };
        }

        private async Task InitializeBrowser()
        {
            try
            {
                var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
                {
                    Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".local-browser")
                });

                await browserFetcher.DownloadAsync();
                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { 
                        "--no-sandbox", 
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu"
                    }
                });

                Console.WriteLine("Browser initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize browser: {ex.Message}");
                _browser = null;
            }
        }

        public async Task StartBot()
        {
            try
            {
                // Initialize browser in background
                _ = InitializeBrowser();

                var me = await _botClient.GetMeAsync();
                Console.WriteLine($"Hello, I am user {me.Id} and my name is {me.FirstName}.");

                using var cts = new CancellationTokenSource();

                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>()
                };

                _botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    pollingErrorHandler: HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: cts.Token
                );

                Console.WriteLine($"Start listening for @{me.Username}");

                // Instead of waiting for a key press, wait indefinitely
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting bot: {ex.Message}");
                throw;
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { Text: { } messageText } message)
                return;

            var chatId = message.Chat.Id;

            try 
            {
                // Ignore empty messages
                if (string.IsNullOrWhiteSpace(messageText))
                    return;

                // Handle commands and menu buttons
                switch (messageText)
                {
                    case "/start":
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "👋 Привет! Я многофункциональный бот.\n\n" +
                                 "Используйте кнопки меню для быстрого доступа к функциям.\n" +
                                 "Отправьте /help чтобы увидеть список всех доступных команд.\n\n" +
                                 "👨‍💻 Разработчик: Кирилл Лукьянов",
                            replyMarkup: _mainMenu,
                            cancellationToken: cancellationToken);
                        break;

                    case "🔄 Перезапуск":
                    case "/restart":
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "🔄 Перезапускаюсь...",
                            cancellationToken: cancellationToken);
                            
                        // Сбрасываем состояние и переинициализируем компоненты
                        if (_browser != null)
                        {
                            await _browser.CloseAsync();
                            await _browser.DisposeAsync();
                            _browser = null;
                        }
                        await InitializeBrowser();
                        
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "✅ Перезапуск завершен! Бот снова готов к работе.",
                            replyMarkup: _mainMenu,
                            cancellationToken: cancellationToken);
                        break;

                    case "🌤 Погода":
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Введите название города в формате:\n/weather Москва",
                            cancellationToken: cancellationToken);
                        break;

                    case "🔍 Поиск":
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Просто напишите свой вопрос, и я найду ответ!",
                            cancellationToken: cancellationToken);
                        break;

                    case "⏰ Время":
                        await SendCurrentTime(chatId, cancellationToken);
                        break;

                    case "ℹ️ Помощь":
                        await SendHelpMessage(chatId, cancellationToken);
                        break;

                    default:
                        // Handle specific commands
                        if (messageText.StartsWith("/"))
                        {
                            if (messageText.StartsWith("/weather"))
                            {
                                var parts = messageText.Split(' ', 2);
                                if (parts.Length < 2)
                                {
                                    await _botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: "Пожалуйста, укажите город. Пример: /weather Москва",
                                        cancellationToken: cancellationToken);
                                    return;
                                }
                                await SendWeatherInfo(chatId, parts[1], cancellationToken);
                            }
                            else if (messageText == "/time")
                            {
                                await SendCurrentTime(chatId, cancellationToken);
                            }
                            else if (messageText == "/help")
                            {
                                await SendHelpMessage(chatId, cancellationToken);
                            }
                        }
                        // Handle any other text as a search query
                        else
                        {
                            await SearchWeb(chatId, messageText, cancellationToken);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"Произошла ошибка: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Error occurred: {exception.Message}");
            return Task.CompletedTask;
        }

        private string GetFunnyErrorMessage()
        {
            var funnyMessages = new[]
            {
                "🌪 Ой! Кажется, метеоролог заснул на рабочем месте... Попробуйте разбудить его еще раз!",
                "🌡 Градусник сломался, термометр убежал, барометр взял выходной... Давайте попробуем еще раз?",
                "☔ Наш синоптик застрял под дождем без зонта. Скоро вернется и все расскажет!",
                "🌈 Радуга украла все наши данные о погоде. Подождите, пока мы их вернем!",
                "🌞 Солнце временно отказывается сотрудничать. Говорит, что устало светить...",
                "🌙 Луна сейчас заменяет солнце на смене, но она еще не разобралась с компьютером",
                "⛈ Гроза повредила наши метеоспутники! Но наши космонавты уже чинят их",
                "🌤 Облака устроили забастовку и отказываются двигаться. Ведем переговоры...",
                "❄ Снежинки запутались в проводах и мешают получить данные. Отправили дворника на помощь!",
                "🌪 Торнадо унес все наши записи о погоде! Пытаемся догнать их..."
            };

            var random = new Random();
            return funnyMessages[random.Next(funnyMessages.Length)];
        }

        private string GetFunnyNotFoundMessage(string city)
        {
            var funnyMessages = new[]
            {
                $"🗺 Хмм... {city}? Может быть этот город спрятался за облаками? Проверьте название!",
                $"🌍 Ой-ой! Кажется, {city} временно переехал в другую галактию. Может, опечатка?",
                $"🧭 Наш компас не может найти {city}. Возможно, его похитили инопланетяне?",
                $"🎯 {city} играет с нами в прятки! Проверьте, правильно ли написано название",
                $"🔍 Наш географ в отпуске и забыл указать {city} на карте. Может быть, попробуем другой город?",
                $"📍 {city}? Хм... Может быть, этот город замаскировался? Проверьте написание!",
                $"🌏 О нет! {city} временно ушел гулять. Убедитесь, что название написано правильно",
                $"🗿 Археологи все еще ищут древний город {city}. Пока поищем погоду в другом месте?",
                $"🎪 {city} уехал с цирком! Проверьте, не опечатались ли вы в названии",
                $"🌈 {city} где-то за радугой... Может быть, попробуем поискать его под другим названием?"
            };

            var random = new Random();
            return funnyMessages[random.Next(funnyMessages.Length)];
        }

        private async Task SendWeatherInfo(long chatId, string city, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient();
                var url = $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={_weatherApiKey}&units=metric&lang=ru";
                
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(url);
                }
                catch
                {
                    // Если произошла ошибка при запросе, показываем общее забавное сообщение
                    var funnyMessage = GetFunnyErrorMessage();
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: funnyMessage + "\n\n(Попробуйте еще раз через минутку)",
                        cancellationToken: cancellationToken);
                    return;
                }

                // Если город не найден
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var funnyMessage = GetFunnyNotFoundMessage(city);
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: funnyMessage,
                        cancellationToken: cancellationToken);
                    return;
                }

                // Если другая ошибка
                if (!response.IsSuccessStatusCode)
                {
                    var funnyMessage = GetFunnyErrorMessage();
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: funnyMessage + "\n\n(Попробуйте еще раз через минутку)",
                        cancellationToken: cancellationToken);
                    return;
                }

                // Если всё хорошо, получаем погоду
                var responseContent = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseContent);

                var temp = json["main"]?["temp"]?.Value<double>() ?? 0;
                var description = json["weather"]?[0]?["description"]?.Value<string>() ?? "неизвестно";
                var humidity = json["main"]?["humidity"]?.Value<int>() ?? 0;

                var weatherInfo = $"Погода в городе {city}:\n" +
                                $"Температура: {temp}°C\n" +
                                $"Условия: {description}\n" +
                                $"Влажность: {humidity}%";

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: weatherInfo,
                    cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                // В случае любой непредвиденной ошибки показываем забавное сообщение
                var funnyMessage = GetFunnyErrorMessage();
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: funnyMessage + "\n\n(Попробуйте еще раз через минутку)",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task SearchWeb(long chatId, string query, CancellationToken cancellationToken)
        {
            if (_browser == null)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "🔄 Инициализация поиска... Пожалуйста, подождите несколько секунд и попробуйте снова.",
                    cancellationToken: cancellationToken);

                // Try to initialize browser again
                await InitializeBrowser();
                return;
            }

            try
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "🔍 Ищу ответ на ваш вопрос...",
                    cancellationToken: cancellationToken);

                using var page = await _browser.NewPageAsync();
                
                // Configure page settings
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                await page.SetRequestInterceptionAsync(true);

                page.Request += (sender, e) =>
                {
                    if (e.Request.ResourceType == ResourceType.Image || 
                        e.Request.ResourceType == ResourceType.StyleSheet || 
                        e.Request.ResourceType == ResourceType.Font)
                    {
                        e.Request.AbortAsync();
                    }
                    else
                    {
                        e.Request.ContinueAsync();
                    }
                };
                
                // Navigate with increased timeout
                var navigationOptions = new NavigationOptions 
                { 
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
                    Timeout = 60000 // 60 seconds
                };
                
                await page.GoToAsync($"https://www.google.com/search?q={Uri.EscapeDataString(query)}", navigationOptions);

                // Wait for search results with increased timeout
                var waitOptions = new WaitForSelectorOptions { Timeout = 60000 };
                await page.WaitForSelectorAsync("div.g", waitOptions);
                
                var searchResults = await page.EvaluateFunctionAsync<string>(@"() => {
                    const results = Array.from(document.querySelectorAll('.g'));
                    return results.slice(0, 3).map(result => {
                        const title = result.querySelector('h3')?.textContent || '';
                        const link = result.querySelector('a')?.href || '';
                        const snippet = result.querySelector('.VwiC3b')?.textContent || '';
                        return `📌 *${title}*\n${snippet}\n${link}\n`;
                    }).join('\n');
                }");

                if (string.IsNullOrEmpty(searchResults))
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "По вашему запросу ничего не найдено. Попробуйте переформулировать вопрос.",
                        cancellationToken: cancellationToken);
                    return;
                }

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"🔍 Результаты поиска по запросу \"{query}\":\n\n{searchResults}",
                    parseMode: ParseMode.Markdown,
                    disableWebPagePreview: true,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
                string errorMessage = "❌ Произошла ошибка при поиске. ";
                
                if (ex.Message.Contains("Timeout"))
                {
                    errorMessage += "Превышено время ожидания ответа. Пожалуйста, попробуйте еще раз или переформулируйте вопрос.";
                }
                else
                {
                    errorMessage += "Пожалуйста, попробуйте позже.";
                }

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: errorMessage,
                    cancellationToken: cancellationToken);

                // Try to reinitialize browser on error
                try
                {
                    if (_browser != null)
                    {
                        await _browser.CloseAsync();
                        await _browser.DisposeAsync();
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                
                _browser = null;
                _ = InitializeBrowser();
            }
        }

        private async Task SendCurrentTime(long chatId, CancellationToken cancellationToken)
        {
            var currentTime = DateTime.Now.ToString("HH:mm:ss");
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Текущее время: {currentTime}",
                cancellationToken: cancellationToken);
        }

        private async Task SendHelpMessage(long chatId, CancellationToken cancellationToken)
        {
            var helpMessage = @"📋 *Список доступных команд:*

🌤 */weather* [город] - Узнать погоду в городе
Пример: /weather Москва

⏰ */time* - Показать текущее время

ℹ️ */help* - Показать это сообщение

💡 *Как задать вопрос?*
Просто напишите свой вопрос в чат, и я постараюсь найти на него ответ!

💡 Совет: Используйте кнопки меню для быстрого доступа к функциям.
Чтобы открыть меню, отправьте команду /start";

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: helpMessage,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }
}
