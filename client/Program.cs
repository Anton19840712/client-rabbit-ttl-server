using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
Console.Title = "client";

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();

var app = builder.Build();

// Настройка маршрутов:
// Здесь я отправляю определенное сообщение, полученное из post запроса в очередь:
app.MapPost("/send-request", async (HttpRequest request) =>
{
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

	// Получение тела запроса:
	using var reader = new StreamReader(request.Body);
	var message = await reader.ReadToEndAsync();
	Log.Information("Запрос отправлен: {Message}", message);

	// Отправка сообщения в request_queue:
	var body = Encoding.UTF8.GetBytes(message);
	channel.BasicPublish(exchange: "", routingKey: "request_queue", basicProperties: null, body: body);

	// Ожидание ответа из другой очереди bpm:
	var responseConsumer = new EventingBasicConsumer(channel);
	string? responseMessage = null;

	var completionSource = new TaskCompletionSource<string>();
	responseConsumer.Received += (model, ea) =>
	{
		responseMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
		Log.Information("Получен ответ: {ResponseMessage}", responseMessage);
		completionSource.SetResult(responseMessage);
	};

	channel.BasicConsume(queue: "response_queue", autoAck: true, consumer: responseConsumer);

	// Ожидание ответа с таймаутом
	var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(15000));
	if (completedTask == completionSource.Task)
	{
		return Results.Ok(new { Message = responseMessage });
	}

	Log.Warning("Тайм-аут при ожидании ответа");
	return Results.StatusCode(504);
});

app.Run();
