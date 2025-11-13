using MongoDB.Driver;
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
    }
}
