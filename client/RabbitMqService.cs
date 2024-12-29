namespace client
{
	using RabbitMQ.Client;
	using RabbitMQ.Client.Events;
	using System.Text;

	public class RabbitMqService : IRabbitMqService
	{
		private readonly ConnectionFactory _factory;

		public RabbitMqService()
		{
			_factory = new ConnectionFactory
			{
				HostName = "localhost",
				Port = 5672,
				UserName = "guest",
				Password = "guest"
			};
		}

		public void PublishMessage(string queueName, string message)
		{
			using var connection = _factory.CreateConnection();
			using var channel = connection.CreateModel();

			channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

			var body = Encoding.UTF8.GetBytes(message);
			channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
		}

		public async Task<string?> WaitForResponse(string queueName, int timeoutMilliseconds = 15000)
		{
			using var connection = _factory.CreateConnection();
			using var channel = connection.CreateModel();

			channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

			var consumer = new EventingBasicConsumer(channel);
			var completionSource = new TaskCompletionSource<string?>();

			consumer.Received += (model, ea) =>
			{
				var response = Encoding.UTF8.GetString(ea.Body.ToArray());
				completionSource.SetResult(response);
			};

			channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

			var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(timeoutMilliseconds));
			return completedTask == completionSource.Task ? completionSource.Task.Result : null;
		}
	}
}
