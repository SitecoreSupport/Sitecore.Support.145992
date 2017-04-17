namespace Sitecore.Support.ContentSearch.Azure
{
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Azure;
    using System;
    using Sitecore.ContentSearch.Diagnostics;
    using System.Collections.Concurrent;
    public class CloudSearchDocumentBuilder : Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder
    {
        public CloudSearchDocumentBuilder(IIndexable indexable, IProviderUpdateContext context) : base(indexable, context)
        {
        }

        public override void AddField(IIndexableDataField field)
        {
            var value = this.Index.Configuration.FieldReaders.GetFieldValue(field);

            if (!base.IsMedia && IndexOperationsHelper.IsTextField(field))
            {
                this.AddField(BuiltinFields.Content, value, true);
            }
            this.AddField(field.Name, value, false);
        }

        public override void AddField(string fieldName, object fieldValue, bool append = false)
        {
            var cloudName = this.Index.FieldNameTranslator.GetIndexFieldName(fieldName);
            if (!append && this.Document.ContainsKey(cloudName))
            {
                return;
            }
            var configuration = this.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName) as CloudSearchFieldConfiguration;
            if (configuration != null)
            {
                if (configuration.Ignore)
                {
                    return;
                }

                fieldValue = configuration.FormatForWriting(fieldValue);

                if (fieldValue == null)
                {
                    VerboseLogging.CrawlingLogDebug(() => string.Format("Skipping field name:{0} - Value is empty.", fieldName));
                    return;
                }
            }

            var searchIndex = this.Index as CloudSearchProviderIndex;
            if (searchIndex != null)
            {
                searchIndex.SchemaBuilder.AddField(cloudName, fieldValue);
            }

            var formattedValue = this.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(fieldValue, cloudName);

            if (append && base.Document.ContainsKey(cloudName) && formattedValue is string)
            {
                ConcurrentDictionary<string, object> document = base.Document;
                document[cloudName] = document[cloudName] + " " + (string)formattedValue;
                return;
            }

            if (!base.Document.ContainsKey(cloudName) && formattedValue != null)
            {
                this.Document.GetOrAdd(cloudName, formattedValue);
            }
        }

        public override void AddComputedIndexFields()
        {
            foreach (var computedIndexField in this.Options.ComputedIndexFields)
            {
                object fieldValue;

                try
                {
                    fieldValue = computedIndexField.ComputeFieldValue(this.Indexable);
                }
                catch (Exception ex)
                {
                    CrawlingLog.Log.Error(string.Format("Could not compute value for ComputedIndexField: {0} for indexable: {1}", computedIndexField.FieldName, this.Indexable.UniqueId), ex);
                    continue;
                }

                this.AddField(computedIndexField.FieldName, fieldValue, true);
            }
        }
    }
}