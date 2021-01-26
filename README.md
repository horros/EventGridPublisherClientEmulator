# EventGridPublisherClientEmulator

This is a class that "emulates" EventGridPublisherClient in the new Azure.Messaging.EventGrid 4.0.0-beta release.

## Problem

Developing locally with EventGrid is possible, using something like ngrok to open a tunnel from the outside and adding a new Event Subscription to EventGrid pointing to the ngrok tunnel. This is less than ideal in many cases, especially when developing larger event driven microservices apps.

## Solution

Paul Mcilreavy developed the awesome [EventGridSimulator](https://github.com/pmcilreavy/AzureEventGridSimulator) that can act as Azure EventGrid on your local computer. However, try as I may, I could not get the EventGridPublisherClient in the EventGrid 4.0.0-beta4 API to play ball, it kept crashing with an error saying "The SSL connection could not be established" and nothing more of use. I tried everything I could think of at the time, even constructing a new HttpClientHandler for which I disabled certificate validation completely and used that as parameter for a new HttpClient which was used as a parameter for a new HttpClientTransport which was set as the transport on a EventGridPublisherClientOptions object that was passed to EventGridPublisherClient, but nothing worked.

I know EventGridPublisherClient does stuff like force the protocol to be HTTPS regardless of what you pass in as the URI and override the query path etc, so I spent a little while writing together an "emulator" of sorts for EventGridPublisherClient. Since EventGridSimulator does not support CloudEvents I didn't implement support for it in the emulator either, though this should be trivial to add. All the "emulator" does is override
```csharp
async Task<Response> SendEventsAsync(IEnumerable<EventGridEvent> eventGridEvents, CancellationToken cancellationToken = default)
```
and construct a JSON representation of the events, set the correct headers and post that to the URI specified, in this case EventGridSimulator. Using this you could roll your own local Event Grid-y thing if you wish, but I warmly recommed EventGridSimulator.

## Usage

Build the project and add it as a reference in your own project. Or just copy the .cs file to your project.

My quickly cobbled together thing registers a singleton service of type EventGridPublisherClient in `Startup.cs`, except if the environment is Development, we instantiate a ClientEmulator class instead of the EventGridPublisherClient:

```csharp
    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
    {
        services.AddSingleton<EventGridPublisherClient>(clientEmulator =>
        {
            return new ClientEmulator(new Uri("https://localhost:60102/api/events?api-version=2018-01-01"), new AzureKeyCredential("DummyKey"));
        }); 
    }
    else
    {
        string topicEndpoint = Environment.GetEnvironmentVariable("EVENT_GRID_TOPIC_ENDPOINT");
        string key = Environment.GetEnvironmentVariable("EVENT_GRID_KEY");
        services.AddSingleton<EventGridPublisherClient>(clientEmulator =>
        {
            return new EventGridPublisherClient(new Uri(topicEndpoint), new AzureKeyCredential(key));
        });
    }
```

Then I do something like this in my MediatR Notification Handler:

```csharp
public class ExampleHandler : INotificationHandler<ExampleEvent>
{
    private readonly EventGridPublisherClient client;

    public ExampleHandler(EventGridPublisherClient client) {
        this.client = client;
    }
    
    public async Task Handle(ExampleEvent notification, CancellationToken cancellationToken = default) {
        await client.SendEventsAsync(GetEventsList(notification), cancellationToken);
    }

    static IList<EventGridEvent> GetEventsList(ExampleEvent notification) 
    {
        // Get a list of events etc
    }
}
```

For any questions, comments or improvements, open an issue.
