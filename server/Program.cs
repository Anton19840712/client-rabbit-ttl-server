using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;
using System.Timers;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

// Добавление логирования в сервисы
builder.Services.AddLogging(loggingBuilder =>
{
	loggingBuilder.AddSerilog();
});

int processingTimeLimit = 10; // Время обработки запроса в секундах
int idleTimeout = 15000; // Тайм-аут простоя в миллисекундах
DateTime lastMessageTime = DateTime.Now; // Время последнего сообщения
System.Timers.Timer idleTimer = new System.Timers.Timer(idleTimeout);

// Add services to the container
builder.Services.AddControllers();

var app = builder.Build();

// Логирование при запуске приложения
app.Logger.LogInformation($"Приложение запущено с временем обработки запросов: {processingTimeLimit} секунд");

// Настройка RabbitMQ
var factory = new ConnectionFactory
{
	HostName = "localhost",
	Port = 5672,
	UserName = "guest",
	Password = "guest"
};

var connection = factory.CreateConnection();
var channel = connection.CreateModel();

channel.QueueDeclare(queue: "request_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
channel.QueueDeclare(queue: "response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

app.Logger.LogInformation("RabbitMQ Queues Initialized: request_queue, response_queue");

// Таймер для проверки простоя
idleTimer.Elapsed += (sender, e) =>
{
	if ((DateTime.Now - lastMessageTime).TotalMilliseconds >= idleTimeout)
	{
		app.Logger.LogInformation("Тайм-аут простоя. Закрытие соединения с RabbitMQ.");
		if (channel.IsOpen) channel.Close();
		if (connection.IsOpen) connection.Close();
		idleTimer.Stop();
	}
};

// Подписка на запросы
var consumer = new EventingBasicConsumer(channel);
consumer.Received += async (model, ea) =>
{
	lastMessageTime = DateTime.Now; // Обновляем время последнего сообщения
	if (!idleTimer.Enabled) idleTimer.Start(); // Запускаем таймер простоя

	if (!connection.IsOpen || !channel.IsOpen)
	{
		app.Logger.LogInformation("Переподключение к RabbitMQ...");
		connection = factory.CreateConnection();
		channel = connection.CreateModel();
		channel.QueueDeclare(queue: "request_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		channel.QueueDeclare(queue: "response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		app.Logger.LogInformation("Соединение восстановлено.");
	}

	var body = ea.Body.ToArray();
	var message = Encoding.UTF8.GetString(body);
	app.Logger.LogInformation("Получен запрос: {Message}", message);

	try
	{
		var cts = new CancellationTokenSource();
		cts.CancelAfter(processingTimeLimit * 1000); // Устанавливаем лимит времени на обработку сообщений

		app.Logger.LogInformation("Начало обработки данных...");
		await Task.Delay(TimeSpan.FromSeconds(processingTimeLimit), cts.Token); // Имитация обработки данных

		var response = $"Данные для запроса '{message}' обработаны за {processingTimeLimit} секунд.";
		var responseBody = Encoding.UTF8.GetBytes(response);

		if (channel.IsOpen)
		{
			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
			app.Logger.LogInformation("Результат отправлен: {Response}", response);
		}
	}
	catch (OperationCanceledException)
	{
		app.Logger.LogWarning($"Возможность обработки данных по за установленный интервал закончен, так как лимит времени в {processingTimeLimit} секунд истек.");
		var timeoutResponse = $"Возможность обработки данных по за установленный интервал закончен, так как лимит времени в {processingTimeLimit} секунд истек.";
		var timeoutBody = Encoding.UTF8.GetBytes(timeoutResponse);

		if (channel.IsOpen)
		{
			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: timeoutBody);
		}
	}
	catch (Exception ex)
	{
		app.Logger.LogError(ex, "Ошибка при обработке запроса");
	}
};

// Подключаемся к очереди
channel.BasicConsume(queue: "request_queue", autoAck: true, consumer: consumer);

app.Run();
