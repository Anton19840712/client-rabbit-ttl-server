using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
Console.Title = "client";

// ��������� Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();

var app = builder.Build();

// ��������� ���������:
// ����� � ��������� ������������ ���������, ���������� �� post ������� � �������:
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

	// ��������� ���� �������:
	using var reader = new StreamReader(request.Body);
	var message = await reader.ReadToEndAsync();
	Log.Information("������ ���������: {Message}", message);

	// �������� ��������� � request_queue:
	var body = Encoding.UTF8.GetBytes(message);
	channel.BasicPublish(exchange: "", routingKey: "request_queue", basicProperties: null, body: body);

	// �������� ������ �� ������ ������� bpm:
	var responseConsumer = new EventingBasicConsumer(channel);
	string? responseMessage = null;

	var completionSource = new TaskCompletionSource<string>();
	responseConsumer.Received += (model, ea) =>
	{
		responseMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
		Log.Information("������� �����: {ResponseMessage}", responseMessage);
		completionSource.SetResult(responseMessage);
	};

	channel.BasicConsume(queue: "response_queue", autoAck: true, consumer: responseConsumer);

	// �������� ������ � ���������
	var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(15000));
	if (completedTask == completionSource.Task)
	{
		return Results.Ok(new { Message = responseMessage });
	}

	Log.Warning("����-��� ��� �������� ������");
	return Results.StatusCode(504);
});

app.Run();
