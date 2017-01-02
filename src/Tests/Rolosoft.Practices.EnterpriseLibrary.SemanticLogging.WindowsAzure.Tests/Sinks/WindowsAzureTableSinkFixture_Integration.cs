﻿using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Sinks;
using Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.TestObjects;
using Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.TestSupport;
using Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.UsingEventListener;

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.Sinks
{
    public class given_empty_account : ArrangeActAssert
    {
        protected string connectionString;
        private CloudStorageAccount account;
        protected CloudTableClient client;
        protected string tableName;
        protected WindowsAzureTableSink sink;

        protected virtual string InstanceName
        {
            get { return "TestInstanceName"; }
        }

        protected override void Arrange()
        {
            this.connectionString = ConfigurationHelper.GetSetting("StorageConnectionString");

            if (string.IsNullOrEmpty(connectionString)
                || connectionString.Contains("[AccountName]")
                || connectionString.Contains("[AccountKey]"))
            {
                Assert.Inconclusive("Cannot run tests because the Azure Storage credentials are not configured");
            }

            this.account = CloudStorageAccount.Parse(connectionString);
            this.client = this.account.CreateCloudTableClient();
            this.tableName = "AzureTableEventListenerTests" +
                             new Random(unchecked((int)DateTime.Now.Ticks)).Next(10000).ToString();

            this.sink = new WindowsAzureTableSink(InstanceName, connectionString, tableName, TimeSpan.FromSeconds(1),
                5000, Timeout.InfiniteTimeSpan);
        }

        protected override void Teardown()
        {
            base.Teardown();
            this.sink.Dispose();

            if (this.tableName != null)
            {
                this.account.CreateCloudTableClient().GetTableReference(tableName).DeleteIfExists();
            }
        }
    }

    public class time_consuming_test : given_empty_account
    {
        protected override void Arrange()
        {
            var runSlowTests = ConfigurationHelper.GetSetting("RunSlowIntegrationTests");

            if (string.IsNullOrEmpty(runSlowTests) || runSlowTests.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                runSlowTests.Equals("0"))
            {
                Assert.Inconclusive(
                    "Skipping integration tests that are time-consuming because the RunSlowIntegrationTests setting is not set to true in the app.config file.");
            }

            base.Arrange();
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif

    [TestClass]
    public class when_writing_to_in_ascending_order : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            sink.SortKeysAscending = true;

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 1,
                    payloadNames: new string[] { "arg1" },
                    payload: new object[] { "value arg1" })));

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 2,
                    payloadNames: new string[] { "arg2" },
                    payload: new object[] { "value arg2" })));

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 3,
                    payloadNames: new string[] { "arg3" },
                    payload: new object[] { "value arg3" })));
        }

        [TestMethod]
        public void then_orders_them_in_ascending_order()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(3, list.Count());
            Assert.AreEqual<int>(1, list.ElementAt(0).EventId);
            Assert.AreEqual<int>(2, list.ElementAt(1).EventId);
            Assert.AreEqual<int>(3, list.ElementAt(2).EventId);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_single_entry : given_empty_account
    {
        private CloudEventEntry entry;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:CurlyBracketsForMultiLineStatementsMustNotShareLine", Justification = "inline array ok")]
        protected override void Act()
        {
            base.Act();
            this.entry = new CloudEventEntry(EventEntryTestHelper.Create(
                eventId: 12,
                providerId: Guid.NewGuid(),
                providerName: "Provider Name",
                timestamp: new DateTimeOffset(2013, 4, 10, 16, 0, 0, TimeSpan.Zero),
                keywords: (EventKeywords)16L,
                level: EventLevel.Informational,
                formattedMessage: "My message",
                opcode: (EventOpcode)4,
                task: (EventTask)24,
                version: 2,
                payloadNames: new string[] { "arg1" },
                payload: new object[] { "value arg1" },
                processId: 200,
                threadId: 300,
                activityId: Guid.Parse("{562D0422-F427-4849-A6CD-7990A46F1223}"),
                relatedActivityId: Guid.Parse("{23408E19-3133-47E1-9307-C99A4F9AC8CC}")))
            {
                InstanceName = "Instance Name"
            };
            sink.OnNext(this.entry);
        }

        [TestMethod]
        public void then_all_properties_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var actual = table.ExecuteQuery(query).Single();

            Assert.AreEqual(entry.EventId, actual.EventId);
            Assert.AreEqual(entry.ProviderId, actual.ProviderId);
            Assert.AreEqual(entry.ProviderName, actual.ProviderName);
            Assert.AreEqual(entry.EventDate, actual.EventDate);
            Assert.AreEqual(entry.Keywords, actual.Keywords);
            Assert.AreEqual(entry.InstanceName, actual.InstanceName);
            Assert.AreEqual(entry.Level, actual.Level);
            Assert.AreEqual(entry.Message, actual.Message);
            Assert.AreEqual(entry.Opcode, actual.Opcode);
            Assert.AreEqual(entry.Task, actual.Task);
            Assert.AreEqual(entry.Version, actual.Version);
            StringAssert.Contains(actual.Payload, "arg1");
            StringAssert.Contains(actual.Payload, (string)entry.Payload["arg1"]);
            Assert.AreEqual("value arg1", actual.RawPayloadProperties["Payload_arg1"].StringValue);
            Assert.AreEqual(entry.ProcessId, actual.ProcessId);
            Assert.AreEqual(entry.ThreadId, actual.ThreadId);
            Assert.AreEqual(entry.ActivityId, actual.ActivityId.Value);
            Assert.AreEqual(entry.RelatedActivityId, actual.RelatedActivityId.Value);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_single_entry_without_activity_ids : given_empty_account
    {
        private CloudEventEntry entry;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:CurlyBracketsForMultiLineStatementsMustNotShareLine", Justification = "inline array ok")]
        protected override void Act()
        {
            base.Act();
            this.entry =
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 12,
                    providerId: Guid.NewGuid(),
                    providerName: "Provider Name",
                    timestamp: new DateTimeOffset(2013, 4, 10, 16, 0, 0, TimeSpan.Zero),
                    keywords: (EventKeywords)16,
                    level: EventLevel.Informational,
                    formattedMessage: "My message",
                    opcode: (EventOpcode)4,
                    task: (EventTask)24,
                    version: 2,
                    payloadNames: new string[] { "arg1" },
                    payload: new object[] { "value arg1" }))
                {
                    InstanceName = "Instance Name",
                };
            sink.OnNext(this.entry);
        }

        [TestMethod]
        public void then_all_properties_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var actual = table.ExecuteQuery(query).Single();

            Assert.AreEqual(entry.EventId, actual.EventId);
            Assert.AreEqual(entry.ProviderId, actual.ProviderId);
            Assert.AreEqual(entry.ProviderName, actual.ProviderName);
            Assert.AreEqual(entry.EventDate, actual.EventDate);
            Assert.AreEqual(entry.Keywords, actual.Keywords);
            Assert.AreEqual(entry.InstanceName, actual.InstanceName);
            Assert.AreEqual(entry.Level, actual.Level);
            Assert.AreEqual(entry.Message, actual.Message);
            Assert.AreEqual(entry.Opcode, actual.Opcode);
            Assert.AreEqual(entry.Task, actual.Task);
            Assert.AreEqual(entry.Version, actual.Version);
            StringAssert.Contains(actual.Payload, "arg1");
            StringAssert.Contains(actual.Payload, (string)entry.Payload["arg1"]);
            Assert.AreEqual("value arg1", actual.RawPayloadProperties["Payload_arg1"].StringValue);
            Assert.AreEqual(null, actual.ActivityId);
            Assert.AreEqual(null, actual.RelatedActivityId);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_single_entry_with_activity_id : given_empty_account
    {
        private CloudEventEntry entry;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:CurlyBracketsForMultiLineStatementsMustNotShareLine", Justification = "inline array ok")]
        protected override void Act()
        {
            base.Act();
            this.entry = new CloudEventEntry(EventEntryTestHelper.Create(
                eventId: 12,
                providerId: Guid.NewGuid(),
                providerName: "Provider Name",
                timestamp: new DateTimeOffset(2013, 4, 10, 16, 0, 0, TimeSpan.Zero),
                keywords: (EventKeywords)16,
                level: EventLevel.Informational,
                formattedMessage: "My message",
                opcode: (EventOpcode)4,
                task: (EventTask)24,
                version: 2,
                payloadNames: new string[] { "arg1" },
                payload: new object[] { "value arg1" },
                activityId: Guid.Parse("{562D0422-F427-4849-A6CD-7990A46F1223}")))
            {
                InstanceName = "Instance Name",
            };
            sink.OnNext(this.entry);
        }

        [TestMethod]
        public void then_all_properties_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var actual = table.ExecuteQuery(query).Single();

            Assert.AreEqual(entry.EventId, actual.EventId);
            Assert.AreEqual(entry.ProviderId, actual.ProviderId);
            Assert.AreEqual(entry.ProviderName, actual.ProviderName);
            Assert.AreEqual(entry.EventDate, actual.EventDate);
            Assert.AreEqual(entry.Keywords, actual.Keywords);
            Assert.AreEqual(entry.InstanceName, actual.InstanceName);
            Assert.AreEqual(entry.Level, actual.Level);
            Assert.AreEqual(entry.Message, actual.Message);
            Assert.AreEqual(entry.Opcode, actual.Opcode);
            Assert.AreEqual(entry.Task, actual.Task);
            Assert.AreEqual(entry.Version, actual.Version);
            StringAssert.Contains(actual.Payload, "arg1");
            StringAssert.Contains(actual.Payload, (string)entry.Payload["arg1"]);
            Assert.AreEqual("value arg1", actual.RawPayloadProperties["Payload_arg1"].StringValue);
            Assert.AreEqual(entry.ActivityId, actual.ActivityId.Value);
            Assert.AreEqual(null, actual.RelatedActivityId);
        }
    }

    [TestClass]
    [Ignore]
    public class when_deleting_table_while_its_running : time_consuming_test
    {
        protected override void Act()
        {
            base.Act();
            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 1, payloadNames: new[] { "arg1" },
                payload: new object[] { "value arg1" })));
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));
        }

        [TestMethod]
        public void then_sink_recreates_table_on_new_entry()
        {
            var table = client.GetTableReference(tableName);

            table.Delete();
            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 4, payloadNames: new[] { "arg4" },
                payload: new object[] { "value arg4" })));

            // wait for long, as deleting a table and then recreating it can take very long
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromMinutes(3)));

            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(1, list.Count());
            Assert.AreEqual<int>(4, list.ElementAt(0).EventId);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_to_storage : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 1,
                    payloadNames: new string[] { "arg1" },
                    payload: new object[] { "value arg1" })));

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 2,
                    payloadNames: new string[] { "arg2" },
                    payload: new object[] { "value arg2" })));

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 3,
                    payloadNames: new string[] { "arg3" },
                    payload: new object[] { "value arg3" })));

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 4)));
        }

        [TestMethod]
        public void writes_to_azure_tables()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            table.CreateIfNotExists();

            var list = PollingHelper.WaitUntil(() => table.ExecuteQuery(query).ToArray(), l => l.Length >= 4,
                TimeSpan.FromSeconds(20));

            Assert.AreEqual<int>(4, list.Count());
            Assert.IsTrue(list.Any(x => x.EventId == 1));
            Assert.IsTrue(list.Any(x => x.EventId == 2));
            Assert.IsTrue(list.Any(x => x.EventId == 3));
            Assert.IsTrue(list.Any(x => x.EventId == 4));
        }

        [TestMethod]
        public void then_can_force_flush_messages()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(4, list.Count());
            Assert.IsTrue(list.Any(x => x.EventId == 1));
            Assert.IsTrue(list.Any(x => x.EventId == 2));
            Assert.IsTrue(list.Any(x => x.EventId == 3));
            Assert.IsTrue(list.Any(x => x.EventId == 4));
        }

        [TestMethod]
        public void then_orders_them_from_newer_to_older()
        {
            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();

            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var list = table.ExecuteQuery<TestCloudTableEntry>(query).ToArray();

            Assert.AreEqual<int>(4, list.Count());
            Assert.AreEqual<int>(4, list.ElementAt(0).EventId);
            Assert.AreEqual<int>(3, list.ElementAt(1).EventId);
            Assert.AreEqual<int>(2, list.ElementAt(2).EventId);
            Assert.AreEqual<int>(1, list.ElementAt(3).EventId);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_multiple_simultaneous_entries_with_ascending_key : given_empty_account
    {
        protected const int NumberOfEntries = 5;
        protected DateTimeOffset eventDate = DateTimeOffset.UtcNow;

        protected override void Act()
        {
            base.Act();

            sink.SortKeysAscending = true;

            for (int i = 0; i < 5; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 10,
                        timestamp: this.eventDate,
                        payloadNames: new string[] { "arg" },
                        payload: new object[] { i })));
            }
        }

        [TestMethod]
        public void then_entries_are_idempotent_but_unique_and_ordered()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(NumberOfEntries, list.Length);
            for (int i = 0; i < NumberOfEntries; i++)
            {
                Assert.AreEqual<int>(i, Convert.ToInt32(list[i].DeserializedPayload["arg"]));
            }
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_multiple_simultaneous_entries_with_descending_key : given_empty_account
    {
        protected const int NumberOfEntries = 5;
        protected DateTimeOffset eventDate = DateTimeOffset.UtcNow;

        protected override void Act()
        {
            base.Act();

            sink.SortKeysAscending = false;

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 10,
                        timestamp: this.eventDate,
                        payloadNames: new string[] { "arg" },
                        payload: new object[] { i })));
            }
        }

        [TestMethod]
        public void then_entries_are_idempotent_but_unique_and_ordered()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(NumberOfEntries, list.Length);
            for (int i = 0; i < NumberOfEntries; i++)
            {
                Assert.AreEqual<int>(NumberOfEntries - i - 1, Convert.ToInt32(list[i].DeserializedPayload["arg"]));
            }
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_200_entries_with_descending_key : time_consuming_test
    {
        protected const int NumberOfEntries = 200;

        protected override void Act()
        {
            base.Act();

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 10,
                        payloadNames: new string[] { "arg" },
                        payload: new object[] { i })));
            }
        }

        [TestMethod]
        public void then_all_entries_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(NumberOfEntries, list.Length);

            for (int i = 0; i < NumberOfEntries; i++)
            {
                Assert.AreEqual<int>(NumberOfEntries - i - 1, Convert.ToInt32(list[i].DeserializedPayload["arg"]));
            }
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_several_entries_with_descending_key : time_consuming_test
    {
        protected const int NumberOfEntries = 550;

        protected override void Act()
        {
            base.Act();

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 10,
                        payloadNames: new string[] { "arg" },
                        payload: new object[] { i })));
            }
        }

        [TestMethod]
        public void then_all_entries_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(NumberOfEntries, list.Length);

            for (int i = 0; i < NumberOfEntries; i++)
            {
                Assert.AreEqual<int>(NumberOfEntries - i - 1, Convert.ToInt32(list[i].DeserializedPayload["arg"]));
            }
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_200_entries_with_ascending_key : time_consuming_test
    {
        protected const int NumberOfEntries = 200;

        protected override void Act()
        {
            base.Act();

            sink.SortKeysAscending = true;

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 10,
                        payloadNames: new string[] { "arg" },
                        payload: new object[] { i })));
            }
        }

        [TestMethod]
        public void then_all_entries_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(NumberOfEntries, list.Length);
            for (int i = 0; i < NumberOfEntries; i++)
            {
                Assert.AreEqual<int>(i, Convert.ToInt32(list[i].DeserializedPayload["arg"]));
            }
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_several_entries_with_ascending_key : time_consuming_test
    {
        protected const int NumberOfEntries = 550;

        protected override void Act()
        {
            base.Act();

            sink.SortKeysAscending = true;

            for (int i = 0; i < NumberOfEntries; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 10,
                        payloadNames: new string[] { "arg" },
                        payload: new object[] { i })));
            }
        }

        [TestMethod]
        public void then_all_entries_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<int>(NumberOfEntries, list.Length);
            for (int i = 0; i < NumberOfEntries; i++)
            {
                Assert.AreEqual<int>(i, Convert.ToInt32(list[i].DeserializedPayload["arg"]));
            }
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_with_version_opcode_level : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 10,
                    opcode: EventOpcode.Reply,
                    level: EventLevel.Informational,
                    version: 2,
                    payloadNames: new string[] { "arg1", "arg2", "arg3" },
                    payload: new object[] { 1, "2", true })));
        }

        [TestMethod]
        public void then_all_entries_with_version_opcode_level_are_written()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            var entry = list.Single();
            Assert.AreEqual<int>(2, entry.Version);
            Assert.AreEqual<int>((int)EventOpcode.Reply, entry.Opcode);
            Assert.AreEqual<int>((int)EventLevel.Informational, entry.Level);
        }
    }
