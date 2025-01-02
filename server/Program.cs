using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;

Console.Title = "server";

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

Log.Information("Приложение запущено");

// Получение параметров из аргументов командной строки
var port = GetArgument(args, "--port", 5673);
var reconnectTimerInterval = GetArgument(args, "--reconnect-timer", 5000);
var idleTimerInterval = GetArgument(args, "--idle-timer", 15000);
var processingDelay = GetArgument(args, "--processing-delay", 10000);

// Настройка адреса запуска
string url = $"http://localhost:{port}";
builder.WebHost.UseUrls(url);

// Логирование адреса запуска
Log.Information("Приложение client запускается на {Url}", url);

// Настройка RabbitMQ
var factory = new ConnectionFactory
{
	HostName = "localhost",
	Port = 5672,
	UserName = "guest",
	Password = "guest",
	AutomaticRecoveryEnabled = false // Восстановление управляется вручную
};

IConnection connection = null;
IModel channel = null;

// Таймеры
var reconnectTimer = new System.Timers.Timer(reconnectTimerInterval);
var idleTimer = new System.Timers.Timer(idleTimerInterval);

// Состояние соединения
bool isProcessingMessage = false;

void ConnectToRabbitMQ()
{
	try
	{
		Log.Information("Попытка подключения к RabbitMQ...");
		connection = factory.CreateConnection();
		channel = connection.CreateModel();

		channel.QueueDeclare(queue: "request_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		channel.QueueDeclare(queue: "response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		channel.BasicQos(0, prefetchCount: 1, global: false);

		Log.Information("Соединение с RabbitMQ установлено");
		reconnectTimer.Stop();
		idleTimer.Start();
		StartConsuming();
	}
	catch (Exception ex)
	{
		Log.Error(ex, "Ошибка подключения к RabbitMQ. Повтор через {Interval} мс...", reconnectTimerInterval);
		reconnectTimer.Start();
	}
}

void StartConsuming()
{
	var consumer = new EventingBasicConsumer(channel);
	consumer.Received += async (model, ea) =>
	{
		isProcessingMessage = true;
		idleTimer.Stop();

		var body = ea.Body.ToArray();
		var message = Encoding.UTF8.GetString(body);
		Log.Information("Получено сообщение: {Message}", message);

		try
		{
			await Task.Delay(processingDelay);

			var response = $"Сообщение обработано: {message}";
			var responseBody = Encoding.UTF8.GetBytes(response);

			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
			channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
			Log.Information("Ответ отправлен: {Response}", response);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка при обработке сообщения");
			channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
		}
		finally
		{
			isProcessingMessage = false;
			idleTimer.Start();
		}
	};

	channel.BasicConsume(queue: "request_queue", autoAck: false, consumer: consumer);
}

idleTimer.Elapsed += (sender, e) =>
{
	if (!isProcessingMessage)
	{
		Log.Information("Простой {Interval} мс. Соединение с RabbitMQ будет закрыто.", idleTimerInterval);
		CloseConnection();
		reconnectTimer.Start();
	}
};

reconnectTimer.Elapsed += (sender, e) =>
{
	if (connection == null || !connection.IsOpen)
	{
		ConnectToRabbitMQ();
	}
};

void CloseConnection()
{
	idleTimer.Stop();

	if (channel?.IsOpen == true)
	{
		channel.Close();
	}

	if (connection?.IsOpen == true)
	{
		connection.Close();
	}

	Log.Information("Соединение с RabbitMQ закрыто");
}

static int GetArgument(string[] args, string key, int defaultValue)
{
	var arg = args.FirstOrDefault(a => a.StartsWith(key + "="));
	if (arg != null && int.TryParse(arg.Substring(key.Length + 1), out var value))
	{
		return value;
	}
	return defaultValue;
}

// Начало работы
ConnectToRabbitMQ();

app.Run();
