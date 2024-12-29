using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Console.Title = "server";

// ��������� Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

// ���������� ����������� � �������
builder.Services.AddLogging(loggingBuilder =>
{
	loggingBuilder.AddSerilog();
});

// ��������� �� ���������
int processingTimeLimit = 10;

// ������ ���������� ��������� ������
foreach (var arg in args)
{
	if (arg.StartsWith("--processingTimeLimit="))
	{
		if (int.TryParse(arg.Split('=')[1], out var parsedLimit))
		{
			processingTimeLimit = Math.Max(parsedLimit, 1); // ������� 1 �������
		}
	}
}

// Add services to the container
builder.Services.AddControllers();

var app = builder.Build();

// ����������� ��� ������� ����������
app.Logger.LogInformation("���������� �������� � �������� ��������� ��������: {ProcessingTimeLimit} ������", processingTimeLimit);

// ��������� RabbitMQ
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

// �������� �� �������
var consumer = new EventingBasicConsumer(channel);
consumer.Received += async (model, ea) =>
{
	var body = ea.Body.ToArray();
	var message = Encoding.UTF8.GetString(body);
	app.Logger.LogInformation("������� ������: {Message}", message);

	try
	{
		var cts = new CancellationTokenSource();
		cts.CancelAfter(processingTimeLimit * 1000); // ������������� ����� �������

		app.Logger.LogInformation("������ ��������� ������...");
		await Task.Delay(TimeSpan.FromSeconds(processingTimeLimit), cts.Token); // ����������� �������� � ������� ������

		// ������������ ������
		var response = $"������ ��� ������� '{message}' ���������� �� {processingTimeLimit} ������.";
		var responseBody = Encoding.UTF8.GetBytes(response);

		channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
		app.Logger.LogInformation("��������� ���������: {Response}", response);
	}
	catch (OperationCanceledException)
	{
		app.Logger.LogWarning("��������� ������ ��������� ����� ������� � {ProcessingTimeLimit} ������. ������ ����� �������.", processingTimeLimit);
		var timeoutResponse = $"������: ��������� ������ ��������� ����� ������� � {processingTimeLimit} ������.";
		var timeoutBody = Encoding.UTF8.GetBytes(timeoutResponse);

		channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: timeoutBody);
	}
	catch (Exception ex)
	{
		app.Logger.LogError(ex, "������ ��� ��������� �������");
	}
};

channel.BasicConsume(queue: "request_queue", autoAck: true, consumer: consumer);

app.Run();
