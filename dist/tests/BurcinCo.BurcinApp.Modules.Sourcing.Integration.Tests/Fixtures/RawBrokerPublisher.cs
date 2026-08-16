using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

/// <summary>
/// Raw AMQP publisher used by tests that need control over things Ruya's <c>IMessagePublisher</c> hides:
/// the envelope's <c>MessageId</c> (for Inbox-dedup tests), the JSON shape (for case-insensitive tests),
/// and frankly-invalid payloads (for poison-message DLQ tests). Mirrors how the Gateway Webhook adapter
/// service composed by Gateway publishes inbound envelopes: exchange-per-topic, envelope-wrapped JSON, persistent.
/// </summary>
internal sealed class RawBrokerPublisher : IAsyncDisposable
{
	private readonly IConnection _connection;
	private readonly IChannel _channel;

	private RawBrokerPublisher(IConnection connection, IChannel channel)
	{
		_connection = connection;
		_channel = channel;
	}

	public static async Task<RawBrokerPublisher> ConnectAsync(
		string host,
		int port,
		string username = SourcingTestFixture.RabbitMqUsername,
		string password = SourcingTestFixture.RabbitMqPassword,
		CancellationToken cancellationToken = default)
	{
		var factory = new ConnectionFactory
		{
			HostName = host,
			Port = port,
			UserName = username,
			Password = password,
			VirtualHost = "/",
		};
		var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		return new RawBrokerPublisher(connection, channel);
	}

	/// <summary>
	/// Publish an envelope-wrapped payload with a caller-controlled <paramref name="messageId"/>. Used by
	/// the Inbox-dedup test to verify the second delivery of the same MessageId is recognised as a duplicate.
	/// </summary>
	public async Task PublishEnvelopeAsync<TPayload>(string topic, string messageId, TPayload payload, JsonNamingPolicy? namingPolicy = null, CancellationToken cancellationToken = default) where TPayload : class
	{
		ArgumentNullException.ThrowIfNull(payload);
		var envelopeObject = new
		{
			messageId = messageId,
			messageType = topic,
			timestamp = DateTimeOffset.UtcNow,
			source = "test",
			persistent = true,
			payload = payload,
		};
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = namingPolicy ?? JsonNamingPolicy.CamelCase,
			WriteIndented = false,
		};
		var json = JsonSerializer.Serialize(envelopeObject, jsonOptions);
		await PublishRawAsync(topic, Encoding.UTF8.GetBytes(json), messageId, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Publish a raw byte payload to <paramref name="topic"/> (= exchange name in Ruya's exchange-per-topic topology).</summary>
	public async Task PublishRawAsync(string topic, byte[] body, string? messageId = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(body);

		// Match Ruya's topology: topic exchange named after the topic, default routing pattern "#".
		await _channel.ExchangeDeclareAsync(
			exchange: topic,
			type: ExchangeType.Topic,
			durable: true,
			autoDelete: false,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var props = new BasicProperties
		{
			DeliveryMode = DeliveryModes.Persistent,
			ContentType = "application/json",
		};
		if (messageId is not null)
		{
			props.MessageId = messageId;
		}

		await _channel.BasicPublishAsync(
			exchange: topic,
			routingKey: topic,
			mandatory: false,
			basicProperties: props,
			body: body,
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Count messages currently sitting in <paramref name="queueName"/>. Used to assert DLQ population.</summary>
	public async Task<uint> GetQueueDepthAsync(string queueName, CancellationToken cancellationToken = default)
	{
		// QueueDeclarePassive throws if the queue doesn't exist; for our DLQ assertions we want to
		// observe the queue created by the Sourcing subscriber — passive declare is exactly right.
		var ok = await _channel.QueueDeclarePassiveAsync(queueName, cancellationToken).ConfigureAwait(false);
		return ok.MessageCount;
	}

	/// <summary>Count active consumers on <paramref name="queueName"/>.</summary>
	public async Task<uint> GetQueueConsumerCountAsync(string queueName, CancellationToken cancellationToken = default)
	{
		var ok = await _channel.QueueDeclarePassiveAsync(queueName, cancellationToken).ConfigureAwait(false);
		return ok.ConsumerCount;
	}

	public async ValueTask DisposeAsync()
	{
		await _channel.CloseAsync().ConfigureAwait(false);
		_channel.Dispose();
		await _connection.CloseAsync().ConfigureAwait(false);
		_connection.Dispose();
	}
}
