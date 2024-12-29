using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;
using System.Timers;

var builder = WebApplication.CreateBuilder(args);

// ��������� Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

// ���������� ����������� � �������
builder.Services.AddLogging(loggingBuilder =>
{
	loggingBuilder.AddSerilog();
});

int processingTimeLimit = 10; // ����� ��������� ������� � ��������
int idleTimeout = 15000; // ����-��� ������� � �������������
DateTime lastMessageTime = DateTime.Now; // ����� ���������� ���������
System.Timers.Timer idleTimer = new System.Timers.Timer(idleTimeout);

// Add services to the container
builder.Services.AddControllers();

var app = builder.Build();

// ����������� ��� ������� ����������
app.Logger.LogInformation($"���������� �������� � �������� ��������� ��������: {processingTimeLimit} ������");

// ��������� RabbitMQ
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

// ������ ��� �������� �������
idleTimer.Elapsed += (sender, e) =>
{
	if ((DateTime.Now - lastMessageTime).TotalMilliseconds >= idleTimeout)
	{
		app.Logger.LogInformation("����-��� �������. �������� ���������� � RabbitMQ.");
		if (channel.IsOpen) channel.Close();
		if (connection.IsOpen) connection.Close();
		idleTimer.Stop();
	}
};

// �������� �� �������
var consumer = new EventingBasicConsumer(channel);
consumer.Received += async (model, ea) =>
{
	lastMessageTime = DateTime.Now; // ��������� ����� ���������� ���������
	if (!idleTimer.Enabled) idleTimer.Start(); // ��������� ������ �������

	if (!connection.IsOpen || !channel.IsOpen)
	{
		app.Logger.LogInformation("��������������� � RabbitMQ...");
		connection = factory.CreateConnection();
		channel = connection.CreateModel();
		channel.QueueDeclare(queue: "request_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		channel.QueueDeclare(queue: "response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		app.Logger.LogInformation("���������� �������������.");
	}

	var body = ea.Body.ToArray();
	var message = Encoding.UTF8.GetString(body);
	app.Logger.LogInformation("������� ������: {Message}", message);

	try
	{
		var cts = new CancellationTokenSource();
		cts.CancelAfter(processingTimeLimit * 1000); // ������������� ����� ������� �� ��������� ���������

		app.Logger.LogInformation("������ ��������� ������...");
		await Task.Delay(TimeSpan.FromSeconds(processingTimeLimit), cts.Token); // �������� ��������� ������

		var response = $"������ ��� ������� '{message}' ���������� �� {processingTimeLimit} ������.";
		var responseBody = Encoding.UTF8.GetBytes(response);

		if (channel.IsOpen)
		{
			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
			app.Logger.LogInformation("��������� ���������: {Response}", response);
		}
	}
	catch (OperationCanceledException)
	{
		app.Logger.LogWarning($"����������� ��������� ������ �� �� ������������� �������� ��������, ��� ��� ����� ������� � {processingTimeLimit} ������ �����.");
		var timeoutResponse = $"����������� ��������� ������ �� �� ������������� �������� ��������, ��� ��� ����� ������� � {processingTimeLimit} ������ �����.";
		var timeoutBody = Encoding.UTF8.GetBytes(timeoutResponse);

		if (channel.IsOpen)
		{
			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: timeoutBody);
		}
	}
	catch (Exception ex)
	{
		app.Logger.LogError(ex, "������ ��� ��������� �������");
	}
};

// ������������ � �������
channel.BasicConsume(queue: "request_queue", autoAck: true, consumer: consumer);

app.Run();
