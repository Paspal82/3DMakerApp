using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace _3DMakerApp.Server.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }

        // Image stored as binary in MongoDB (original full-size image)
        public byte[]? Image { get; set; }
        // Optional content type (e.g. "image/png") to build data URI on client
        public string? ImageContentType { get; set; }

        // Thumbnails for different UI contexts (card, detail)
        public byte[]? ThumbnailCard { get; set; }
        public string? ThumbnailCardContentType { get; set; }

        public byte[]? ThumbnailDetail { get; set; }
        public string? ThumbnailDetailContentType { get; set; }

        // Optional. If not provided, defaults to now (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}