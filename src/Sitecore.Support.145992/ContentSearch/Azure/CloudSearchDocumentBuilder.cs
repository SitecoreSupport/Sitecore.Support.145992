using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Azure;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;

namespace Sitecore.Support.ContentSearch.Azure
{
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

        public override void AddItemFields()
        {
            try
            {
                VerboseLogging.CrawlingLogDebug(() => "AddItemFields start");

                var fields = this.Options.IndexAllFields ?
                    this.GetFieldsByItemList(this.Indexable) :
                    this.GetFieldsByIncludedList(this.Indexable);

                if (this.IsParallel)
                    this.ProcessFieldsInParallel(fields);
                else
                    this.ProcessFieldsInSequence(fields);
            }
            finally
            {
                VerboseLogging.CrawlingLogDebug(() => "AddItemFields End");
            }
        }

        protected virtual IEnumerable<IIndexableDataField> GetFieldsByItemList([NotNull] IIndexable indexable)
        {
            indexable.LoadAllFields();

            return indexable.Fields ?? Enumerable.Empty<IIndexableDataField>();
        }

        protected virtual IEnumerable<IIndexableDataField> GetFieldsByIncludedList([NotNull] IIndexable indexable)
        {
            var includedFields = this.Options.IncludedFields;

            if (includedFields == null)
            {
                return Enumerable.Empty<IIndexableDataField>();
            }

            return this.Options.IncludedFields
                .Select(key => this.GetFieldByKeyFromIndexable(indexable, key))
                .Where(field => field != null);
        }

        protected virtual IIndexableDataField GetFieldByKeyFromIndexable(IIndexable indexable, string fieldKey)
        {
            return ID.TryParse(fieldKey, out var id) ?
                GetFieldById(indexable, id) :
                indexable.GetFieldByName(fieldKey);
        }

        private static IIndexableDataField GetFieldById(IIndexable indexable, object fieldId)
        {
            if (indexable is SitecoreIndexableItem sitecoreIndexableItem)
            {
            }
            else
            {
                return null;
            }

            var id = fieldId as ID;

            if (id == (ID)null)
            {
                return null;
            }

            if (!ItemHasField(sitecoreIndexableItem.Item, id))
            {
                return null;
            }

            return (SitecoreItemDataField)sitecoreIndexableItem.Item.Fields[id];
        }

        private static bool ItemHasField(Item item, ID fieldId) => TemplateManager.IsFieldPartOfTemplate(fieldId, item);

        protected virtual void ProcessFieldsInParallel([NotNull] IEnumerable<IIndexableDataField> fields)
        {
            var exceptions = new ConcurrentQueue<Exception>();

            this.ParallelForeachProxy.ForEach(fields, this.ParallelOptions, f =>
            {
                try
                {
                    this.CheckAndAddField(this.Indexable, f);
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                }
            });

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        }

        protected virtual void ProcessFieldsInSequence([NotNull] IEnumerable<IIndexableDataField> fields)
        {
            foreach (var field in fields)
            {
                this.CheckAndAddField(this.Indexable, field);
            }
        }


        private static readonly MethodInfo checkAndAddFieldMethodInfo =
            typeof(Sitecore.ContentSearch.Azure.CloudSearchDocumentBuilder).BaseType?.GetMethod("CheckAndAddField",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private void CheckAndAddField(IIndexable indexable, IIndexableDataField field)
        {
            if (checkAndAddFieldMethodInfo == null)
            {
                Log.SingleError("Sitecore.Support.145992: checkAndAddFieldMethodInfo is not initialized", this);
                return;
            }

            checkAndAddFieldMethodInfo.Invoke(this, new object[] {indexable, field});
        }
    }
}