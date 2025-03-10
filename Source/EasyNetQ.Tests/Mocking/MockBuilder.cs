using System;
using System.Collections.Generic;
using System.ComponentModel;
using EasyNetQ.ConnectionString;
using EasyNetQ.Consumer;
using EasyNetQ.DI;
using EasyNetQ.LightInject;
using EasyNetQ.Producer;
using NSubstitute;
using RabbitMQ.Client;

namespace EasyNetQ.Tests.Mocking;

public class MockBuilder : IDisposable
{
    private readonly ServiceContainer container;
    private readonly IBus bus;

    private readonly IBasicProperties basicProperties = new BasicProperties();
    private readonly Stack<IModel> channelPool = new();
    private readonly List<IModel> channels = new();
    private readonly IConnection connection = Substitute.For<IAutorecoveringConnection>();
    private readonly IConnectionFactory connectionFactory = Substitute.For<IConnectionFactory>();
    private readonly List<AsyncDefaultBasicConsumer> consumers = new();

    public MockBuilder() : this(_ => { })
    {
    }

    public MockBuilder(Action<IServiceRegister> registerServices) : this("host=localhost", registerServices)
    {
    }

    public MockBuilder(string connectionString) : this(connectionString, _ => { })
    {
    }

    public MockBuilder(string connectionString, Action<IServiceRegister> registerServices)
    {
        for (var i = 0; i < 10; i++)
            channelPool.Push(Substitute.For<IModel, IRecoverable>());

        connectionFactory.CreateConnection(Arg.Any<IList<AmqpTcpEndpoint>>()).Returns(connection);
        connection.IsOpen.Returns(true);
        connection.Endpoint.Returns(new AmqpTcpEndpoint("localhost"));

        connection.CreateModel().Returns(_ =>
        {
            var channel = channelPool.Pop();
            channels.Add(channel);
            channel.CreateBasicProperties().Returns(basicProperties);
            channel.IsOpen.Returns(true);
            channel.BasicConsume(null, false, null, true, false, null, null)
                .ReturnsForAnyArgs(consumeInvocation =>
                {
                    var queueName = (string)consumeInvocation[0];
                    var consumerTag = (string)consumeInvocation[2];
                    var consumer = (AsyncDefaultBasicConsumer)consumeInvocation[6];

                    ConsumerQueueNames.Add(queueName);
                    consumer.HandleBasicConsumeOk(consumerTag);
                    consumers.Add(consumer);
                    return string.Empty;
                });
            channel.QueueDeclare(null, true, false, false, null)
                .ReturnsForAnyArgs(queueDeclareInvocation =>
                {
                    var queueName = (string)queueDeclareInvocation[0];
                    return new QueueDeclareOk(queueName, 0, 0);
                });
            channel.WaitForConfirms(default).ReturnsForAnyArgs(true);

            return channel;
        });

        container = new ServiceContainer(c => c.EnablePropertyInjection = false);
        var adapter = new LightInjectAdapter(container);
        adapter.Register(connectionFactory);

        RabbitHutch.RegisterBus(
            adapter,
            x => x.Resolve<IConnectionStringParser>().Parse(connectionString),
            registerServices
        );

        bus = container.GetInstance<IBus>();
    }

    public IBus Bus => bus;

    public IConnectionFactory ConnectionFactory => connectionFactory;

    public IConnection Connection => connection;

    public List<IModel> Channels => channels;

    public List<AsyncDefaultBasicConsumer> Consumers => consumers;

    public IModel NextModel => channelPool.Peek();

    public IPubSub PubSub => container.GetInstance<IPubSub>();

    public IRpc Rpc => container.GetInstance<IRpc>();

    public ISendReceive SendReceive => container.GetInstance<ISendReceive>();

    public IScheduler Scheduler => container.GetInstance<IScheduler>();

    public IEventBus EventBus => container.GetInstance<IEventBus>();

    public IConventions Conventions => container.GetInstance<IConventions>();

    public ITypeNameSerializer TypeNameSerializer => container.GetInstance<ITypeNameSerializer>();

    public ISerializer Serializer => container.GetInstance<ISerializer>();

    public IProducerConnection ProducerConnection => container.GetInstance<IProducerConnection>();
    public IConsumerConnection ConsumerConnection => container.GetInstance<IConsumerConnection>();

    public IConsumerErrorStrategy ConsumerErrorStrategy => container.GetInstance<IConsumerErrorStrategy>();

    public List<string> ConsumerQueueNames { get; } = new();

    public void Dispose() => container.Dispose();
}
