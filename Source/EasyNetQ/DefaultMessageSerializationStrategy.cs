using System;
using EasyNetQ.Internals;

namespace EasyNetQ;

/// <inheritdoc />
public class DefaultMessageSerializationStrategy : IMessageSerializationStrategy
{
    private readonly ITypeNameSerializer typeNameSerializer;
    private readonly ISerializer serializer;
    private readonly ICorrelationIdGenerationStrategy correlationIdGenerator;

    /// <summary>
    ///     Creates DefaultMessageSerializationStrategy
    /// </summary>
    /// <param name="typeNameSerializer">The type name serialized</param>
    /// <param name="serializer">The serializer</param>
    /// <param name="correlationIdGenerator">The correlation id generator</param>
    public DefaultMessageSerializationStrategy(
        ITypeNameSerializer typeNameSerializer,
        ISerializer serializer,
        ICorrelationIdGenerationStrategy correlationIdGenerator
    )
    {
        this.typeNameSerializer = typeNameSerializer;
        this.serializer = serializer;
        this.correlationIdGenerator = correlationIdGenerator;
    }

    /// <inheritdoc />
    public SerializedMessage SerializeMessage(IMessage message)
    {
        var typeName = typeNameSerializer.Serialize(message.MessageType);
        var messageBody = message.GetBody() is null
            ? new ArrayPooledMemoryStream()
            : serializer.MessageToBytes(message.MessageType, message.GetBody()!);

        var messageProperties = message.Properties.WithType(typeName);
        if (string.IsNullOrEmpty(messageProperties.CorrelationId))
            messageProperties = messageProperties.WithCorrelationId(correlationIdGenerator.GetCorrelationId());

        return new SerializedMessage(messageProperties, messageBody);
    }

    /// <inheritdoc />
    public IMessage DeserializeMessage(MessageProperties properties, in ReadOnlyMemory<byte> body)
    {
        var messageType = typeNameSerializer.Deserialize(properties.Type!);
        var messageBody = body.IsEmpty ? null : serializer.BytesToMessage(messageType, body);
        return MessageFactory.CreateInstance(messageType, messageBody, properties);
    }
}
