using Models;
using Microsoft.AspNetCore.Http;
using Microsoft.OData.Edm;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;

namespace Repositories
{
    public interface IRepositoryBase
    {
        DeleteResult Delete<T>(string id) where T : ItemBase;
        ReplaceOneResult Upsert<T>(T item) where T : ItemBase;
        T GetById<T>(string id) where T : ItemBase;
        IEnumerable<T> Find<T>(string collectionName, IQueryCollection query, string defaultFilter = null) where T : ItemBase;
    }
}
