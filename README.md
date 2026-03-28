
# School28NewsParser - Парс новостей с сайта лицея в телеграмм

**School28NewsParser** - лёгкая фоновая служба C# (.NET 9), которая парсит новости с [сайта лицея](https://school28-kirov.ru/novosti), получая их заголовок и проверяя новизну. Для развёртывания используется Docker, по умолчанию Dockerfile предназначен и адаптирован под Ubuntu 24.04. 

**Описание задачи:** Проект создан для автоматизации отслеживания новостей о приемной кампании лицея. Служба освобождает пользователя от необходимости вручную обновлять страницу сайта, мгновенно доставляя уведомления в удобный мессенджер.

##  Технические детали
`Program.cs` вызывает и запускает `Worker.cs`, где и содержится служба:

    var  builder  =  Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<NewsParserWorker>();
    var  host  =  builder.Build();
    host.Run();

`Worker.cs` является главным файлом, выполняет сам парс и отправку сообщений, фактически представляет собой весь проект.
В первую очередь в `Worker.cs` инициализируются библиотеки:

    using  HtmlAgilityPack;
    using  Telegram.Bot;
    using  Telegram.Bot.Types.Enums;
   

`HtmlAgilityPack` --- используется для формирования и отправки GET-запросов непосредственно на сайт(сервер) лицея. Именно эта библиотека занимается парсом новостей с сайта.
`Telegram.Bot` и `Telegram.Bot.Types.Enums` --- библиотеки для работы с API телеграмм ботов, осуществляют отправку полученной ранее страницы пользователю (пользователям).

 После инициализации библиотек идёт их первичная настройка, для `HtmlAgilityPack` такой настройки не требуется, а для `Telegram.Bot` необходим токен бота:

     public  NewsParserWorker(ILogger<NewsParserWorker> logger)
    {
	    _logger  =  logger;
	    //Токен из переменной окружения, задается в docker-compose 
	    string?  token  =  Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN"); 
	    
	    if (string.IsNullOrEmpty(token)) 
	    {
		    throw  new  Exception("Не задан токен телеграм-бота в переменных окружения!");
	    }
	    _botClient  =  new  TelegramBotClient(token);
	}
После первичной (единоразовой) настройки выполняется цикличный код, в данном проекте это парс новости, проверка свежести и отправка клиентам. Все действия выведены в отдельный метод, он вызывается раз в 30 минут:

    protected  override  async  Task  ExecuteAsync(CancellationToken  stoppingToken)
    {
	    _logger.LogInformation("School28 News Bot started.");
	    while (!stoppingToken.IsCancellationRequested)
	    {
	    try
	    {  
		    await  CheckForNewNews(stoppingToken); //Вызов основного метода
	    }
	    catch (Exception  ex)
	    {
		    _logger.LogError(ex, "Ошибка при парсинге или отправке.");
	    }
	    await  Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Ждем 30 минут перед следующей проверкой
	    }
    }

Основной метод `private  async  Task  CheckForNewNews(CancellationToken  cancellationToken)` в первую очередь создает и заполняет необходимые для работы переменные:

    string  url  =  "https://school28-kirov.ru/novosti";
    var  web  =  new  HtmlWeb();
    var  doc  =  await  web.LoadFromWebAsync(url, cancellationToken);
После получения переменных метод формирует ссылку на новость, для этого используется HTML класс `news__item`, он получен вручную с помощью проверки страницы с новостями через режим разработчика (F12):

    // Ищем первый тег <a> с классом news__item
    var  firstNewsNode  =  doc.DocumentNode.SelectSingleNode("//a[contains(@class, 'news__item')]");
    if (firstNewsNode  ==  null) return; //NotNull check
    
    //Ссылка относительная, добавляем домен
    var  relativeLink  =  firstNewsNode.GetAttributeValue("href", "");
    var  link  =  "https://school28-kirov.ru/"  +  relativeLink;
После получения ссылки уже по ней получаем заголовок:

    var  title  =  firstNewsNode.GetAttributeValue("title", "Новость без заголовка");
Теперь у нас есть новость и её заголовок, осталось проверить новая ли она и если да, то отправить клиенту:

    // Проверяем, новая ли это новость
    if (!string.IsNullOrEmpty(relativeLink) &&  link  !=  _lastNewsUrl)
    {
	    _logger.LogInformation($"Найдена новая новость: {title}");
	    _lastNewsUrl  =  link;
		string  message  =  $"📢 <b>Новая публикация на сайте лицея!</b>\n\n{title}\n\n<a href='{link}'>Читать полностью...</a>";
	    // Рассылаем всем подписчикам
	    foreach (var  chatId  in  _subscribedUsers)
	    {
		    await  _botClient.SendMessage(
		    chatId: chatId,
			text: message,
			parseMode: ParseMode.Html,
		    cancellationToken: cancellationToken);
	    }
	}

## Установка и запуск

Сервис распространяется в виде Docker-контейнера и может быть запущен на любой системе с установленным Docker.

**Для Docker Compose предусмотрен файл docker-compose.yml, в случаи его использования достаточно копировать репозиторий и прописать команду `docker compose up -d --build`**

### Требования

-   Docker 20+
    
-   доступ к интернету для загрузки базовых образов
    

### Сборка контейнера

Перейдите в директорию проекта (где находится `Dockerfile`) и выполните:

```bash
docker build -t school-news-bot .

```

Во время сборки Docker:

1.  Использует образ **.NET 9 SDK** для компиляции проекта.
    
2.  Восстанавливает зависимости (`dotnet restore`).
    
3.  Публикует приложение в режиме `Release`.
    
4.  Создаёт финальный минимальный образ на базе **.NET Runtime**.
    

### Запуск сервиса

После успешной сборки контейнер можно запустить командой:

```bash
docker run -d --name school-news-bot -e TELEGRAM_BOT_TOKEN="токен_здесь" school-news-bot

```

Контейнер запустит службу через:

```bash
dotnet SchoolNewsBot.dll

```

### Остановка контейнера

```bash
docker stop school-news-bot
docker rm school-news-bot

```

### Пересборка после изменений

Если код был изменён, пересоберите образ:

```bash
docker build -t school-news-bot .

```

и перезапустите контейнер.
