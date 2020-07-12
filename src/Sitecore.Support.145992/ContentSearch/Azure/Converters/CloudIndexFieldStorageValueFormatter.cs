namespace Sitecore.Support.ContentSearch.Azure.Converters
{
  using System;
  using System.Collections.Generic;
  using System.ComponentModel;
  using System.Reflection;

  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Converters;
  using Sitecore.Diagnostics;

  public class CloudIndexFieldStorageValueFormatter : Sitecore.ContentSearch.Azure.Converters.CloudIndexFieldStorageValueFormatter
  {
    private static readonly MethodInfo ConvertToTypeMethodInfo =
      typeof(Sitecore.ContentSearch.Azure.Converters.CloudIndexFieldStorageValueFormatter).GetMethod("ConvertToType",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo SearchIndexFieldInfo =
      typeof(Sitecore.ContentSearch.Azure.Converters.CloudIndexFieldStorageValueFormatter).GetField("searchIndex",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private object ConvertToType(object value, Type expectedType, ITypeDescriptorContext context)
    {
      Assert.IsNotNull(ConvertToTypeMethodInfo, "ConvertToTypeMethodInfo");
      return ConvertToTypeMethodInfo.Invoke(this, new object[] {value, expectedType, context});
    }

    private Sitecore.ContentSearch.Azure.CloudSearchProviderIndex cloudSearchProviderIndex;

    private Sitecore.ContentSearch.Azure.CloudSearchProviderIndex SearchIndex =>
      cloudSearchProviderIndex ??
      (cloudSearchProviderIndex = (CloudSearchProviderIndex) SearchIndexFieldInfo.GetValue(this));

    public override object FormatValueForIndexStorage(object value, string fieldName)
    {
      Assert.IsNotNullOrEmpty(fieldName, nameof(fieldName));

      var result = value;

      if (result == null)
      {
        return null;
      }

      var fieldSchema = this.SearchIndex.SchemaBuilder.GetSchema().GetFieldByCloudName(fieldName);
      if (fieldSchema == null)
      {
        return value;
      }

      var cloudTypeMapper = this.SearchIndex.CloudConfiguration.CloudTypeMapper;
      var fieldType = cloudTypeMapper.GetNativeType(fieldSchema.Type);

      var context = new IndexFieldConverterContext(fieldName);

      try
      {
        if (result is IIndexableId)
        {
          result = this.FormatValueForIndexStorage(((IIndexableId)result).Value, fieldName);
        }
        else if (result is IIndexableUniqueId)
        {
          result = this.FormatValueForIndexStorage(((IIndexableUniqueId)result).Value, fieldName);
        }
        else
        {
          result = this.ConvertToType(result, fieldType, context);
        }

        if (result != null && !(result is string || fieldType.IsInstanceOfType(result) || (result is IEnumerable<string> && typeof(IEnumerable<string>).IsAssignableFrom(fieldType))))
        {
          throw new InvalidCastException($"Converted value has type '{result.GetType()}', but '{fieldType}' is expected.");
        }
      }
      catch (Exception ex)
      {
        throw new NotSupportedException($"Field '{fieldName}' with value '{value}' of type '{value.GetType()}' cannot be converted to type '{fieldType}' declared for the field in the schema.", ex);
      }

      return result;
    }
  }
}