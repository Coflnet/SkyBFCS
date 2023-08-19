using System.Threading.Tasks;
using System;
using System.Threading;
using Confluent.Kafka;
using System.Collections.Generic;

namespace Coflnet.Sky.BFCS.Services
{
    public class MockProd<TPayload> : IProducer<string, TPayload>
    {
        private Action<TPayload> action;

        public MockProd(Action<TPayload> action)
        {
            this.action = action;
        }

        public Handle Handle => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public void AbortTransaction(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public void AbortTransaction()
        {
            throw new NotImplementedException();
        }

        public int AddBrokers(string brokers)
        {
            throw new NotImplementedException();
        }

        public void BeginTransaction()
        {
            throw new NotImplementedException();
        }

        public void CommitTransaction(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public void CommitTransaction()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            // nothing to do
        }

        public int Flush(TimeSpan timeout)
        {
            return 1;
        }

        public void Flush(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void InitTransactions(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public int Poll(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public void Produce(string topic, Message<string, TPayload> message, Action<DeliveryReport<string, TPayload>> deliveryHandler = null)
        {
            action(message.Value);
        }

        public void Produce(TopicPartition topicPartition, Message<string, TPayload> message, Action<DeliveryReport<string, TPayload>> deliveryHandler = null)
        {
            throw new NotImplementedException();
        }

        public Task<DeliveryResult<string, TPayload>> ProduceAsync(string topic, Message<string, TPayload> message, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeliveryResult<string, TPayload>> ProduceAsync(TopicPartition topicPartition, Message<string, TPayload> message, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public void SetSaslCredentials(string username, string password)
        {
            throw new NotImplementedException();
        }
    }
}
