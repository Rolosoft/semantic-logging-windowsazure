using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rolosoft.Practices.EnterpriseLibrary.SemanticLogging.WindowsAzure.Tests.TestSupport
{
    [TestClass]
    public abstract class ContextBase
    {
        [TestInitialize]
        public void Initialize()
        {
            this.Given();
            this.When();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.OnCleanup();
        }

        protected virtual void Given()
        {
        }

        protected virtual void When()
        {
        }

        protected virtual void OnCleanup()
        {
        }
    }
}