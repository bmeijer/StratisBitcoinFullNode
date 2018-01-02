﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.P2P.Protocol
{
    public interface IMessageListener<in T>
    {
        void PushMessage(T message);
    }

    public class EventLoopMessageListener<T> : IMessageListener<T>, IDisposable
    {
        private BlockingCollection<T> messageQueue = new BlockingCollection<T>(new ConcurrentQueue<T>());
        public BlockingCollection<T> MessageQueue { get { return this.messageQueue; } }

        private CancellationTokenSource cancellationSource = new CancellationTokenSource();

        public EventLoopMessageListener(Func<T, Task> processMessageAsync)
        {
            new Thread(new ThreadStart(async () =>
            {
                try
                {
                    while (!this.cancellationSource.IsCancellationRequested)
                    {
                        T message = this.messageQueue.Take(this.cancellationSource.Token);
                        if (message != null)
                        {
                            try
                            {
                                await processMessageAsync(message);
                            }
                            catch (Exception ex)
                            {
                                NodeServerTrace.Error("Unexpected expected during message loop", ex);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            })).Start();
        }

        public void PushMessage(T message)
        {
            this.messageQueue.Add(message);
        }

        public void Dispose()
        {
            if (this.cancellationSource.IsCancellationRequested)
                return;

            this.cancellationSource.Cancel();
            this.cancellationSource.Dispose();
        }
    }

    public class PollMessageListener<T> : IMessageListener<T>
    {
        private BlockingCollection<T> messageQueue = new BlockingCollection<T>(new ConcurrentQueue<T>());
        public BlockingCollection<T> MessageQueue { get { return this.messageQueue; } }

        public virtual T ReceiveMessage(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.MessageQueue.Take(cancellationToken);
        }

        public virtual void PushMessage(T message)
        {
            this.messageQueue.Add(message);
        }
    }
}