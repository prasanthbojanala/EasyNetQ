// ReSharper disable InconsistentNaming

using EasyNetQ.MessageVersioning;
using NSubstitute;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using EasyNetQ.Internals;
using Xunit;

namespace EasyNetQ.Tests.MessageVersioningTests;

public class VersionedMessageSerializationStrategyTests
{
    private const string AlternativeMessageTypesHeaderKey = "Alternative-Message-Types";

    [Fact]
    public void When_using_the_versioned_serialization_strategy_messages_are_correctly_serialized()
    {
        const string messageType = "MyMessageTypeName";
        var serializedMessageBody = Encoding.UTF8.GetBytes("Hello world!");
        const string correlationId = "CorrelationId";
        var messageTypes = new Dictionary<string, Type> { { messageType, typeof(MyMessage) } };

        var message = new Message<MyMessage>(new MyMessage());
        var serializationStrategy = CreateSerializationStrategy(message, messageTypes, serializedMessageBody, correlationId);

        var serializedMessage = serializationStrategy.SerializeMessage(message);

        AssertMessageSerializedCorrectly(serializedMessage, serializedMessageBody, p => AssertDefaultMessagePropertiesCorrect(p, messageType, correlationId));
    }

    [Fact]
    public void When_serializing_a_message_with_a_correlation_id_it_is_not_overwritten()
    {
        const string messageType = "MyMessageTypeName";
        var serializedMessageBody = Encoding.UTF8.GetBytes("Hello world!");
        const string correlationId = "CorrelationId";
        var messageTypes = new Dictionary<string, Type> { { messageType, typeof(MyMessage) } };

        var properties = new MessageProperties()
            .WithCorrelationId(correlationId);
        var message = new Message<MyMessage>(new MyMessage(), properties);

        var serializationStrategy = CreateSerializationStrategy(message, messageTypes, serializedMessageBody, "SomeOtherCorrelationId");
        var serializedMessage = serializationStrategy.SerializeMessage(message);

        AssertMessageSerializedCorrectly(serializedMessage, serializedMessageBody, p => AssertDefaultMessagePropertiesCorrect(p, messageType, correlationId));
    }

    [Fact]
    public void When_using_the_versioned_serialization_strategy_messages_are_correctly_deserialized()
    {
        const string messageType = "MyMessageTypeName";
        const string messageContent = "Hello world!";
        const string correlationId = "CorrelationId";
        var messageBody = Encoding.UTF8.GetBytes(messageContent);

        var messageTypes = new Dictionary<string, Type> { { messageType, typeof(MyMessage) } };

        var message = new Message<MyMessage>(
            new MyMessage { Text = messageContent },
            new MessageProperties()
                .WithType(messageType)
                .WithCorrelationId(correlationId)
                .WithUserId("Bob")
        );

        var serializationStrategy = CreateDeserializationStrategy(message.Body, messageTypes, typeof(MyMessage), messageBody);

        var deserializedMessage = serializationStrategy.DeserializeMessage(message.Properties, messageBody);

        AssertMessageDeserializedCorrectly((Message<MyMessage>)deserializedMessage, messageContent, typeof(MyMessage),
            p => AssertDefaultMessagePropertiesCorrect(p, messageType, correlationId));
        Assert.Equal(deserializedMessage.Properties.UserId, message.Properties.UserId); //, "Additional message properties not serialized");
    }

    [Fact]
    public void When_using_the_versioned_serialization_strategy_messages_are_correctly_round_tripped()
    {
        var typeNameSerializer = new DefaultTypeNameSerializer();
        var serializer = new JsonSerializer();

        const string correlationId = "CorrelationId";

        var serializationStrategy =
            new VersionedMessageSerializationStrategy(typeNameSerializer, serializer, new StaticCorrelationIdGenerationStrategy(correlationId));

        var messageBody = new MyMessage { Text = "Hello world!" };
        var message = new Message<MyMessage>(messageBody);
        var serializedMessage = serializationStrategy.SerializeMessage(message);
        var deserializedMessage = serializationStrategy.DeserializeMessage(serializedMessage.Properties, serializedMessage.Body);

        Assert.Equal(deserializedMessage.MessageType, message.Body.GetType());
        Assert.Equal(((Message<MyMessage>)deserializedMessage).Body.Text, message.Body.Text);
    }

    [Fact]
    public void When_using_the_versioned_serialization_strategy_versioned_messages_are_correctly_serialized()
    {
        const string messageType = "MyMessageV2TypeName";
        const string supersededMessageType = "MyMessageTypeName";
        var serializedMessageBody = Encoding.UTF8.GetBytes("Hello world!");
        const string correlationId = "CorrelationId";
        var messageTypes = new Dictionary<string, Type>
        {
            { messageType, typeof(MyMessageV2) },
            { supersededMessageType, typeof(MyMessage) }
        };

        var message = new Message<MyMessageV2>(new MyMessageV2());
        var serializationStrategy = CreateSerializationStrategy(message, messageTypes, serializedMessageBody, correlationId);

        var serializedMessage = serializationStrategy.SerializeMessage(message);

        AssertMessageSerializedCorrectly(serializedMessage, serializedMessageBody,
            p => AssertVersionedMessagePropertiesCorrect(p, messageType, correlationId, supersededMessageType));
    }

