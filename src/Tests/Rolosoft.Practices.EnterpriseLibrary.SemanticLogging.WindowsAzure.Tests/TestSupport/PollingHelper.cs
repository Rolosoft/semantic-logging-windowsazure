using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.TestSupport
{
    internal class PollingHelper
    {
        public static T WaitUntil<T>(Func<T> action, Func<T, bool> condition, TimeSpan timeout)
        {
            var waitTime = (int)Math.Max(timeout.TotalMilliseconds / 20.5d, 150d);
            var deadline = DateTime.UtcNow.Add(timeout);
            while (true)
            {
                T result = action();
                if (condition(result))
                {
                    return result;
                }

                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail("Timeout time exceeded and condition was not met. Current result: {0}", result);
                }

                Thread.Sleep(waitTime);
            }
        }
    }
}