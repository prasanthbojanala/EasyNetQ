// ReSharper disable InconsistentNaming

using System;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace EasyNetQ.Tests;

public class MessagePropertiesTests
{
    [Fact]
    public void Should_copy_from_Rabbit_client_properties()
    {
        const string replyTo = "reply to";

        var originalProperties = new BasicProperties { ReplyTo = replyTo };
        var properties = new MessageProperties(originalProperties);

        properties.ReplyTo.Should().Be(replyTo);
    }

    [Fact]
    public void Should_copy_to_rabbit_client_properties()
    {
        const string replyTo = "reply to";

        var properties = new MessageProperties().WithReplyTo(replyTo);
        var destinationProperties = new BasicProperties();

        properties.CopyTo(destinationProperties);

        destinationProperties.ReplyTo.Should().Be(replyTo);
        destinationProperties.IsReplyToPresent().Should().BeTrue();
        destinationProperties.IsMessageIdPresent().Should().BeFalse();
    }

    [Fact]
    public void Should_be_able_to_write_debug_properties()
    {
        const string expectedDebugProperties =
            "ContentType=content_type, ContentEncoding=content_encoding, " +
            "Headers=[key1=value1, key2=value2], DeliveryMode=10, Priority=3, CorrelationId=NULL, " +
            "ReplyTo=reply_to, Expiration=00:00:00.0010000, MessageId=message_id, Timestamp=123456, Type=type, " +
            "UserId=user_id, AppId=app_id, ClusterId=cluster_id";

        var headers = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        var properties = new MessageProperties()
            .WithAppId("app_id")
            .WithClusterId("cluster_id")
            .WithContentEncoding("content_encoding")
            .WithContentType("content_type")
            .WithDeliveryMode(10)
            .WithExpiration(TimeSpan.FromMilliseconds(1))
            .WithHeaders(headers)
            .WithPriority(3)
            .WithMessageId("message_id")
            .WithReplyTo("reply_to")
            .WithTimestamp(123456)
            .WithType("type")
            .WithUserId("user_id");

        properties.ToString().Should().Be(expectedDebugProperties);
    }
}

// ReSharper restore InconsistentNaming
