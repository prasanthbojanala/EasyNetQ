using System;
using EasyNetQ.Internals;

namespace EasyNetQ.MessageVersioning;

/// <inheritdoc />
public class VersionedMessageSerializationStrategy : IMessageSerializationStrategy
{
    private readonly ITypeNameSerializer typeNameSerializer;
    private readonly ISerializer serializer;
    private readonly ICorrelationIdGenerationStrategy correlationIdGenerator;

    /// <summary>
    ///     Creates VersionedMessageSerializationStrategy
    /// </summary>
    public VersionedMessageSerializationStrategy(
        ITypeNameSerializer typeNameSerializer, ISerializer serializer, ICorrelationIdGenerationStrategy correlationIdGenerator
    )
    {
        this.typeNameSerializer = typeNameSerializer;
        this.serializer = serializer;
        this.correlationIdGenerator = correlationIdGenerator;
    }

    /// <inheritdoc />
    public SerializedMessage SerializeMessage(IMessage message)
    {
        var messageBody = message.GetBody() is null
            ? new ArrayPooledMemoryStream()
            : serializer.MessageToBytes(message.MessageType, message.GetBody()!);
        var messageTypeProperties = MessageTypeProperty.CreateForMessageType(message.MessageType, typeNameSerializer);
        var messageProperties = message.Properties;
        messageProperties = messageTypeProperties.AppendTo(messageProperties);
        if (string.IsNullOrEmpty(messageProperties.CorrelationId))
            messageProperties = messageProperties.WithCorrelationId(correlationIdGenerator.GetCorrelationId());
        return new SerializedMessage(messageProperties, messageBody);
    }

    /// <inheritdoc />
    public IMessage DeserializeMessage(MessageProperties properties, in ReadOnlyMemory<byte> body)
    {
        var messageTypeProperty = MessageTypeProperty.ExtractFromProperties(properties, typeNameSerializer);
        var messageType = messageTypeProperty.GetMessageType();
        var messageBody = body.IsEmpty ? null : serializer.BytesToMessage(messageType, body);
        messageTypeProperty.AppendTo(properties);
        return MessageFactory.CreateInstance(messageType, messageBody, properties);
    }
}
