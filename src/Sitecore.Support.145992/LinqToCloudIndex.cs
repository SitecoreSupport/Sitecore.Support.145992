
namespace Sitecore.Support.ContentSearch.Azure.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Configuration;
    using Diagnostics;
    using Newtonsoft.Json.Linq;
    using Publishing.Explanations;
    using Sitecore.ContentSearch.Azure;
    using Sitecore.ContentSearch.Azure.Query;
    using Sitecore.ContentSearch.Azure.Utils;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Linq;
    using Sitecore.ContentSearch.Linq.Common;
    using Sitecore.ContentSearch.Linq.Methods;
    using Sitecore.ContentSearch.Linq.Nodes;

    public class LinqToCloudIndex<TItem> : Sitecore.ContentSearch.Azure.Query.LinqToCloudIndex<TItem>
    {
        private readonly CloudSearchSearchContext context;

        private static MethodInfo miGetOrderByExpression;
        private static MethodInfo miGetFacetExpression;
        private static MethodInfo miApplyScalarMethods;
        private static FieldInfo fiQueryOptimizer;

        static LinqToCloudIndex()
        {
            var t = typeof(Sitecore.ContentSearch.Azure.Query.LinqToCloudIndex<TItem>);

            miGetOrderByExpression = t.GetMethod("GetOrderByExpression", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(miGetOrderByExpression, "miGetOrderByExpression is null...");
            
            miGetFacetExpression = t.GetMethod("GetFacetExpression", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(miGetFacetExpression, "miGetFacetExpression is null...");

            miApplyScalarMethods = t.GetMethod("ApplyScalarMethods", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(miApplyScalarMethods, "miApplyScalarMethods is null...");

            var tCloudIndex = typeof(Sitecore.ContentSearch.Azure.Query.CloudIndex<TItem>);
            fiQueryOptimizer = tCloudIndex.GetField("queryOptimizer", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public LinqToCloudIndex(CloudSearchSearchContext context, IExecutionContext executionContext) : base(context, executionContext)
        {
            this.context = context;
            fiQueryOptimizer.SetValue(this, new Sitecore.Support.ContentSearch.Azure.Query.CloudQueryOptimizer());
        }

        public LinqToCloudIndex(CloudSearchSearchContext context, IExecutionContext[] executionContexts) : base(context, executionContexts)
        {
            this.context = context;
            fiQueryOptimizer.SetValue(this, new Sitecore.Support.ContentSearch.Azure.Query.CloudQueryOptimizer());
        }

        public override TResult Execute<TResult>(CloudQuery query)
        {
            var selectMethod = this.GetSelectMethod(query);
            int totalDoc, countDoc;
            Dictionary<string, object> facetResult;

            var results = this.Execute(query, out countDoc, out totalDoc, out facetResult);

            Type documentType = null;
            object processedResults;

            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(SearchResults<>))
            {
                documentType = typeof(TResult).GetGenericArguments()[0];

                // Construct CloudSearchResults<> instance
                var cloudSearchResultsGenericType = typeof(CloudSearchResults<>).MakeGenericType(documentType);
                processedResults = Activator.CreateInstance(cloudSearchResultsGenericType, this.context, results, selectMethod, countDoc, facetResult, this.Parameters, query.VirtualFieldProcessors);
            }
            else
            {
                documentType = typeof(TItem);
                processedResults = new CloudSearchResults<TItem>(this.context, results, selectMethod, countDoc, facetResult, this.Parameters, query.VirtualFieldProcessors);
            }

            // Since the ApplyScalarMethods is private, in any case reflection has to be used 
            // return this.ApplyScalarMethods<TResult, TItem>(query, processedResults, totalDoc);
            var applyScalarMethodsGenericMethod = miApplyScalarMethods.MakeGenericMethod(typeof(TResult), documentType);

            TResult result;

            try
            {
                result = (TResult) applyScalarMethodsGenericMethod.Invoke(this, new[] {query, processedResults, totalDoc});
            }
            catch (TargetInvocationException ex)
            {
                // Required for First or Single methdos otherwise TargetInvocationException is thrown instead of InvalidOperationException
                throw ex.InnerException;
            }

            return result;
        }


        public override IEnumerable<TElement> FindElements<TElement>(CloudQuery query)
        {
            if (EnumerableLinq.ShouldExecuteEnumerableLinqQuery(query))
            {
                return EnumerableLinq.ExecuteEnumerableLinqQuery<IEnumerable<TElement>>(query);
            }

            int totalDoc, countDoc;
            Dictionary<string, object> facetResult;
            var valueList = this.Execute(query, out countDoc, out totalDoc, out facetResult);
            var selectMethod = this.GetSelectMethod(query);
            var processedResults = new CloudSearchResults<TElement>(this.context, valueList, selectMethod, countDoc, facetResult, this.Parameters, query.VirtualFieldProcessors);
            return processedResults.GetSearchResults();
        }

        protected virtual List<Dictionary<string, object>> Execute(CloudQuery query, out int countDoc, out int totalDoc, out Dictionary<string, object> facetResult)
        {
            countDoc = 0;
            totalDoc = 0;
            facetResult = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(query.Expression) && query.Methods.Count == 0)
            {
                return new List<Dictionary<string, object>>();
            }

            var searchIndex = this.context.Index as CloudSearchProviderIndex;
            if (searchIndex == null)
            {
                return new List<Dictionary<string, object>>();
            }

            if (query.Expression.Contains(CloudQueryBuilder.Search.SearchForNothing))
            {
                return new List<Dictionary<string, object>>();
            }

            var expression = this.OptimizeQueryExpression(query, searchIndex) + "&$count=true";

            if (expression == CloudQueryBuilder.Search.SearchForNothing)
            {

            }

            try
            {
                SearchLog.Log.Info(string.Format("Azure Search Query [{0}]: {1}", this.context.Index.Name, expression));
                var results = searchIndex.SearchService.Search(expression);

                if (string.IsNullOrEmpty(results))
                {
                    return new List<Dictionary<string, object>>();
                }

                var parsedResult = JObject.Parse(results);

                var valueList = parsedResult.SelectToken("value")
                        .Select(x => JsonHelper.Deserialize(x.ToString()) as Dictionary<string, object>)
                        .ToList();

                totalDoc = parsedResult["@odata.count"].ToObject<int>();

                var skip = this.GetRealSkipValue(query);
                var take = this.GetRealTakeValue(query);

                countDoc = this.CalculateActualCountPerPage(totalDoc, take, skip);

                var facetData = parsedResult.GetValue("@search.facets");
                if (facetData != null)
                {
                    facetResult = facetData.ToObject<Dictionary<string, object>>();
                }

                // This method is needed to workaround the old behavior of the CloudSearchResults.ElementAt
                valueList = this.AdoptResulltForElementAt(query, countDoc, valueList);

                return valueList;
            }
            catch (Exception ex)
            {
                SearchLog.Log.Error(string.Format("Azure Search Error [Index={0}] ERROR:{1} Search expression:{2}",
                    searchIndex.Name, ex.Message, query.Expression));

                throw;
            }
        }

        protected List<Dictionary<string, object>> AdoptResulltForElementAt(CloudQuery query, int actualDocCount, List<Dictionary<string, object>> curResult)
        {
            var elementAt = this.GetQueryMethods<ElementAtMethod>(query, QueryMethodType.ElementAt).FirstOrDefault();
            if (elementAt == null)
            {
                return curResult;
            }

            if (curResult.Count == 0)
            {
                return curResult;
            }

            if (curResult.Count > 1)
            {
                SearchLog.Log.Warn("SUPPORT Result for ElementAt contains more than 1 documents, only single will be used...");
            }

            Dictionary<string, object> doc = curResult[0];

            var adoptedResult = new Dictionary<string, object>[elementAt.Index + 1];
            adoptedResult[elementAt.Index] = doc;

            return adoptedResult.ToList();
        }


        protected virtual int CalculateActualCountPerPage(int total, int? take, int? skip)
        {
            // TODO Check case for skip >= total
            if (total == 0 || take == 0 || skip >= total)
            {
                return 0;
            }

            var rest = total;
            if (skip.HasValue)
            {
                rest -= skip.Value;
            }

            if (take.HasValue)
            {
                rest = Math.Min(take.Value, rest);
            }

            return Math.Min(rest, this.GetMaxNumberDocumentsInResult());
        }

        protected virtual int GetMaxNumberDocumentsInResult()
        {
            // Azure Search Service limitation
            const int AzureSearchMaxDocuments = 1000;

            var maxDocumentsPerPage = Settings.GetIntSetting("ContentSearch.AzureSearch.SearchMaxResults", -1);

            if (maxDocumentsPerPage == -1)
            {
                maxDocumentsPerPage = Settings.GetIntSetting("ContentSearch.SearchMaxResults", int.MaxValue);
            }

            return Math.Min(maxDocumentsPerPage, AzureSearchMaxDocuments);
        }


        protected virtual string OptimizeQueryExpression(CloudQuery query, CloudSearchProviderIndex index)
        {
            var expression = query.Expression;

            var skip = this.GetSkipParameterValue(query, index);
            string skipParameter = skip == null ? string.Empty : $"&$skip={skip}";

            var top = this.GetTopParameterValue(query, index);

            var facetExpression = this.GetFacetExpression(query, index);

            expression = CloudQueryBuilder.Merge(expression, facetExpression, "and", CloudQueryBuilder.ShouldWrap.Both);

            var orderByExpression = this.GetOrderByExpression(query, index);

            return $"{expression}{orderByExpression}&$top={top}{skipParameter}";
        }

        protected int? GetRealTakeValue(CloudQuery query)
        {
            var takeMethods = this.GetQueryMethods<TakeMethod>(query, QueryMethodType.Take).ToList();

            var totalTake = takeMethods.Count > 0 ? (int?)takeMethods.Sum(takeMethod => takeMethod.Count) : null;

            // MSDN descrption says if value < 0 return all values 
            return totalTake < 0 ? null : totalTake;
        }

        protected int? GetRealSkipValue(CloudQuery query)
        {
            var skipMethods = this.GetQueryMethods<SkipMethod>(query, QueryMethodType.Skip).ToList();

            var totalSkip = skipMethods.Count > 0 ? (int?) skipMethods.Sum(skipMethod => skipMethod.Count) : null;

            // MSDN descrption says if value < 0, elements must not be skipped
            return totalSkip < 0 ? 0 : totalSkip;
        }

        protected virtual int? GetSkipParameterValue(CloudQuery query, CloudSearchProviderIndex index)
        {
            int? skipValue = this.GetRealSkipValue(query);

            var elementAt = this.GetQueryMethods<ElementAtMethod>(query, QueryMethodType.ElementAt).FirstOrDefault();

            if (elementAt != null)
            {
                if (skipValue == null)
                {
                    skipValue = elementAt.Index;
                }
                else
                {
                    skipValue += elementAt.Index;
                }
            }

            return skipValue;
        }

        protected virtual int GetTopParameterValue(CloudQuery query, CloudSearchProviderIndex index)
        {
            // Take method 
            int? topValue = this.GetRealTakeValue(query);
            
            // Methods Executors
            var countMethod = this.GetQueryMethods<CountMethod>(query, QueryMethodType.Count).FirstOrDefault();

            if (countMethod != null)
            {
                topValue = 0;
            }

            var getFacetsMethod = this.GetQueryMethods<GetFacetsMethod>(query, QueryMethodType.GetFacets).FirstOrDefault();

            if (getFacetsMethod != null)
            {
                topValue = 0;
            }

            var firstMethod = this.GetQueryMethods<FirstMethod>(query, QueryMethodType.First).FirstOrDefault();

            if (firstMethod != null)
            {
                topValue = 1;
            }

            var singleMethod = this.GetQueryMethods<SingleMethod>(query, QueryMethodType.Single).FirstOrDefault();

            if (singleMethod != null)
            {
                topValue = 2;
            }

            var anyMethod = this.GetQueryMethods<AnyMethod>(query, QueryMethodType.Any).FirstOrDefault();

            if (anyMethod != null)
            {
                topValue = 1;
            }

            var elementAtMethod = this.GetQueryMethods<ElementAtMethod>(query, QueryMethodType.ElementAt).FirstOrDefault();

            if (elementAtMethod != null)
            {
                // topValue = 0 for cases .Take(3).ElementAt(4); where we know result is empty
                topValue = topValue <= elementAtMethod.Index ? 0 : 1;
            }

            return topValue ?? this.GetMaxNumberDocumentsInResult();
        }

        
        protected virtual SelectMethod GetSelectMethod(CloudQuery compositeQuery)
        {
            return this.GetQueryMethods<SelectMethod>(compositeQuery, QueryMethodType.Select).SingleOrDefault();
        }

        protected IEnumerable<TQMethod> GetQueryMethods<TQMethod>(CloudQuery compositeQuery, QueryMethodType methodType) where TQMethod : QueryMethod
        {
            return compositeQuery.Methods.Where(m => m.MethodType == methodType).Select(m => (TQMethod)m);
        }

        protected virtual string GetOrderByExpression(CloudQuery query, CloudSearchProviderIndex index)
        {
            return (string)miGetOrderByExpression.Invoke(this, new object[] {query, index});
        }

        protected virtual string GetFacetExpression(CloudQuery query, CloudSearchProviderIndex index)
        {
            return (string)miGetFacetExpression.Invoke(this, new object[] { query, index });
        }
    }
}