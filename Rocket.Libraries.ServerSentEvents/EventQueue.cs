using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Rocket.Libraries.ServerSentEvents
{
    public interface IEventQueue
    {
        Task CloseAsync(object queueId);
        Task DequeueAsync(object queueId);
        Task EnqueueManyAsync(object queueId, IEnumerable<string> messages);

        Task EnqueueSingleAsync(object queueId, string message);
    }

    public class EventQueue : IEventQueue
    {
        private string terminateMessage = "---terminate---";

        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private Dictionary<string,string> messageQueue = new Dictionary<string, string>();
        private HashSet<string> scheduledForTermination = new HashSet<string>();
        private readonly IHttpContextAccessor httpContextAccessor;

        public EventQueue(
            IHttpContextAccessor httpContextAccessor
        )
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public async Task CloseAsync(object queueId)
        {
            try
            {
                await semaphoreSlim.WaitAsync();
                var queueIdString = getQueueIdString(queueId);
                if (!scheduledForTermination.Contains(queueIdString))
                {
                    scheduledForTermination.Add(queueIdString);
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task EnqueueManyAsync(object queueId, IEnumerable<string> messages)
        {
            try
            {
                var queueIdString = getQueueIdString(queueId);
                await semaphoreSlim.WaitAsync();
                if (!messageQueue.ContainsKey(queueIdString))
                {
                    messageQueue.Add(queueIdString, string.Empty);
                }
                var combinedMessage = string.Join("<br/>", messages);
                messageQueue[queueIdString] += "<br/>" + combinedMessage;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }


        

        
        public async Task DequeueAsync(object queueId)
        {
            try
            {
                var queueIdString = getQueueIdString(queueId);
                await semaphoreSlim.WaitAsync();
                if (messageQueue.ContainsKey(queueIdString))
                {
                    if(string.IsNullOrEmpty(messageQueue[queueIdString]) && scheduledForTermination.Contains(queueIdString))
                    {
                        messageQueue[queueIdString] = terminateMessage;
                    }
                    var message = messageQueue[queueIdString];
                    messageQueue[queueIdString] = string.Empty;
                    var isTerminate = message == terminateMessage;
                    if (isTerminate)
                    {
                        messageQueue.Remove(queueIdString);
                        scheduledForTermination.Remove(queueIdString);
                    }
                    await TransmitAsync(message);
                }
                else
                {
                    await TransmitAsync(string.Empty);
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private async Task TransmitAsync(string text)
        {
            httpContextAccessor.HttpContext.Response.Headers.Add("Content-Type", "text/event-stream");
            if (!string.IsNullOrEmpty(text))
            {
                text = $"data: {text}\n\n";
                var messageBytes = System.Text.Encoding.ASCII.GetBytes(text);
                await httpContextAccessor.HttpContext.Response.Body.WriteAsync(messageBytes, 0, messageBytes.Length);
            }
            await httpContextAccessor.HttpContext.Response.Body.FlushAsync();
        }

        private string getQueueIdString(object queueId)
        {
            if (queueId == default)
            {
                throw new ArgumentNullException($"{nameof(queueId)} cannot be null");
            }
            return queueId.ToString().ToLower();
        }

        public async Task EnqueueSingleAsync(object queueId, string message)
        {
            await EnqueueManyAsync(queueId, new List<string>{ message});

        }
    }
}
