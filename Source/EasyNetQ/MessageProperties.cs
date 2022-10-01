using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using EasyNetQ.Internals;
using RabbitMQ.Client;

namespace EasyNetQ;

/// <summary>
///     Represents various properties of a message
/// </summary>
public readonly struct MessageProperties
{
    private readonly string? contentType;
    private readonly string? contentEncoding;
    private readonly IReadOnlyDictionary<string, object?>? headers;
    private readonly byte deliveryMode;
    private readonly byte priority;
    private readonly string? correlationId;
    private readonly string? replyTo;
    private readonly TimeSpan? expiration;
    private readonly string? messageId;
    private readonly long timestamp;
    private readonly string? type;
    private readonly string? userId;
    private readonly string? appId;
    private readonly string? clusterId;

    internal MessageProperties(
        string? contentType,
        string? contentEncoding,
        IReadOnlyDictionary<string, object?>? headers,
        byte deliveryMode,
        byte priority,
        string? correlationId,
        string? replyTo,
        TimeSpan? expiration,
        string? messageId,
        long timestamp,
        string? type,
        string? userId,
        string? appId,
        string? clusterId
    )
    {
        this.contentType = contentType;
        this.contentEncoding = contentEncoding;
        this.headers = headers;
        this.deliveryMode = deliveryMode;
        this.priority = priority;
        this.correlationId = correlationId;
        this.replyTo = replyTo;
        this.expiration = expiration;
        this.messageId = messageId;
        this.timestamp = timestamp;
        this.type = type;
        this.userId = userId;
        this.appId = appId;
        this.clusterId = clusterId;
    }

    internal MessageProperties(IBasicProperties basicProperties)
    {
        contentType = basicProperties.ContentType;
        contentEncoding = basicProperties.ContentEncoding;
        deliveryMode = basicProperties.DeliveryMode;
        priority = basicProperties.Priority;
        correlationId = basicProperties.CorrelationId;
        replyTo = basicProperties.ReplyTo;
        expiration = int.TryParse(basicProperties.Expiration, out var expirationMilliseconds)
            ? TimeSpan.FromMilliseconds(expirationMilliseconds)
            : default;
        messageId = basicProperties.MessageId;
        timestamp = basicProperties.Timestamp.UnixTime;
        type = basicProperties.Type;
        userId = basicProperties.UserId;
        appId = basicProperties.AppId;
        clusterId = basicProperties.ClusterId;
        // A little crutch to allocate less, Dictionary implements IReadOnlyDictionary
        headers = basicProperties.Headers switch
        {
            null => null,
            IReadOnlyDictionary<string, object?> readonlyDictionary => readonlyDictionary,
            _ => new ReadOnlyDictionary<string, object?>(basicProperties.Headers)
        };
    }

    /// <summary>
    ///     MIME Content type
    /// </summary>
    public string? ContentType => contentType;

    /// <summary>
    ///     MIME content encoding
    /// </summary>
    public string? ContentEncoding => contentEncoding;

    /// <summary>
    ///     Various headers
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Headers => headers;

    /// <summary>
    ///     non-persistent (1) or persistent (2)
    /// </summary>
    public byte DeliveryMode => deliveryMode;

    /// <summary>
    ///     Message priority, 0 to 9
    /// </summary>
    public byte Priority => priority;

    /// <summary>
    ///     Application correlation identifier
    /// </summary>
    public string? CorrelationId => correlationId;

    /// <summary>
    ///     Destination to reply to
    /// </summary>
    public string? ReplyTo => replyTo;

    /// <summary>
    ///     Message expiration specification
    /// </summary>
    public TimeSpan? Expiration => expiration;

    /// <summary>
    ///     Application message identifier
    /// </summary>
    public string? MessageId => messageId;

    /// <summary>
    ///     Message timestamp
    /// </summary>
    public long Timestamp => timestamp;

    /// <summary>
    ///     Message type name
    /// </summary>
    public string? Type => type;

    /// <summary>
    ///     Creating user id
    /// </summary>
    public string? UserId => userId;

    /// <summary>
    ///     Application id
    /// </summary>
    public string? AppId => appId;

    /// <summary>
    ///     Intra-cluster routing identifier
    /// </summary>
    public string? ClusterId => clusterId;

    /// <summary>
    ///     True if <see cref="ContentType"/> is present
    /// </summary>
    public bool ContentTypePresent => contentType != default;

    /// <summary>
    ///     True if <see cref="ContentEncoding"/> is present
    /// </summary>
    public bool ContentEncodingPresent => contentEncoding != default;

    /// <summary>
    ///     True if <see cref="Headers"/> is present
    /// </summary>
    public bool HeadersPresent => headers?.Count > 0;

    /// <summary>
    ///     True if <see cref="DeliveryMode"/> is present
    /// </summary>
    public bool DeliveryModePresent => deliveryMode != default;

    /// <summary>
    ///     True if <see cref="Priority"/> is present
    /// </summary>
    public bool PriorityPresent => priority != default;

    /// <summary>
    ///     True if <see cref="CorrelationId"/> is present
    /// </summary>
    public bool CorrelationIdPresent => correlationId != default;

    /// <summary>
    ///     True if <see cref="ReplyTo"/> is present
    /// </summary>
    public bool ReplyToPresent => replyTo != default;

    /// <summary>
    ///     True if <see cref="Expiration"/> is present
    /// </summary>
    public bool ExpirationPresent => expiration != default;

    /// <summary>
    ///     True if <see cref="MessageId"/> is present
    /// </summary>
    public bool MessageIdPresent => messageId != default;

    /// <summary>
    ///     True if <see cref="Timestamp"/> is present
    /// </summary>
    public bool TimestampPresent => timestamp != default;

    /// <summary>
    ///     True if <see cref="Type"/> is present
    /// </summary>
    public bool TypePresent => type != default;

    /// <summary>
    ///     True if <see cref="UserId"/> is present
    /// </summary>
    public bool UserIdPresent => userId != default;

    /// <summary>
    ///     True if <see cref="AppId"/> is present
    /// </summary>
    public bool AppIdPresent => appId != default;

    /// <summary>
    ///     True if <see cref="ClusterId"/> is present
    /// </summary>
    public bool ClusterIdPresent => clusterId != default;

    /// <inheritdoc />
    public override string ToString()
    {
        object obj = this;
        return obj.GetType()
            .GetProperties()
            .Where(x => !x.Name.EndsWith("Present"))
            .Select(x => $"{x.Name}={GetValueString(x.GetValue(obj, null))}")
            .Intersperse(", ")
            .Aggregate(new StringBuilder(), (sb, x) => sb.Append(x))
            .ToString();
    }

    private static string GetValueString(object? value)
    {
        if (value == null) return "NULL";

        return value is IDictionary<string, object> dictionary
            ? dictionary
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}={x.Value}")
                .Intersperse(", ")
                .SurroundWith("[", "]")
                .Aggregate(new StringBuilder(), (builder, element) => builder.Append(element))
                .ToString()
            : value.ToString() ?? "NULL";
    }
}