#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_writing_with_enum_payload : given_empty_account
    {
        protected override void Act()
        {
            base.Act();

            sink.OnNext(
                new CloudEventEntry(EventEntryTestHelper.Create(
                    eventId: 10,
                    payloadNames: new[] { "arg1", "arg2", "arg3" },
                    payload: new object[] { MyLongEnum.Value1, MyIntEnum.Value2, MyShortEnum.Value3 })));
        }

        [TestMethod]
        public void then_writes_integral_value()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            var entry = list.Single();

            StringAssert.Contains(entry.Payload, "\"arg1\": 0");
            StringAssert.Contains(entry.Payload, "\"arg2\": 1");
            StringAssert.Contains(entry.Payload, "\"arg3\": 2");

            Assert.AreEqual<long>(0, entry.RawPayloadProperties["Payload_arg1"].Int64Value.Value);
            Assert.AreEqual<int>(1, entry.RawPayloadProperties["Payload_arg2"].Int32Value.Value);
            Assert.AreEqual<int>(2, entry.RawPayloadProperties["Payload_arg3"].Int32Value.Value);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_having_a_non_standard_instance_name_entries : given_empty_account
    {
        protected override string InstanceName
        {
            get { return @"1#2@3/4\" + new string('a', 1000); }
        }

        protected override void Act()
        {
            base.Act();

            // check not running on the emulator
            if (
                string.Compare(this.connectionString, "UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase) ==
                0)
            {
                Assert.Inconclusive("This test cannot be run with the storage emulator");
            }

            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10)));
            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 20)) { InstanceName = "Custom@#Name" });
        }

        [TestMethod]
        public void then_instance_name_is_normalized()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual<string>("1_2_3_4_" + new string('a', 255 - "1_2_3_4_".Length),
                list.Single(x => x.EventId == 10).InstanceName);
            Assert.AreEqual<string>("Custom__Name", list.Single(x => x.EventId == 20).InstanceName);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_sending_entries_with_big_payloads : time_consuming_test
    {
        protected override void Act()
        {
            base.Act();

            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 10,
                payloadNames: Enumerable.Range(0, 500).Select(x => "arg" + x),
                payload: Enumerable.Range(0, 500).Select(x => (object)x))));
            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 20, payloadNames: new string[] { "Large" },
                payload: new object[] { new string('a', 500000) })));
            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 30, formattedMessage: new string('b', 500000))));
            sink.OnNext(new CloudEventEntry(EventEntryTestHelper.Create(eventId: 40,
                payloadNames: Enumerable.Range(0, 50).Select(x => "arg" + x),
                payload: Enumerable.Range(0, 50).Select(x => (object)new string('c', 1000)))));
        }

        [TestMethod]
        public void then_entries_are_truncated()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromSeconds(45)));
            sink.Dispose();

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            var entry1 = list.Single(x => x.EventId == 10);
            for (int i = 0; i < 500; i++)
            {
                StringAssert.Contains(entry1.Payload, string.Format("\"arg{0}\": {0}", i));
            }
            Assert.IsTrue(entry1.RawPayloadProperties.Count >= 200);

            var entry2 = list.Single(x => x.EventId == 20);
            Assert.AreEqual(1, entry2.DeserializedPayload.Count);
            Assert.IsTrue(entry2.DeserializedPayload.ContainsKey("payload_serialization_error"));
            Assert.AreEqual(0, entry2.RawPayloadProperties.Count);

            var entry3 = list.Single(x => x.EventId == 30);
            Assert.AreEqual(new string('b', 30000) + "--TRUNCATED--", entry3.Message);

            var entry4 = list.Single(x => x.EventId == 20);
            Assert.AreEqual(1, entry4.DeserializedPayload.Count);
            Assert.IsTrue(entry4.DeserializedPayload.ContainsKey("payload_serialization_error"));
            Assert.AreEqual(0, entry4.RawPayloadProperties.Count);
        }
    }

