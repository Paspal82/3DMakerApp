using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace _3DMakerApp.Server.Models
{
    /// <summary>
    /// Represents a product image with thumbnails for different display contexts
    /// </summary>
    public class ProductImage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// Reference to parent product
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// Display order in image gallery (0-based)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Is this the cover/featured image (shown in cards and cart)
        /// </summary>
        public bool IsCover { get; set; }

        /// <summary>
        /// Original full-size image (base64)
        /// </summary>
        public byte[] Image { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Content type of original image (e.g., "image/png", "image/jpeg")
        /// </summary>
        public string ImageContentType { get; set; } = "image/png";

        /// <summary>
        /// Thumbnail for product cards (220x220)
        /// </summary>
        public byte[]? ThumbnailCard { get; set; }
        public string? ThumbnailCardContentType { get; set; }

        /// <summary>
        /// Thumbnail for detail modal (512x512)
        /// </summary>
        public byte[]? ThumbnailDetail { get; set; }
        public string? ThumbnailDetailContentType { get; set; }

        /// <summary>
        /// Thumbnail for slider preview (120x120)
        /// </summary>
        public byte[]? ThumbnailSlider { get; set; }
        public string? ThumbnailSliderContentType { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
