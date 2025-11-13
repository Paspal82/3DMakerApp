using MongoDB.Driver;
using MongoDB.Bson;
using System.Text.RegularExpressions;
using _3DMakerApp.Server.Models;

namespace _3DMakerApp.Server.Services
{
    public class ProductService
    {
        private readonly IMongoCollection<Product> _products;

        public ProductService(IConfiguration configuration)
        {
            var connectionString = configuration.GetValue<string>("Mongo:ConnectionString") ?? "mongodb://localhost:27017";
            var dbName = configuration.GetValue<string>("Mongo:Database") ?? "3dmakerdb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(dbName);
            _products = database.GetCollection<Product>("Products");
        }

        public async Task<List<Product>> GetAsync()
        {
            return await _products.Find(_ => true).ToListAsync();
        }

        public async Task<Product?> GetAsync(string id)
        {
            return await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(Product product)
        {
            await _products.InsertOneAsync(product);
        }

        public async Task UpdateAsync(string id, Product updated)
        {
            await _products.ReplaceOneAsync(p => p.Id == id, updated);
        }

        public async Task RemoveAsync(string id)
        {
            await _products.DeleteOneAsync(p => p.Id == id);
        }

        // New: paged query with optional filter/search
        public async Task<(List<Product> Items, long Total)> QueryAsync(string? search, string? nameFilter, int page, int pageSize)
        {
            var filterBuilder = Builders<Product>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var escaped = Regex.Escape(search);
                var pattern = $"(?i).*{escaped}.*"; // case-insensitive contains
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex("Name", pattern),
                    filterBuilder.Regex("Description", pattern)
                );
                filter = filter & searchFilter;
            }

            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                filter &= filterBuilder.Eq("Name", nameFilter);
            }

            var total = await _products.CountDocumentsAsync(filter);

            var items = await _products.Find(filter)
                .Sort(Builders<Product>.Sort.Descending("Name"))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, total);
        }

        // Return distinct product names
        public async Task<List<string>> GetDistinctNamesAsync()
        {
            var names = await _products.Find(_ => true).Project(p => p.Name).ToListAsync();
            return names.Distinct().OrderBy(n => n).ToList();
        }
    }
}