#if !DEBUG
    [Ignore] // Ignore for release build.
#endif
    [TestClass]
    public class when_sending_large_entries_that_exceed_batch_size : time_consuming_test
    {
        protected override void Act()
        {
            base.Act();

            for (int i = 0; i < 100; i++)
            {
                sink.OnNext(
                    new CloudEventEntry(EventEntryTestHelper.Create(
                        eventId: 30,
                        formattedMessage: new string('b', 30000),
                        payloadNames: new string[] { "Medium" },
                        payload: new object[] { new string('a', 20000) })));
            }
        }

        [TestMethod]
        public void then_entries_are_sent_in_smaller_batches()
        {
            Assert.IsTrue(this.sink.FlushAsync().Wait(TimeSpan.FromMinutes(2)));
            sink.Dispose();

            var table = client.GetTableReference(tableName);
            var query = new TableQuery<TestCloudTableEntry>();
            var list = table.ExecuteQuery(query).ToArray();

            Assert.AreEqual(100, list.Length);

            Assert.AreEqual(30, list[0].EventId);
            Assert.AreEqual(new string('b', 30000), list[0].Message);
            Assert.AreEqual(new string('a', 20000), list[0].DeserializedPayload["Medium"]);
            Assert.AreEqual(new string('a', 20000), list[0].RawPayloadProperties["Payload_Medium"].StringValue);
        }
    }
}