using MongoDB.Driver;
using _3DMakerApp.Server.Models;

namespace _3DMakerApp.Server.Services
{
    public class ProductImageService
    {
        private readonly IMongoCollection<ProductImage> _images;

        public ProductImageService(IConfiguration configuration)
        {
            var connectionString = configuration.GetValue<string>("Mongo:ConnectionString") ?? "mongodb://localhost:27017";
            var dbName = configuration.GetValue<string>("Mongo:Database") ?? "3dmakerdb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(dbName);
            _images = database.GetCollection<ProductImage>("ProductImages");

            // Create indexes
            var indexKeys = Builders<ProductImage>.IndexKeys
                .Ascending(x => x.ProductId)
                .Ascending(x => x.Order);
            var indexModel = new CreateIndexModel<ProductImage>(indexKeys);
            _images.Indexes.CreateOneAsync(indexModel);
        }

        public async Task<List<ProductImage>> GetByProductIdAsync(string productId)
        {
            return await _images
                .Find(x => x.ProductId == productId)
                .SortBy(x => x.Order)
                .ToListAsync();
        }

        public async Task<ProductImage?> GetCoverImageAsync(string productId)
        {
            return await _images
                .Find(x => x.ProductId == productId && x.IsCover)
                .FirstOrDefaultAsync();
        }

        public async Task<ProductImage?> GetAsync(string id)
        {
            return await _images.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(ProductImage image)
        {
            await _images.InsertOneAsync(image);
        }

        public async Task CreateManyAsync(IEnumerable<ProductImage> images)
        {
            await _images.InsertManyAsync(images);
        }

        public async Task UpdateAsync(string id, ProductImage image)
        {
            await _images.ReplaceOneAsync(x => x.Id == id, image);
        }

        public async Task DeleteAsync(string id)
        {
            await _images.DeleteOneAsync(x => x.Id == id);
        }

        public async Task DeleteByProductIdAsync(string productId)
        {
            await _images.DeleteManyAsync(x => x.ProductId == productId);
        }

        public async Task SetCoverImageAsync(string productId, string imageId)
        {
            // Unset all covers for this product
            var filter = Builders<ProductImage>.Filter.Eq(x => x.ProductId, productId);
            var update = Builders<ProductImage>.Update.Set(x => x.IsCover, false);
            await _images.UpdateManyAsync(filter, update);

            // Set new cover
            await _images.UpdateOneAsync(
                x => x.Id == imageId,
                Builders<ProductImage>.Update.Set(x => x.IsCover, true)
            );
        }

        public async Task ReorderImagesAsync(string productId, List<string> imageIds)
        {
            for (int i = 0; i < imageIds.Count; i++)
            {
                await _images.UpdateOneAsync(
                    x => x.Id == imageIds[i],
                    Builders<ProductImage>.Update.Set(x => x.Order, i)
                );
            }
        }
    }
}
