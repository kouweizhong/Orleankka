﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using NUnit.Framework;

using Orleans;
using Orleans.Streams;

namespace Orleankka.Features
{
    namespace Stream_references
    {
        using Meta;
        using Testing;

        [Serializable]
        public class Produce : Command
        {
            public string Event;
        }

        [Serializable]
        public class Subscribe : Command
        {}

        [Serializable]
        public class Received : Query<List<string>>
        {}

        class TestProducerActor : Actor
        {
            Task On(Produce cmd)
            {
                var stream = System.StreamOf("sms", "123");
                return stream.OnNextAsync(cmd.Event);
            }
        }

        class TestConsumerActor : Actor
        {
            readonly TestStreamObserver observer = new TestStreamObserver();

            Task On(Subscribe x)
            {
                var stream = System.StreamOf("sms", "123");
                return stream.SubscribeAsync(observer);
            }

            List<string> On(Received x) => observer.Received;
        }

        class TestStreamObserver : IAsyncObserver<string>
        {
            public readonly List<string> Received = new List<string>();

            public Task OnNextAsync(string item, StreamSequenceToken token = null)
            {
                Received.Add(item);
                return TaskDone.Done;
            }

            public Task OnCompletedAsync() => TaskDone.Done;
            public Task OnErrorAsync(Exception ex) => TaskDone.Done;
        }

        [TestFixture]
        [RequiresSilo]
        public class Tests
        {
            IActorSystem system;

            [SetUp]
            public void SetUp()
            {
                system = TestActorSystem.Instance;
            }

            [Test]
            public async void Client_to_stream()
            {
                var stream = system.StreamOf("sms", "123");
                
                var observer = new TestStreamObserver();
                await stream.SubscribeAsync(observer);

                await stream.OnNextAsync("event");
                await Task.Delay(100);

                Assert.That(observer.Received.Count, Is.EqualTo(1));
                Assert.That(observer.Received[0], Is.EqualTo("event"));
            }

            [Test]
            public async void Actor_to_stream()
            {
                var producer = system.ActorOf<TestProducerActor>("p");
                var consumer = system.ActorOf<TestConsumerActor>("c");

                await consumer.Tell(new Subscribe());
                await producer.Tell(new Produce {Event = "event"});

                await Task.Delay(100);
                var received = await consumer.Ask(new Received());
                
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0], Is.EqualTo("event"));
            }
        }
    }
}