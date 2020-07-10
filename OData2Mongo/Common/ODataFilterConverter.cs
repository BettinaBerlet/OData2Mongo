using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    public class ODataFilterConverter
    {
        private static readonly List<BinaryOperatorKind> comparisonOperators = new List<BinaryOperatorKind> { BinaryOperatorKind.NotEqual, BinaryOperatorKind.GreaterThan, BinaryOperatorKind.GreaterThanOrEqual, BinaryOperatorKind.Equal, BinaryOperatorKind.LessThan, BinaryOperatorKind.LessThanOrEqual };
        private static readonly List<BinaryOperatorKind> logicalOperators = new List<BinaryOperatorKind> { BinaryOperatorKind.Or, BinaryOperatorKind.And };
        public static EdmModel model = BuildEdmModel();



        private static EdmModel BuildEdmModel()
        {
            var edmBuilder = new ODataConventionModelBuilder();

            EdmModel model = (EdmModel)edmBuilder.GetEdmModel();
            model.AddEntityType("Models", "ItemBase",
                    null /*baseType*/,
                    false /*isAbstract*/ ,
                    true /*isOpen*/);
            return model;
        }

        public static void ConvertODataQueryToMongoQuery(IQueryCollection query, out ProjectionDefinition<BsonDocument> projection, out FilterDefinition<BsonDocument> filterDefinition, out int? top_, out int? skip_, string defaultField = null)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            Microsoft.Extensions.Primitives.StringValues selection, top, skip, filter;
            if (query.TryGetValue("$select", out selection))
                values["$select"] = selection;
            if (query.TryGetValue("$top", out top))
                values["$top"] = top;
            if (query.TryGetValue("$skip", out skip))
                values["$skip"] = skip;
            if (query.TryGetValue("$filter", out filter))
                values["$filter"] = filter;
            projection = ConvertODataSelectionToBsonProjection(values, defaultField);
            filterDefinition = ConvertODataFilterToBsonFilter(values);
            top_ = GetTop(values);
            skip_ = GetSkip(values);
        }

        private static ODataQueryOptionParser GetQueryOptionsParser(IDictionary<string, string> queryOptions)
        {
            IEdmType edmType = model.FindDeclaredType("Models.ItemBase");
            IEdmNavigationSource navigationSource = model.FindDeclaredEntitySet("Models.ItemBase");
            return new ODataQueryOptionParser(ODataFilterConverter.model, edmType, navigationSource, queryOptions)
            { Resolver = new ODataUriResolver() { EnableCaseInsensitive = true } };
        }
        private static FilterDefinition<BsonDocument> ConvertODataFilterToBsonFilter(IDictionary<string, string> queryOptions)
        {

            ODataQueryOptionParser parser = GetQueryOptionsParser(queryOptions);
            try
            {
                FilterClause filter = parser.ParseFilter();
                BinaryOperatorNode expression = filter == null ? null : filter.Expression as BinaryOperatorNode;
                FilterDefinition<BsonDocument> filter_ = GetFilterExpression(expression);
                return filter_;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static ProjectionDefinition<BsonDocument> ConvertODataSelectionToBsonProjection(IDictionary<string, string> queryOptions, string defaultField = null)
        {
            ODataQueryOptionParser parser = GetQueryOptionsParser(queryOptions);
            SelectExpandClause selectExpand = parser.ParseSelectAndExpand();
            return selectExpand != null ? GetProjection(selectExpand) : GetProjection(defaultField);
        }

        private static ProjectionDefinition<BsonDocument> GetProjection(string fieldName)
        {
            return string.IsNullOrEmpty(fieldName) ? new BsonDocument() : new ProjectionDefinitionBuilder<BsonDocument>().Include(fieldName);
        }

        private static int? GetTop(IDictionary<string, string> queryOptions)
        {
            ODataQueryOptionParser parser = GetQueryOptionsParser(queryOptions);
            long? topValue = parser.ParseTop();
            return topValue.HasValue == true ? Convert.ToInt32(topValue) : (int?)null;
        }

        private static bool? GetCount(IDictionary<string, string> queryOptions)
        {
            ODataQueryOptionParser parser = GetQueryOptionsParser(queryOptions);
            return parser.ParseCount();
        }

        private static int? GetSkip(IDictionary<string, string> queryOptions)
        {
            ODataQueryOptionParser parser = GetQueryOptionsParser(queryOptions);
            long? skipValue = parser.ParseSkip();
            return skipValue.HasValue == true ? Convert.ToInt32(skipValue) : (int?)null;
        }

        private static OrderByClause GetOrderby(IDictionary<string, string> queryOptions)
        {
            ODataQueryOptionParser parser = GetQueryOptionsParser(queryOptions);
            OrderByClause orderBy = parser.ParseOrderBy();
            return parser.ParseOrderBy();
        }

        private static ProjectionDefinition<BsonDocument> GetProjection(SelectExpandClause select)
        {
            var builder = new ProjectionDefinitionBuilder<BsonDocument>();
            ProjectionDefinition<BsonDocument> projection = null;
            List<string> fields = new List<string>();
            foreach (var item in select.SelectedItems)
            {
                PathSelectItem selectItem = item as PathSelectItem;
                if (selectItem != null)
                {
                    fields.Add(selectItem.SelectedPath.FirstSegment.Identifier);
                }
            }
            if (fields.Count > 0)
            {
                projection = builder.Include(fields.First());
                for (int i = 1; i < fields.Count; i++)
                {
                    projection = projection.Include(fields[i]);
                }
            }
            return projection;
        }

        private static FilterDefinition<BsonDocument> GetFilterExpression(SingleValueNode? node)
        {
            if (node == null)
                return new BsonDocument();
            switch (node.Kind)
            {
                case QueryNodeKind.BinaryOperator:
                    return GetBinaryFilter(node as BinaryOperatorNode);
                case QueryNodeKind.Convert:
                    return GetFunctionFilter(node as ConvertNode);
                default:
                    //case not treated
                    return new BsonDocument();
            }
        }

        private static FilterDefinition<MongoDB.Bson.BsonDocument> GetFunctionFilter(ConvertNode node)
        {
            try
            {
                SingleValueFunctionCallNode functionNode = node.Source as SingleValueFunctionCallNode;
                IEnumerable<QueryNode> parameters = functionNode.Parameters;
                string name = ((parameters.ToList()[0] as ConvertNode).Source as SingleValueOpenPropertyAccessNode).Name;
                string value = (parameters.ToList()[1] as ConstantNode).Value as string;
                switch (functionNode.Name.ToLowerInvariant())
                {
                    case "startswith":
                        value = "/" + value.Substring(1, value.Length - 1) + "$/i";
                        return Builders<BsonDocument>.Filter.Regex(name, new BsonRegularExpression("/^" + value + "/i"));
                    case "endswith":
                        value = "/^" + value.Substring(0, value.Length - 1) + "/i";
                        return Builders<BsonDocument>.Filter.Regex(name, new BsonRegularExpression("/^" + value + "/i"));
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static FilterDefinition<BsonDocument> GetBinaryFilter(BinaryOperatorNode node)
        {
            BinaryOperatorKind opKind = (BinaryOperatorKind)node.OperatorKind;
            if (comparisonOperators.Contains(opKind))
            {
                return GetComparisonFilter(node);
            }
            if (logicalOperators.Contains(opKind))
            {
                return GetLogicalFilter(node);
            }
            return null;
        }

        private static FilterDefinition<BsonDocument> GetLogicalFilter(BinaryOperatorNode node)
        {
            SingleValueNode left = node.Left;
            SingleValueNode right = node.Right;
            FilterDefinition<BsonDocument> leftFilter = GetFilterExpression(left);
            FilterDefinition<BsonDocument> rightFilter = GetFilterExpression(right);
            switch (node.OperatorKind)
            {
                case BinaryOperatorKind.And:
                    return Builders<BsonDocument>.Filter.And(leftFilter, rightFilter);
                case BinaryOperatorKind.Or:
                    return Builders<BsonDocument>.Filter.Or(leftFilter, rightFilter);
                default:
                    return null;
            }
        }

        private static FilterDefinition<BsonDocument> GetComparisonFilter(BinaryOperatorNode node)
        {
            ConvertNode left = (ConvertNode)node.Left;
            ConstantNode right = (ConstantNode)node.Right;
            string name = (left.Source as SingleValueOpenPropertyAccessNode).Name;
            object value = right.Value;
            switch (node.OperatorKind)
            {
                case BinaryOperatorKind.NotEqual:
                    return Builders<BsonDocument>.Filter.Ne(name, value);
                case BinaryOperatorKind.GreaterThan:
                    return Builders<BsonDocument>.Filter.Gt(name, value);
                case BinaryOperatorKind.GreaterThanOrEqual:
                    return Builders<BsonDocument>.Filter.Gte(name, value);
                case BinaryOperatorKind.Equal:
                    return Builders<BsonDocument>.Filter.Eq(name, value);
                case BinaryOperatorKind.LessThanOrEqual:
                    return Builders<BsonDocument>.Filter.Lte(name, value);
                default:
                    return null;
            }
        }
    }
}
