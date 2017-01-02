using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.Schema;

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.TestObjects
{
    public class MockEventListener : EventListener
    {
        public ConcurrentBag<EventEntry> WrittenEntries = new ConcurrentBag<EventEntry>();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            WrittenEntries.Add(EventEntry.Create(eventData, EventSourceSchemaCache.Instance.GetSchema(eventData.EventId, eventData.EventSource)));
        }
    }
}