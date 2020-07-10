using Common;
using Microsoft.AspNetCore.Http;
using Models;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OData2Mongo.Repositories
{
    public class Repository : IRepositoryBase
    {
 
        private IMongoDatabase dataBase { get; set; }
        public Repository()
        {
            //init dummy database
            var _runner = MongoDbRunner.StartForDebugging();
            _runner.Import("BooksDB", "books", "Resources/books.json", true);
            MongoClient client = new MongoClient(_runner.ConnectionString);
            dataBase = client.GetDatabase("BooksDB");
            
        }

        DeleteResult IRepositoryBase.Delete<T>(string id)
        {
            throw new NotImplementedException();
        }

        ReplaceOneResult IRepositoryBase.Upsert<T>(T item)
        {
            throw new NotImplementedException();
        }

        T IRepositoryBase.GetById<T>(string id)
        {
            throw new NotImplementedException();
        }

        IEnumerable<T> IRepositoryBase.Find<T>(string collectionName, IQueryCollection query, string defaultFilter)
        {
            ProjectionDefinition<BsonDocument> projection;
            FilterDefinition<BsonDocument> filter;
            int? top, skip;
            ODataFilterConverter.ConvertODataQueryToMongoQuery(query, out projection, out filter, out top, out skip, defaultFilter);
            top = top.HasValue ? top.Value : 100;
            skip = skip.HasValue ? skip.Value : 0;
            var documents = dataBase.GetCollection<BsonDocument>(collectionName).Find<BsonDocument>(filter);
            var test = documents.Project<T>(projection).Skip((int)skip).ToEnumerable<T>();
            return documents.Project<T>(projection).Skip((int)skip).Limit((int)top).ToEnumerable<T>();
        }
    }
}