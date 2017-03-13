
namespace Sitecore.Support.ContentSearch.Azure.Query
{
    using System.Drawing.Imaging;
    using Sitecore.ContentSearch.Azure.Query;
    using Sitecore.ContentSearch.Linq.Nodes;

    public class CloudQueryOptimizer : Sitecore.ContentSearch.Azure.Query.CloudQueryOptimizer
    {
        protected override QueryNode VisitConstant(ConstantNode node, CloudQueryOptimizerState state)
        {
            return base.VisitConstant(this.EncodeNodeValue(node), state);
        }

        // Workaround for issue 151246: encoding ampersands beforehand
        protected virtual ConstantNode EncodeNodeValue(ConstantNode node)
        {
            if (node.Type != typeof(System.String))
            {
                return node;
            }

            var value = (System.String)node.Value;

            if (!value.Contains("&"))
            {
                return node;
            }

            var encodedValue = value.Replace("&&", @"\&&").Replace("&", "%26");
            return new ConstantNode(encodedValue, typeof(System.String));
        }
    }
}