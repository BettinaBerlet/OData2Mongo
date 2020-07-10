using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models;
using MongoDB.Bson;
using Repositories;

namespace OData2Mongo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        public IRepositoryBase db { get; set; }

        public BooksController(IRepositoryBase repository)
        {
            this.db = repository;
        }
        [HttpGet]
        public IEnumerable<Book> Get()
        {
            string defaultFilter = null;
            Microsoft.Extensions.Primitives.StringValues select;
            if (!HttpContext.Request.Query.TryGetValue("$select", out select))
            {
                defaultFilter = "title";
            }
            return db.Find<Book>("books", HttpContext.Request.Query, defaultFilter);
        }
    }
}