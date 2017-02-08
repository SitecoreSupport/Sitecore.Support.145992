
namespace Sitecore.Support.ContentSearch.Azure
{
    using System.Reflection;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Azure.Http;
    using Sitecore.ContentSearch.Azure.Schema;
    using Sitecore.ContentSearch.Maintenance;

    public class CloudSearchProviderIndex : Sitecore.ContentSearch.Azure.CloudSearchProviderIndex
    {
        public CloudSearchProviderIndex(string name, string connectionStringName, string totalParallelServices,
            IIndexPropertyStore propertyStore) : base(name, connectionStringName, totalParallelServices, propertyStore)
        {
        }

        public CloudSearchProviderIndex(string name, string connectionStringName, string totalParallelServices,
            IIndexPropertyStore propertyStore, string @group)
            : base(name, connectionStringName, totalParallelServices, propertyStore, @group)
        {
        }

        public override IIndexOperations Operations
        {
            get { return new Sitecore.Support.ContentSearch.Azure.CloudSearchIndexOperations(this); }
        }

        #region Workaround for issue 136614

        public new ICloudSearchIndexSchemaBuilder SchemaBuilder
        {
            get { return (this as Sitecore.ContentSearch.Azure.CloudSearchProviderIndex).SchemaBuilder; }
            set
            {
                var pi = typeof(Sitecore.ContentSearch.Azure.CloudSearchProviderIndex)
                    .GetProperty("SchemaBuilder", BindingFlags.Instance | BindingFlags.Public);
                pi.SetValue(this, value);
            }
        }


        public new ISearchService SearchService
        {
            get { return (this as Sitecore.ContentSearch.Azure.CloudSearchProviderIndex).SearchService; }
            set
            {
                var pi = typeof(Sitecore.ContentSearch.Azure.CloudSearchProviderIndex)
                    .GetProperty("SearchService", BindingFlags.Instance | BindingFlags.Public);
                pi.SetValue(this, value);
            }
        }

        #endregion
    }
}