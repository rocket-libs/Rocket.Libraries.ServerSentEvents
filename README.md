
  

#  Rocket.Libraries.ServerSentEvents

A library to allow easy communication between client and server over Server Sent Events.

  

#  Quick Start

##  1. Installation

Install the [package from nuget](https://www.nuget.org/packages/Rocket.Libraries.ServerSentEvents)

  

##  2. Configure your dotnet app

1. Include in your ***startup.cs*** file.

`using ServerSentEvents;`

2. Register services auth library in your ***ConfigureServices*** method.

```csharp

public  void  ConfigureServices (IServiceCollection services)

{

//This will allow us to inject the IEventQueue interface into custom classes.
services.SetupRocketServerSentEventsSingleton();

//Register other services

}

```

##  3. Writing and Reading Messages
Rocket Server Sent Events uses a queue to store and retrieve messages.
The server writes to the queue, and the client periodically polls the queue for new content.

As with standard Queues, this is a first in first out system, and each message can only be read once, as it is removed from the queue when it is read.

### Writing To The Queue
Below is an example class that is writing to the queue.

```csharp
	namespace Example
	{
		public class MyLongRunningProcess
		{
			private readonly Rocket.Libraries.ServerSentEvents.eventQueue;
			
			//Access the IEventQueue interface via DI.
			public MyLongRunningProcess(Rocket.Libraries.ServerSentEvents.IEventQueue eventQueue)
			{
				this.eventQueue = eventQueue;
			}

			public async Task DoLongProcess()
			{
				var queueId = System.Guid.NewGuid();
				//Adding a single message to the queue.
				await eventQueue.EnqueueSingleAsync(queueId, "Starting process.");
				
				//Adding multiple message to queue
				await  eventQueue.EnqueueManyAsync (
					queueId,
					ImmutableList<string>.Empty
						.Add ("Message One")
						.Add ("Message Two")
						.Add ("Message Three")
						.Add ("Message Four")
				);

				//Closing the queue -- let the client know not to expect any new messages.
				await eventQueue.CloseAsync(object queueId)
			}

		}
	}
```

That's pretty much it to queuing messages. The main point of note, is that as the library is built to server multiple clients simultaneously, when writing to the queue, a ***queueId*** is required, to help segregate messages meant for different clients. 

You may use a value of any type for ***queueId***. In the example above, we use a GUID.

### Reading From The Queue
Generally  clients will be reading messages over HTTP, so our example will show how to read messages using a Controller.

```csharp
	namespace Example
	{
		[Route("api/v1/[controller]")]
		[ApiController]
		public class ServerSentMessagesController
		{
			private readonly Rocket.Libraries.ServerSentEvents.eventQueue;
			
			//Access the IEventQueue interface via DI.
			public ServerSentMessagesController(Rocket.Libraries.ServerSentEvents.IEventQueue eventQueue)
			{
				this.eventQueue = eventQueue;
			}

			[HttpGet("listen")]
			public  async  Task  ListenAsync([FromQuery] Guid queueId)
			{
				await eventQueue.DequeueAsync(queueId);
			}
		}
	}
```

The Controller above now allows use to call to poll the server for messages from our client over HTTP. 

As different technologies implement Server Sent Event message polling in their own specific ways, it wouldn't make much sense to pick an arbitrary client technology and show an example for it.

If however your client is using JavaScript, then the [Receiving events from the server](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events#receiving_events_from_the_server "Permalink to Receiving events from the server") section on the MDN Web Docs by Mozilla should suffice to explain how you can implement the client side part of your solution.

### Note On Closing The Queue.
Once you're done sending messages to the client, you should call the ``` Task CloseAsync(Object queueId) ``` method on the server (as shown in the ***Writing To The Queue*** section above)

Calling this message on the server, causes a special termination message (**\-\-\-terminate\-\-\-**) to be inserted into the queue. When your client receives this message, then not additional messages from the specific queue will ever be sent by the server, so it is safe now to close the the connection and stop polling.
