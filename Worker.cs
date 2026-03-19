using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

public class NewsParserWorker : BackgroundService
{
    private readonly ILogger<NewsParserWorker> _logger;
    private readonly ITelegramBotClient _botClient;
    private string _lastNewsUrl = string.Empty; // Cсылка на последнюю новость
    
    // TODO: SQL или другой способ хранения подписчиков. Пока просто список в коде для теста.
    private readonly List<long> _subscribedUsers = new() { 1790324436 }; 

    public NewsParserWorker(ILogger<NewsParserWorker> logger)
    {
        _logger = logger;
        //Токен из переменной окружения, задается в docker-compose
        string? token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        
        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("Не задан токен телеграм-бота в переменных окружения!");
        }
        
        _botClient = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("School28 News Bot started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewNews(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при парсинге или отправке.");
            }

            // Ждем 30 минут перед следующей проверкой
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task CheckForNewNews(CancellationToken cancellationToken)
    {
        string url = "https://school28-kirov.ru/novosti";
        var web = new HtmlWeb();
        var doc = await web.LoadFromWebAsync(url, cancellationToken);

        // Ищем первый тег <a> с классом news__item
        var firstNewsNode = doc.DocumentNode.SelectSingleNode("//a[contains(@class, 'news__item')]"); 
        
        if (firstNewsNode == null) return;

        //Ссылка относительная, добавляем домен
        var relativeLink = firstNewsNode.GetAttributeValue("href", "");
        var link = "https://school28-kirov.ru/" + relativeLink;

        // Заголовок из атрибута title
        var title = firstNewsNode.GetAttributeValue("title", "Новость без заголовка");

        // Проверяем, новая ли это новость
        if (!string.IsNullOrEmpty(relativeLink) && link != _lastNewsUrl)
        {
            _logger.LogInformation($"Найдена новая новость: {title}");
            _lastNewsUrl = link;
            File.WriteAllText("OldUrl.txt", link); // Запись в файл

            string message = $"📢 <b>Новая публикация на сайте лицея!</b>\n\n{title}\n\n<a href='{link}'>Читать полностью...</a>";

            // Рассылаем всем подписчикам
            foreach (var chatId in _subscribedUsers)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: message,
                    parseMode: ParseMode.Html,
                    cancellationToken: cancellationToken);
            }
    }
}
}