using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EasyNetQ;

/// <summary>
///     Represents various properties of a message
/// </summary>
public static class MessagePropertiesExtensions
{
    internal const string ConfirmationIdHeader = "EasyNetQ.Confirmation.Id";

    internal static MessageProperties SetConfirmationId(in this MessageProperties properties, ulong confirmationId)
        => properties.WithHeader(ConfirmationIdHeader, confirmationId.ToString());

    internal static bool TryGetConfirmationId(in this MessageProperties properties, out ulong confirmationId)
    {
        confirmationId = 0;
        return properties.Headers != null &&
               properties.Headers.TryGetValue(ConfirmationIdHeader, out var value) &&
               ulong.TryParse(Encoding.UTF8.GetString(value as byte[] ?? Array.Empty<byte>()), out confirmationId);
    }

    internal static void CopyTo(in this MessageProperties source, IBasicProperties basicProperties)
    {
        if (source.ContentTypePresent) basicProperties.ContentType = source.ContentType;
        if (source.ContentEncodingPresent) basicProperties.ContentEncoding = source.ContentEncoding;
        if (source.DeliveryModePresent) basicProperties.DeliveryMode = source.DeliveryMode;
        if (source.PriorityPresent) basicProperties.Priority = source.Priority;
        if (source.CorrelationIdPresent) basicProperties.CorrelationId = source.CorrelationId;
        if (source.ReplyToPresent) basicProperties.ReplyTo = source.ReplyTo;
        if (source.ExpirationPresent)
            basicProperties.Expiration = source.Expiration == null
                ? null
                : ((int)source.Expiration.Value.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        if (source.MessageIdPresent) basicProperties.MessageId = source.MessageId;
        if (source.TimestampPresent) basicProperties.Timestamp = new AmqpTimestamp(source.Timestamp);
        if (source.TypePresent) basicProperties.Type = source.Type;
        if (source.UserIdPresent) basicProperties.UserId = source.UserId;
        if (source.AppIdPresent) basicProperties.AppId = source.AppId;
        if (source.ClusterIdPresent) basicProperties.ClusterId = source.ClusterId;

        if (source.HeadersPresent)
            basicProperties.Headers = source.Headers switch
            {
                null => null,
                IDictionary<string, object?> dictionary => dictionary,
                _ => source.Headers.ToDictionary(x => x.Key, x => x.Value)
            };
    }

    public static MessageProperties WithHeader(in this MessageProperties source, string key, object? value)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            EnsureHeadersImmutable(source.Headers).SetItem(key, value),
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties WithPriority(in this MessageProperties source, byte priority)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties WithType(in this MessageProperties source, string? type)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }


    public static MessageProperties WithCorrelationId(in this MessageProperties source, string? correlationId)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            correlationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }


    public static MessageProperties WithContentType(in this MessageProperties source, string? contentType)
    {
        return new MessageProperties(
            contentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties WithContentEncoding(in this MessageProperties source, string? contentEncoding)
    {
        return new MessageProperties(
            source.ContentType,
            contentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }


    public static MessageProperties WithAppId(in this MessageProperties source, string? appId)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            appId,
            source.ClusterId
        );
    }

    public static MessageProperties WithClusterId(in this MessageProperties source, string? clusterId)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            clusterId
        );
    }

    public static MessageProperties WithUserId(in this MessageProperties source, string? userId)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            userId,
            source.AppId,
            source.ClusterId
        );
    }


    public static MessageProperties WithTimestamp(in this MessageProperties source, long timestamp)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties WithMessageId(in this MessageProperties source, string? messageId)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            messageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }


    public static MessageProperties WithReplyTo(in this MessageProperties source, string replyTo)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            replyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties WithExpiration(in this MessageProperties source, TimeSpan? expiration)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }


    public static MessageProperties WithDeliveryMode(in this MessageProperties source, byte deliveryMode)
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            source.Headers,
            deliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties WithHeaders(in this MessageProperties source, IDictionary<string, object?> headers)
    {
        if (headers.Count == 0) return source;

        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            EnsureHeadersImmutable(source.Headers).SetItems(headers),
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    public static MessageProperties ReplaceHeaders(
        in this MessageProperties source, Func<IReadOnlyDictionary<string, object?>?, IReadOnlyDictionary<string, object?>?> replaceFunc
    )
    {
        return new MessageProperties(
            source.ContentType,
            source.ContentEncoding,
            EnsureHeadersImmutable(replaceFunc(source.Headers)),
            source.DeliveryMode,
            source.Priority,
            source.CorrelationId,
            source.ReplyTo,
            source.Expiration,
            source.MessageId,
            source.Timestamp,
            source.Type,
            source.UserId,
            source.AppId,
            source.ClusterId
        );
    }

    private static ImmutableDictionary<string, object?> EnsureHeadersImmutable(IReadOnlyDictionary<string, object?>? headers)
    {
        return headers switch
        {
            null => ImmutableDictionary<string, object?>.Empty,
            ImmutableDictionary<string, object?> immutable => immutable,
            _ => ImmutableDictionary.CreateRange(headers)
        };
    }
}
