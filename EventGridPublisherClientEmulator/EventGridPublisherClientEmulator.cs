using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Messaging.EventGrid;

namespace EventGridPublisherClientEmulator
{
    public class ClientEmulator : EventGridPublisherClient
    {

        private readonly HttpClient client;
        private readonly Uri responseUri;
        private readonly AzureKeyCredential credential;

        public ClientEmulator(Uri responseUri)
        {
            client = new HttpClient();            
            this.responseUri = responseUri;
        }

        public ClientEmulator(Uri responseUri, AzureKeyCredential credential)
        {
            client = new HttpClient();
            this.responseUri = responseUri;
            this.credential = credential;
        }

        /// <summary>
        /// Constructor that takes an extra HttpClient parameter,
        /// mainly for mocking in tests
        /// </summary>
        /// <param name="responseUri"></param>
        /// <param name="credentials"></param>
        /// <param name="client"></param>
        public ClientEmulator(Uri responseUri, AzureKeyCredential credentials, HttpClient client)
        {
            this.responseUri = responseUri;
            this.client = client;
        }

        // We don't support asynchronous methods (yet)
        public override Response SendEvents(IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default) 
        {
            throw new Exception("Synchronous calls not implemented");
        }

        public override Response SendEvents(IEnumerable<CloudEvent> events, CancellationToken cancellationToken = default)
        {
            throw new Exception("Synchronous calls not implemented");
        }

        public override Response SendEvents(IEnumerable<object> events, CancellationToken cancellationToken = default)
        {
            throw new Exception("Synchronous calls not implemented");
        }

        /// <summary>
        /// Send a collection of EventGridEvents
        /// </summary>
        /// <param name="eventGridEvents"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<Response> SendEventsAsync(IEnumerable<EventGridEvent> eventGridEvents, CancellationToken cancellationToken = default)
        {

            // This is a bit wonky, Data is not a publicly accessible field
            // in EventGridEvent, you can fetch data by calling GetData on the event.
            // Thus, we need to (re)construct the JSON payload ourselves. Fun!
            List<Dictionary<string,object>> events = new List<Dictionary<string,object>>();

            foreach (var gridEvent in eventGridEvents)
            {
                Dictionary<string, object> tempEvent = new Dictionary<string, object>();
                tempEvent.Add("Id", gridEvent.Id);
                tempEvent.Add("Subject", gridEvent.Subject);
                tempEvent.Add("DataVersion", gridEvent.DataVersion);
                tempEvent.Add("EventTime", gridEvent.EventTime);
                tempEvent.Add("EventType", gridEvent.EventType);
                tempEvent.Add("Data", gridEvent.GetData());
                events.Add(tempEvent);
            }


            var content = JsonSerializer.Serialize(events);
            var buffer = System.Text.Encoding.UTF8.GetBytes(content);
            var byteData = new ByteArrayContent(buffer);
            
            // Add the required headers
            byteData.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            byteData.Headers.Add("aeg-event-type", "Notification");
            byteData.Headers.Add("aeg-sas-key", credential.Key);
            
            var result =  await client.PostAsync(responseUri, byteData);

            return new FakeResponse(result);
        }

        public override Task<Response> SendEventsAsync(IEnumerable<CloudEvent> events, CancellationToken cancellationToken = default)
        {
            throw new Exception("CloudEvents are not supported");
        }

        public override Task<Response> SendEventsAsync(IEnumerable<object> events, CancellationToken cancellationToken = default)
        {
            throw new Exception("Custom events are not supported");
        }

    }

    /// <summary>
    /// Class that wraps the abstract Response class that EventGridPublisherClient 
    /// returns. Just take the HttpResponseMessage give to us by HttpClient 
    /// and copy the data.
    /// </summary>
    internal class FakeResponse : Response
    {
        readonly List<HttpHeader> headers = new List<HttpHeader>();

        public FakeResponse(HttpResponseMessage responseMessage)
        {
            foreach (var header in responseMessage.Headers)
            {
                foreach (var value in header.Value)
                {
                    headers.Add(new HttpHeader(header.Key, value));
                }
                
            }
            Status = (int)responseMessage.StatusCode;
            ReasonPhrase = responseMessage.ReasonPhrase;
            ContentStream = Task.Run(async () => await responseMessage.Content.ReadAsStreamAsync()).Result;
        }

        public override string ReasonPhrase { get; }
        public override Stream ContentStream  { get; set;}
        public override string ClientRequestId { get; set; }

        public override int Status { get; }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        protected override bool ContainsHeader(string name)
        {
            return headers.Exists(h => h.Name == name);
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            return headers;
        }

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string value)
        {
            if (headers.Exists(h => h.Name == name))
            {
                value = headers.Find(h => h.Name == name).Value;
                return true;
            } 
            else
            {
                value = null;
                return false;
            }
            
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string> values)
        {
            if (headers.Exists(h => h.Name == name))
            {
                List<string> outlist = new List<string>();

                foreach (var header in headers.FindAll(h => h.Name == name))
                {
                    outlist.Add(header.Value);
                }
                values = outlist;
                return true;
            }
            else
            {
                values = new List<string>();
                return false;
                
            }
        }
    }
}