    [Fact]
    public void When_serializing_a_versioned_message_with_a_correlation_id_it_is_not_overwritten()
    {
        const string messageType = "MyMessageV2TypeName";
        const string supersededMessageType = "MyMessageTypeName";
        var serializedMessageBody = Encoding.UTF8.GetBytes("Hello world!");
        const string correlationId = "CorrelationId";
        var messageTypes = new Dictionary<string, Type>
        {
            { messageType, typeof(MyMessageV2) },
            { supersededMessageType, typeof(MyMessage) }
        };

        var message = new Message<MyMessageV2>(new MyMessageV2(), new MessageProperties().WithCorrelationId(correlationId));

        var serializationStrategy = CreateSerializationStrategy(message, messageTypes, serializedMessageBody, "SomeOtherCorrelationId");

        var serializedMessage = serializationStrategy.SerializeMessage(message);

        AssertMessageSerializedCorrectly(serializedMessage, serializedMessageBody,
            p => AssertVersionedMessagePropertiesCorrect(p, messageType, correlationId, supersededMessageType));
    }

    [Fact]
    public void When_using_the_versioned_serialization_strategy_versioned_messages_are_correctly_deserialized()
    {
        const string messageType = "MyMessageV2TypeName";
        const string supersededMessageType = "MyMessageTypeName";
        const string messageContent = "Hello world!";
        var messageBody = Encoding.UTF8.GetBytes(messageContent);

        const string correlationId = "CorrelationId";
        var messageTypes = new Dictionary<string, Type>
        {
            { messageType, typeof(MyMessageV2) },
            { supersededMessageType, typeof(MyMessage) }
        };

        var message = new Message<MyMessageV2>(
            new MyMessageV2 { Text = messageContent },
            new MessageProperties()
                .WithType(messageType)
                .WithCorrelationId(correlationId)
                .WithUserId("Bob")
                .WithHeader("Alternative-Message-Types", Encoding.UTF8.GetBytes(supersededMessageType))
        );

        var serializationStrategy = CreateDeserializationStrategy(message.Body, messageTypes, typeof(MyMessageV2), messageBody);

        var deserializedMessage = serializationStrategy.DeserializeMessage(message.Properties, messageBody);

        AssertMessageDeserializedCorrectly(
            (Message<MyMessageV2>)deserializedMessage,
            messageContent,
            typeof(MyMessageV2),
            p => AssertVersionedMessagePropertiesCorrect(p, messageType, correlationId, supersededMessageType)
        );
    }

    [Fact]
    public void When_using_the_versioned_serialization_strategy_versioned_messages_are_correctly_round_tripped()
    {
        var typeNameSerializer = new DefaultTypeNameSerializer();
        var serializer = new JsonSerializer();
        const string correlationId = "CorrelationId";

        var serializationStrategy =
            new VersionedMessageSerializationStrategy(typeNameSerializer, serializer, new StaticCorrelationIdGenerationStrategy(correlationId));

        var messageBody = new MyMessageV2 { Text = "Hello world!", Number = 5 };
        var message = new Message<MyMessageV2>(messageBody);
        var serializedMessage = serializationStrategy.SerializeMessage(message);


        // RMQ converts the Header values into a byte[] so mimic the translation here
        var alternativeMessageHeader = (string)serializedMessage.Properties.Headers![AlternativeMessageTypesHeaderKey];
        var updatedSerializedMessage = new SerializedMessage(
            serializedMessage.Properties.WithHeader(AlternativeMessageTypesHeaderKey, Encoding.UTF8.GetBytes(alternativeMessageHeader)),
            new TestMemoryOwner(serializedMessage.Body)
        );

        var deserializedMessage = serializationStrategy.DeserializeMessage(updatedSerializedMessage.Properties, updatedSerializedMessage.Body);

        Assert.Equal(deserializedMessage.MessageType, message.Body.GetType());
        Assert.Equal(((Message<MyMessageV2>)deserializedMessage).Body.Text, message.Body.Text);
        Assert.Equal(((Message<MyMessageV2>)deserializedMessage).Body.Number, message.Body.Number);
    }

