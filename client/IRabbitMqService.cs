using RabbitMQ.Client;

public interface IRabbitMqService
{
	void PublishMessage(string queueName, string message);
	Task<string?> WaitForResponse(string queueName, int timeoutMilliseconds = 15000);
	IConnection CreateConnection();
}
