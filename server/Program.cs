using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Console.Title = "server";

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

// Добавление логирования в сервисы
builder.Services.AddLogging(loggingBuilder =>
{
	loggingBuilder.AddSerilog();
});

// Параметры по умолчанию
int processingTimeLimit = 10;

// Чтение аргументов командной строки
foreach (var arg in args)
{
	if (arg.StartsWith("--processingTimeLimit="))
	{
		if (int.TryParse(arg.Split('=')[1], out var parsedLimit))
		{
			processingTimeLimit = Math.Max(parsedLimit, 1); // Минимум 1 секунда
		}
	}
}

// Add services to the container
builder.Services.AddControllers();

var app = builder.Build();

// Логирование при запуске приложения
app.Logger.LogInformation("Приложение запущено с временем обработки запросов: {ProcessingTimeLimit} секунд", processingTimeLimit);

// Настройка RabbitMQ
var factory = new ConnectionFactory
{
	HostName = "localhost",
	Port = 15672,
	UserName = "guest",
	Password = "guest"
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "request_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
channel.QueueDeclare(queue: "response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

app.Logger.LogInformation("RabbitMQ Queues Initialized: request_queue, response_queue");

// Подписка на запросы
var consumer = new EventingBasicConsumer(channel);
consumer.Received += async (model, ea) =>
{
	var body = ea.Body.ToArray();
	var message = Encoding.UTF8.GetString(body);
	app.Logger.LogInformation("Получен запрос: {Message}", message);

	try
	{
		var cts = new CancellationTokenSource();
		cts.CancelAfter(processingTimeLimit * 1000); // Устанавливаем лимит времени

		app.Logger.LogInformation("Начало обработки данных...");
		await Task.Delay(TimeSpan.FromSeconds(processingTimeLimit), cts.Token); // Асинхронная задержка с токеном отмены

		// Формирование ответа
		var response = $"Данные для запроса '{message}' обработаны за {processingTimeLimit} секунд.";
		var responseBody = Encoding.UTF8.GetBytes(response);

		channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
		app.Logger.LogInformation("Результат отправлен: {Response}", response);
	}
	catch (OperationCanceledException)
	{
		app.Logger.LogWarning("Обработка данных превысила лимит времени в {ProcessingTimeLimit} секунд. Запрос будет отклонён.", processingTimeLimit);
		var timeoutResponse = $"Ошибка: обработка данных превысила лимит времени в {processingTimeLimit} секунд.";
		var timeoutBody = Encoding.UTF8.GetBytes(timeoutResponse);

		channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: timeoutBody);
	}
	catch (Exception ex)
	{
		app.Logger.LogError(ex, "Ошибка при обработке запроса");
	}
};

channel.BasicConsume(queue: "request_queue", autoAck: true, consumer: consumer);

app.Run();
