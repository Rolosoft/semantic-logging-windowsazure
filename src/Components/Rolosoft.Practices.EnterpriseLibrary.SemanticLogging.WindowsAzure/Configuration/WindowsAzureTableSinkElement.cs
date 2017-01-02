// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Configuration
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Xml.Linq;
    using Observable;
    using SemanticLogging.Utility;
    using SemanticLogging.Configuration;

    internal class WindowsAzureTableSinkElement : ISinkElement
    {
        private readonly XName sinkName = XName.Get("windowsAzureTableSink", Constants.Namespace);

        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public bool CanCreateSink(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");

            return element.Name == sinkName;
        }

        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Validated with Guard class")]
        public IObserver<EventEntry> CreateSink(XElement element)
        {
            Guard.ArgumentNotNull(element, "element");

            var subject = new EventEntrySubject();
            subject.LogToWindowsAzureTable(
                (string)element.Attribute("instanceName"),
                (string)element.Attribute("connectionString"),
                (string)element.Attribute("tableAddress") ?? WindowsAzureTableLog.DefaultTableName,
                element.Attribute("bufferingIntervalInSeconds").ToTimeSpan(),
                (bool?)element.Attribute("sortKeysAscending") ?? false,
                element.Attribute("bufferingFlushAllTimeoutInSeconds").ToTimeSpan() ?? Constants.DefaultBufferingFlushAllTimeout,
                (int?)element.Attribute("maxBufferSize") ?? Buffering.DefaultMaxBufferSize);

            return subject;
        }
    }
}
