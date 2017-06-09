using Sitecore.ContentSearch.Azure.Query;
using Sitecore.ContentSearch.Linq.Nodes;

namespace Sitecore.Support.ContentSearch.Azure.Query
{
  public class CloudQueryMapper : Sitecore.ContentSearch.Azure.Query.CloudQueryMapper
  {
    public CloudQueryMapper(CloudIndexParameters parameters) : base(parameters)
    {      
    }

    protected override string HandleWhere(WhereNode node, CloudQueryMapperState mappingState)
    {
      string str = this.HandleCloudQuery(node.SourceNode, mappingState);
      string str2 = this.HandleCloudQuery(node.PredicateNode, mappingState);
      CloudQueryBuilder.ShouldWrap none = CloudQueryBuilder.ShouldWrap.None;
      if ((str != null) && str.ToUpper().Contains(" OR "))
      {
        none = CloudQueryBuilder.ShouldWrap.Left;
      }
      if ((str2 != null) && str2.ToUpper().Contains(" OR "))
      {
        none = CloudQueryBuilder.ShouldWrap.Right;
      }
      if (((str != null) && (str2 != null)) && (str.ToUpper().Contains(" OR ") && str2.ToUpper().Contains(" OR ")))
      {
        none = CloudQueryBuilder.ShouldWrap.Both;
      }
      return CloudQueryBuilder.Merge(str, str2, "and", none);
    }
  }
}