
namespace Sitecore.Support.ContentSearch.Azure
{
    using Sitecore.ContentSearch;

    public class CloudSearchDocumentBuilder : Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder
    {
        public CloudSearchDocumentBuilder(IIndexable indexable, IProviderUpdateContext context) : base(indexable, context)
        {
        }

        public override void AddField(IIndexableDataField field)
        {
            var value = this.Index.Configuration.FieldReaders.GetFieldValue(field);

            this.AddField(field.Name, value);
        }

    }
}