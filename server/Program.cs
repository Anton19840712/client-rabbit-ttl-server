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
var reconnectTimer = new System.Timers.Timer(5000); // Таймер восстановления соединения (5 секунд)
var idleTimer = new System.Timers.Timer(15000);    // Таймер простоя (15 секунд)

// Состояние соединения
bool isProcessingMessage = false; // Флаг для отслеживания обработки сообщения

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
		reconnectTimer.Stop(); // Останавливаем таймер восстановления
		idleTimer.Start(); // Запускаем таймер простоя
		StartConsuming();
	}
	catch (Exception ex)
	{
		Log.Error(ex, "Ошибка подключения к RabbitMQ. Повтор через 5 секунд...");
		reconnectTimer.Start(); // Если не удалось, продолжаем попытки
	}
}

void StartConsuming()
{
	var consumer = new EventingBasicConsumer(channel);
	consumer.Received += async (model, ea) =>
	{
		isProcessingMessage = true;
		idleTimer.Stop(); // Останавливаем таймер простоя, так как пришло сообщение

		var body = ea.Body.ToArray();
		var message = Encoding.UTF8.GetString(body);
		Log.Information("Получено сообщение: {Message}", message);

		try
		{
			await Task.Delay(10000); // Имитация обработки сообщения, здесь может быть ваш процесс, который будет продолжаться столько-то времени.

			var response = $"Сообщение обработано: {message}";
			var responseBody = Encoding.UTF8.GetBytes(response);

			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
			channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false); // Подтверждение
			Log.Information("Ответ отправлен: {Response}", response);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка при обработке сообщения");
			channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true); // Вернуть сообщение в очередь
		}
		finally
		{
			isProcessingMessage = false;
			idleTimer.Start(); // Возобновляем таймер простоя
		}
	};

	channel.BasicConsume(queue: "request_queue", autoAck: false, consumer: consumer);
}

// Таймер для разрыва соединения при простое
idleTimer.Elapsed += (sender, e) =>
{
	if (!isProcessingMessage) // Если сообщений нет
	{
		Log.Information("Простой 15 секунд. Соединение с RabbitMQ будет закрыто.");
		CloseConnection();
		reconnectTimer.Start(); // Начинаем восстановление через 5 секунд
	}
};

// Таймер на восстановление соединения
reconnectTimer.Elapsed += (sender, e) =>
{
	if (connection == null || !connection.IsOpen)
	{
		ConnectToRabbitMQ();
	}
};

// Закрытие соединения
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

// Начало работы
ConnectToRabbitMQ();

app.Run();
