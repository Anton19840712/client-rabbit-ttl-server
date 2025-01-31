using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;

Console.Title = "server";

var builder = WebApplication.CreateBuilder(args);

// ��������� Serilog
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

Log.Information("���������� ��������");

// ��������� ���������� �� ���������� ��������� ������
var port = GetArgument(args, "--port", 5673);
var reconnectTimerInterval = GetArgument(args, "--reconnect-timer", 5000);
var idleTimerInterval = GetArgument(args, "--idle-timer", 15000);
var processingDelay = GetArgument(args, "--processing-delay", 10000);

// ��������� ������ �������
string url = $"http://localhost:{port}";
builder.WebHost.UseUrls(url);

// ����������� ������ �������
Log.Information("���������� client ����������� �� {Url}", url);

// ��������� RabbitMQ
var factory = new ConnectionFactory
{
	HostName = "localhost",
	Port = 5672,
	UserName = "guest",
	Password = "guest",
	AutomaticRecoveryEnabled = false // �������������� ����������� �������
};

IConnection connection = null;
IModel channel = null;

// �������
var reconnectTimer = new System.Timers.Timer(reconnectTimerInterval);
var idleTimer = new System.Timers.Timer(idleTimerInterval);

// ��������� ����������
bool isProcessingMessage = false;

void ConnectToRabbitMQ()
{
	try
	{
		Log.Information("������� ����������� � RabbitMQ...");
		connection = factory.CreateConnection();
		channel = connection.CreateModel();

		channel.QueueDeclare(queue: "request_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		channel.QueueDeclare(queue: "response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
		channel.BasicQos(0, prefetchCount: 1, global: false);

		Log.Information("���������� � RabbitMQ �����������");
		reconnectTimer.Stop();
		idleTimer.Start();
		StartConsuming();
	}
	catch (Exception ex)
	{
		Log.Error(ex, "������ ����������� � RabbitMQ. ������ ����� {Interval} ��...", reconnectTimerInterval);
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
		Log.Information("�������� ���������: {Message}", message);

		try
		{
			await Task.Delay(processingDelay);

			var response = $"��������� ����������: {message}";
			var responseBody = Encoding.UTF8.GetBytes(response);

			channel.BasicPublish(exchange: "", routingKey: "response_queue", basicProperties: null, body: responseBody);
			channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
			Log.Information("����� ���������: {Response}", response);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "������ ��� ��������� ���������");
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
		Log.Information("������� {Interval} ��. ���������� � RabbitMQ ����� �������.", idleTimerInterval);
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

	Log.Information("���������� � RabbitMQ �������");
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

// ������ ������
ConnectToRabbitMQ();

app.Run();
