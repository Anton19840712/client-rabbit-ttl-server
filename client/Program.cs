using client;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;


Console.Title = "client";

var builder = WebApplication.CreateBuilder(args);

// Регистрация RabbitMqService как Singleton
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

// Регистрация ResponseListenerService как фонового сервиса
builder.Services.AddHostedService<ResponseListenerService>();

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

var app = builder.Build();

// Настройка маршрутов:
// Здесь я отправляю определенное сообщение, полученное из post запроса в очередь:
app.MapPost("/send-request", async (HttpRequest request, IRabbitMqService rabbitMqService) =>
{
	// Получение тела запроса:
	using var reader = new StreamReader(request.Body);
	var message = await reader.ReadToEndAsync();
	Log.Information("Запрос отправлен: {Message}", message);

	// Отправка сообщения
	rabbitMqService.PublishMessage("request_queue", message);

	// Ожидание ответа
	var responseMessage = await rabbitMqService.WaitForResponse("response_queue");
	if (responseMessage != null)
	{
		Log.Information($"Получен ответ: {responseMessage}");
		return Results.Ok(new { Message = responseMessage });
	}

	Log.Warning("Тайм-аут при ожидании ответа");
	return Results.StatusCode(504);
});

app.Run();
