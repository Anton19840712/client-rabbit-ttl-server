using client;
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

builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

var app = builder.Build();

// ��������� ���������:
// ����� � ��������� ������������ ���������, ���������� �� post ������� � �������:
app.MapPost("/send-request", async (HttpRequest request, IRabbitMqService rabbitMqService) =>
{
	// ��������� ���� �������:
	using var reader = new StreamReader(request.Body);
	var message = await reader.ReadToEndAsync();
	Log.Information("������ ���������: {Message}", message);

	// �������� ���������
	rabbitMqService.PublishMessage("request_queue", message);

	// �������� ������
	var responseMessage = await rabbitMqService.WaitForResponse("response_queue");
	if (responseMessage != null)
	{
		Log.Information("������� �����: {ResponseMessage}", responseMessage);
		return Results.Ok(new { Message = responseMessage });
	}

	Log.Warning("����-��� ��� �������� ������");
	return Results.StatusCode(504);
});

app.Run();