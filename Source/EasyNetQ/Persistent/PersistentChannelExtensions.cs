using System;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Internals;
using RabbitMQ.Client;

namespace EasyNetQ.Persistent;

internal static class PersistentChannelExtensions
{
    public static void InvokeChannelAction(
        this IPersistentChannel source, Action<IModel> channelAction, CancellationToken cancellationToken = default
    )
    {
        source.InvokeChannelActionAsync(channelAction, cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    public static Task InvokeChannelActionAsync(
        this IPersistentChannel source, Action<IModel> channelAction, CancellationToken cancellationToken = default
    )
    {
        return source.InvokeChannelActionAsync<NoResult, ActionBasedPersistentChannelAction>(
            new ActionBasedPersistentChannelAction(channelAction), cancellationToken
        );
    }

    public static Task<TResult> InvokeChannelActionAsync<TResult>(
        this IPersistentChannel source, Func<IModel, TResult> channelAction, CancellationToken cancellationToken = default
    )
    {
        return source.InvokeChannelActionAsync<TResult, FuncBasedPersistentChannelAction<TResult>>(
            new FuncBasedPersistentChannelAction<TResult>(channelAction), cancellationToken
        );
    }
}
