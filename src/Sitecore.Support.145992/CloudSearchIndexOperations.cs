
namespace Sitecore.Support.ContentSearch.Azure
{
    using System.Collections.Concurrent;
    using Abstractions;
    using Reflection;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Azure.Config;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Pipelines.IndexingFilters;
    using Utils;

    public class CloudSearchIndexOperations: Sitecore.ContentSearch.Azure.CloudSearchIndexOperations
    {
        private readonly CloudSearchProviderIndex index;

        public CloudSearchIndexOperations(CloudSearchProviderIndex index) : base(index)
        {
            this.index = index;
        }

        protected override ConcurrentDictionary<string, object> GetDocument(IIndexable indexable, IProviderUpdateContext context)
        {
            var instance = context.Index.Locator.GetInstance<ICorePipeline>();
            
            if (InboundIndexFilterPipeline.Run(instance, new InboundIndexFilterArgs(indexable)))
            {
                this.index.Locator.GetInstance<IEvent>().RaiseEvent("indexing:excludedfromindex", new object[] { this.index.Name, indexable.UniqueId });
                return null;
            }

            object[] parameters = new object[] { indexable, context };
            var builder = (Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder)ReflectionUtil.CreateObject(context.Index.Configuration.DocumentBuilderType, parameters);
            if (builder == null)
            {
                CrawlingLog.Log.Error("Unable to create document builder (" + context.Index.Configuration.DocumentBuilderType + "). Please check your configuration. We will fallback to the default for now.", null);
                builder = new Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder(indexable, context);
            }

            builder.AddSpecialField(CloudSearchConfig.VirtualFields.CloudUniqueId, PublicCloudIndexParser.HashUniqueId(indexable.UniqueId.Value.ToString()));
            builder.AddSpecialField(BuiltinFields.ID, indexable.Id);
            builder.AddSpecialFields();

            builder.AddItemFields();
            builder.AddComputedIndexFields();
            return builder.Document;
        }
    }
}