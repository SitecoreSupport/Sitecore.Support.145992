
namespace Sitecore.Support.ContentSearch.Azure
{
    using System.Linq;
    using Diagnostics;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Azure.Query;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Linq.Common;
    using Sitecore.ContentSearch.Security;
    using Sitecore.ContentSearch.Utilities;

    public class CloudSearchSearchContext : Sitecore.ContentSearch.Azure.CloudSearchSearchContext, IProviderSearchContext
    {
        private readonly Sitecore.ContentSearch.Azure.CloudSearchProviderIndex index;

        public CloudSearchSearchContext(Sitecore.ContentSearch.Azure.CloudSearchProviderIndex index, SearchSecurityOptions options = SearchSecurityOptions.EnableSecurityCheck) : base(index, options)
        {
            Assert.ArgumentNotNull(index, "index");
            this.index = index;
        }

        IQueryable<TItem> IProviderSearchContext.GetQueryable<TItem>()
        {
            return this.BuildQueryable<TItem>(new IExecutionContext[0]);
        } 

        IQueryable<TItem> IProviderSearchContext.GetQueryable<TItem>(IExecutionContext executionContext)
        {
            IExecutionContext[] executionContexts = new IExecutionContext[] { executionContext };
            return this.BuildQueryable<TItem>(executionContexts);
        }

        IQueryable<TItem> IProviderSearchContext.GetQueryable<TItem>(params IExecutionContext[] executionContexts)
        {
            return this.BuildQueryable<TItem>(executionContexts);
        }

        protected virtual IQueryable<TItem> BuildQueryable<TItem>(params IExecutionContext[] executionContexts)
        {
            LinqToCloudIndex<TItem> linqIndex = new Sitecore.Support.ContentSearch.Azure.Query.LinqToCloudIndex<TItem>(this, executionContexts);
            if (this.index.Locator.GetInstance<IContentSearchConfigurationSettings>().EnableSearchDebug())
            {
                (linqIndex as IHasTraceWriter).TraceWriter = new LoggingTraceWriter(SearchLog.Log);
            }

            return linqIndex.GetQueryable();
        }

    }
}