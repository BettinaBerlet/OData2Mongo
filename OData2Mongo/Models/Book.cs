
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Models
{
    [BsonIgnoreExtraElements]
    public class Book : ItemBase
    {
        public string title;
        [BsonExtraElements]
        [BsonDictionaryOptions(DictionaryRepresentation.Document)]
        public IDictionary<string, Object> data;
    }
}
