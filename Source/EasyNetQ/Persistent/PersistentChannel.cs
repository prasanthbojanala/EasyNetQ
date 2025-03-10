using System;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Events;
using EasyNetQ.Internals;
using EasyNetQ.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EasyNetQ.Persistent;

/// <inheritdoc />
public class PersistentChannel : IPersistentChannel
{
    private const string RequestPipeliningForbiddenMessage = "Pipelining of requests forbidden";

    private const int MinRetryTimeoutMs = 50;
    private const int MaxRetryTimeoutMs = 5000;
    private readonly IPersistentConnection connection;

    private readonly CancellationTokenSource disposeCts = new();
    private readonly IEventBus eventBus;
    private readonly AsyncLock mutex = new();
    private readonly PersistentChannelOptions options;
    private readonly ILogger<PersistentChannel> logger;

    private volatile IModel? initializedChannel;
    private volatile bool disposed;

    /// <summary>
    ///     Creates PersistentChannel
    /// </summary>
    /// <param name="options">The channel options</param>
    /// <param name="logger">The logger</param>
    /// <param name="connection">The connection</param>
    /// <param name="eventBus">The event bus</param>
    public PersistentChannel(
        in PersistentChannelOptions options,
        ILogger<PersistentChannel> logger,
        IPersistentConnection connection,
        IEventBus eventBus
    )
    {
        this.connection = connection;
        this.eventBus = eventBus;
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResult> InvokeChannelActionAsync<TResult, TChannelAction>(
        TChannelAction channelAction, CancellationToken cancellationToken = default
    ) where TChannelAction : struct, IPersistentChannelAction<TResult>
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PersistentChannel));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disposeCts.Token);
        using var _ = await mutex.AcquireAsync(cts.Token).ConfigureAwait(false);

        var retryTimeoutMs = MinRetryTimeoutMs;

        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            try
            {
                var channel = initializedChannel ??= CreateChannel();
                return channelAction.Invoke(channel);
            }
            catch (Exception exception)
            {
                var exceptionVerdict = GetExceptionVerdict(exception);
                if (exceptionVerdict.CloseChannel)
                    CloseChannel();

                if (exceptionVerdict.Rethrow)
                    throw;

                logger.Error(exception, "Failed to invoke channel action, invocation will be retried");
            }

            await Task.Delay(retryTimeoutMs, cts.Token).ConfigureAwait(false);
            retryTimeoutMs = Math.Min(retryTimeoutMs * 2, MaxRetryTimeoutMs);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        disposeCts.Cancel();
        mutex.Dispose();
        CloseChannel();
        disposeCts.Dispose();
    }

    private IModel CreateChannel()
    {
        var channel = connection.CreateModel();
        AttachChannelEvents(channel);
        return channel;
    }

    private void CloseChannel()
    {
        var channel = Interlocked.Exchange(ref initializedChannel, null);
        if (channel == null)
            return;

        channel.Close();
        DetachChannelEvents(channel);
        channel.Dispose();
    }

    private void AttachChannelEvents(IModel channel)
    {
        if (options.PublisherConfirms)
        {
            channel.ConfirmSelect();

            channel.BasicAcks += OnAck;
            channel.BasicNacks += OnNack;
        }

        channel.BasicReturn += OnReturn;
        channel.ModelShutdown += OnChannelShutdown;

        if (channel is IRecoverable recoverable)
            recoverable.Recovery += OnChannelRecovered;
        else
            throw new NotSupportedException("Non-recoverable channel is not supported");
    }

    private void DetachChannelEvents(IModel channel)
    {
        if (channel is IRecoverable recoverable)
            recoverable.Recovery -= OnChannelRecovered;

        channel.ModelShutdown -= OnChannelShutdown;
        channel.BasicReturn -= OnReturn;

        if (!options.PublisherConfirms)
            return;

        channel.BasicNacks -= OnNack;
        channel.BasicAcks -= OnAck;
    }

    private void OnChannelRecovered(object? sender, EventArgs e)
    {
        eventBus.Publish(new ChannelRecoveredEvent((IModel)sender!));
    }

    private void OnChannelShutdown(object? sender, ShutdownEventArgs e)
    {
        eventBus.Publish(new ChannelShutdownEvent((IModel)sender!));
    }

    private void OnReturn(object? sender, BasicReturnEventArgs args)
    {
        var messageProperties = new MessageProperties();
        messageProperties.CopyFrom(args.BasicProperties);
        var messageReturnedInfo = new MessageReturnedInfo(args.Exchange, args.RoutingKey, args.ReplyText);
        var @event = new ReturnedMessageEvent(
            (IModel)sender!,
            args.Body,
            messageProperties,
            messageReturnedInfo
        );
        eventBus.Publish(@event);
    }

    private void OnAck(object? sender, BasicAckEventArgs args)
    {
        eventBus.Publish(MessageConfirmationEvent.Ack((IModel)sender!, args.DeliveryTag, args.Multiple));
    }

    private void OnNack(object? sender, BasicNackEventArgs args)
    {
        eventBus.Publish(MessageConfirmationEvent.Nack((IModel)sender!, args.DeliveryTag, args.Multiple));
    }

    private static ExceptionVerdict GetExceptionVerdict(Exception exception)
    {
        switch (exception)
        {
            case OperationInterruptedException e:
                return e.ShutdownReason?.ReplyCode switch
                {
                    AmqpErrorCodes.ConnectionClosed => ExceptionVerdict.Suppress,
                    AmqpErrorCodes.AccessRefused => ExceptionVerdict.ThrowAndCloseChannel,
                    AmqpErrorCodes.NotFound => ExceptionVerdict.ThrowAndCloseChannel,
                    AmqpErrorCodes.ResourceLocked => ExceptionVerdict.ThrowAndCloseChannel,
                    AmqpErrorCodes.PreconditionFailed => ExceptionVerdict.ThrowAndCloseChannel,
                    AmqpErrorCodes.InternalErrors => ExceptionVerdict.SuppressAndCloseChannel,
                    _ => ExceptionVerdict.Throw
                };
            case NotSupportedException e:
                var isRequestPipeliningForbiddenException = e.Message.Contains(RequestPipeliningForbiddenMessage);
                return isRequestPipeliningForbiddenException
                    ? ExceptionVerdict.SuppressAndCloseChannel
                    : ExceptionVerdict.Throw;
            case BrokerUnreachableException:
                return ExceptionVerdict.Suppress;
            case EasyNetQException:
                return ExceptionVerdict.Suppress;
            default:
                return ExceptionVerdict.Throw;
        }
    }

    private readonly struct ExceptionVerdict
    {
        public static ExceptionVerdict Suppress { get; } = new(false, false);
        public static ExceptionVerdict SuppressAndCloseChannel { get; } = new(false, true);
        public static ExceptionVerdict Throw { get; } = new(true, false);
        public static ExceptionVerdict ThrowAndCloseChannel { get; } = new(true, true);

        private ExceptionVerdict(bool rethrow, bool closeChannel)
        {
            Rethrow = rethrow;
            CloseChannel = closeChannel;
        }

        public bool Rethrow { get; }
        public bool CloseChannel { get; }
    }
}
