﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Utility
{
    using System;
    using Observable;
    using SemanticLogging.Utility;
    using Sinks;

    /// <summary>
    /// Extensions for <see cref="EventEntry"/>.
    /// </summary>
    public static class CloudEventEntryExtensions
    {
        /// <summary>
        /// Subscribes an <see cref="IObserver{T}"/> sink by doing a straight projection of a sequence of <see cref="EventEntry"/>
        /// and converting it to a <see cref="CloudEventEntry"/> entity.
        /// </summary>
        /// <param name="source">The original stream of events.</param>
        /// <param name="sink">The underlying sink.</param>
        /// <returns>A subscription token to unsubscribe to the event stream.</returns>
        /// <remarks>When using Reactive Extensions (Rx), this is equivalent to doing a Select statement on the <paramref name="source"/> to convert it to <see cref="IObservable{String}"/> and then
        /// calling Subscribe on it.
        /// </remarks>
        public static IDisposable SubscribeWithConversion(this IObservable<EventEntry> source, IObserver<CloudEventEntry> sink)
        {
            return source.CreateSubscription(sink, TryConvertToCloudEventEntry);
        }

        /// <summary>
        /// Converts an <see cref="EventEntry"/> to a <see cref="CloudEventEntry"/>.
        /// </summary>
        /// <param name="entry">The entry to convert.</param>
        /// <returns>A converted entry, or <see langword="null"/> if the payload is invalid.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design",
            "CA1062:Validate arguments of public methods", MessageId = "0",
            Justification = "Validated using Guard class")]
        public static CloudEventEntry TryConvertToCloudEventEntry(this EventEntry entry)
        {
            Guard.ArgumentNotNull(entry, "entry");

            try
            {
                return new CloudEventEntry(entry);
            }
            catch (Exception e)
            {
                SemanticLoggingEventSource.Log.WindowsAzureTableSinkEntityCreationFailed(e.ToString());
                return null;
            }
        }
    }
}