    [Fact]
    public void When_deserializing_versioned_message_use_first_available_message_type()
    {
        var typeNameSerializer = new DefaultTypeNameSerializer();
        var serializer = new JsonSerializer();
        const string correlationId = "CorrelationId";

        var serializationStrategy =
            new VersionedMessageSerializationStrategy(typeNameSerializer, serializer, new StaticCorrelationIdGenerationStrategy(correlationId));

        var messageBody = new MyMessageV2 { Text = "Hello world!", Number = 5 };
        var message = new Message<MyMessageV2>(messageBody);
        var serializedMessage = serializationStrategy.SerializeMessage(message);

        // Mess with the properties to mimic a message serialized as MyMessageV3

        var messageType = serializedMessage.Properties.Type!;

        var alternativeMessageHeader = (string)serializedMessage.Properties.Headers![AlternativeMessageTypesHeaderKey];
        alternativeMessageHeader = string.Concat(messageType, ";", alternativeMessageHeader);

        serializedMessage = new SerializedMessage(
            serializedMessage.Properties
                .WithType(messageType.Replace("MyMessageV2", "SomeCompletelyRandomType"))
                .WithHeader(AlternativeMessageTypesHeaderKey, Encoding.UTF8.GetBytes(alternativeMessageHeader)),
            new TestMemoryOwner(serializedMessage.Body)
        );

        var deserializedMessage = serializationStrategy.DeserializeMessage(serializedMessage.Properties, serializedMessage.Body);

        Assert.Equal(typeof(MyMessageV2), deserializedMessage.MessageType);
        Assert.Equal(((Message<MyMessageV2>)deserializedMessage).Body.Text, message.Body.Text);
        Assert.Equal(((Message<MyMessageV2>)deserializedMessage).Body.Number, message.Body.Number);
    }

    private static void AssertMessageSerializedCorrectly(
        SerializedMessage message, byte[] expectedBody, Action<MessageProperties> assertMessagePropertiesCorrect
    )
    {
        Assert.Equal(message.Body, expectedBody); //, "Serialized message body does not match expected value");
        assertMessagePropertiesCorrect(message.Properties); //;
    }

    private static void AssertMessageDeserializedCorrectly(
        IMessage<MyMessage> message,
        string expectedBodyText,
        Type expectedMessageType,
        Action<MessageProperties> assertMessagePropertiesCorrect
    )
    {
        Assert.Equal(message.Body.Text, expectedBodyText); //, "Deserialized message body text does not match expected value");
        Assert.Equal(message.MessageType, expectedMessageType); //, "Deserialized message type does not match expected value");

        assertMessagePropertiesCorrect(message.Properties);
    }

    private static void AssertDefaultMessagePropertiesCorrect(MessageProperties properties, string expectedType, string expectedCorrelationId)
    {
        Assert.Equal(properties.Type, expectedType); //, "Message type does not match expected value");
        Assert.Equal(properties.CorrelationId, expectedCorrelationId); //, "Message correlation id does not match expected value");
    }

    private static void AssertVersionedMessagePropertiesCorrect(
        MessageProperties properties, string expectedType, string expectedCorrelationId, string alternativeTypes
    )
    {
        AssertDefaultMessagePropertiesCorrect(properties, expectedType, expectedCorrelationId);
        Assert.Equal(properties.Headers?[AlternativeMessageTypesHeaderKey], alternativeTypes); //, "Alternative message types do not match expected value");
    }

    private static VersionedMessageSerializationStrategy CreateSerializationStrategy<T>(
        IMessage<T> message, IEnumerable<KeyValuePair<string, Type>> messageTypes, byte[] messageBody, string correlationId
    )
    {
        var typeNameSerializer = Substitute.For<ITypeNameSerializer>();
        foreach (var messageType in messageTypes)
        {
            var localMessageType = messageType;
            typeNameSerializer.Serialize(localMessageType.Value).Returns(localMessageType.Key);
        }

        var serializer = Substitute.For<ISerializer>();
        var serializedMessage = Substitute.For<IMemoryOwner<byte>>();
        serializedMessage.Memory.Returns(messageBody);
        serializer.MessageToBytes(typeof(T), message.GetBody()).Returns(serializedMessage);

        return new VersionedMessageSerializationStrategy(typeNameSerializer, serializer, new StaticCorrelationIdGenerationStrategy(correlationId));
    }

    private static VersionedMessageSerializationStrategy CreateDeserializationStrategy<T>(
        T message, IEnumerable<KeyValuePair<string, Type>> messageTypes, Type expectedMessageType, byte[] messageBody
    )
    {
        var typeNameSerializer = Substitute.For<ITypeNameSerializer>();
        foreach (var messageType in messageTypes)
        {
            var localMessageType = messageType;
            typeNameSerializer.Deserialize(localMessageType.Key).Returns(localMessageType.Value);
        }

        var serializer = Substitute.For<ISerializer>();
        serializer.BytesToMessage(expectedMessageType, messageBody).Returns(message);

        return new VersionedMessageSerializationStrategy(typeNameSerializer, serializer, new StaticCorrelationIdGenerationStrategy(string.Empty));
    }


    private class TestMemoryOwner : IMemoryOwner<byte>
    {
        private readonly byte[] bytes;

        public TestMemoryOwner(ReadOnlyMemory<byte> readOnlyMemory) => bytes = readOnlyMemory.ToArray();

        public void Dispose()
        {
        }

        public Memory<byte> Memory => bytes;
    }
}
