using Microsoft.AspNetCore.Mvc;
using _3DMakerApp.Server.Models;
using _3DMakerApp.Server.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace _3DMakerApp.Server.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/images")]
    public class ProductImagesController : ControllerBase
    {
        private readonly ProductImageService _imageService;
        private readonly ProductService _productService;

        public ProductImagesController(ProductImageService imageService, ProductService productService)
        {
            _imageService = imageService;
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductImage>>> GetImages(string productId)
        {
            var product = await _productService.GetAsync(productId);
            if (product == null) return NotFound("Product not found");

            var images = await _imageService.GetByProductIdAsync(productId);
            return Ok(images);
        }

        [HttpGet("{imageId}")]
        public async Task<ActionResult<ProductImage>> GetImage(string productId, string imageId)
        {
            var image = await _imageService.GetAsync(imageId);
            if (image == null || image.ProductId != productId) return NotFound();
            return Ok(image);
        }

        [HttpPost]
        [RequestSizeLimit(50_000_000)] // 50 MB for multiple images
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages(string productId, [FromForm] MultiImageUploadForm form)
        {
            var product = await _productService.GetAsync(productId);
            if (product == null) return NotFound("Product not found");

            if (form.Images == null || !form.Images.Any())
                return BadRequest("No images provided");

            var createdImages = new List<ProductImage>();
            var existingImages = await _imageService.GetByProductIdAsync(productId);
            int startOrder = existingImages.Count;

            for (int i = 0; i < form.Images.Count; i++)
            {
                var file = form.Images[i];

                if (!IsAllowedImage(file))
                    continue; // Skip invalid images

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var originalBytes = ms.ToArray();

                var productImage = new ProductImage
                {
                    ProductId = productId,
                    Order = startOrder + i,
                    IsCover = existingImages.Count == 0 && i == 0, // First image is cover if no images exist
                    Image = originalBytes,
                    ImageContentType = file.ContentType ?? "image/png"
                };

                // Generate thumbnails
                using var stream1 = new MemoryStream(originalBytes);
                var (cardBytes, cardType) = await GenerateThumbnailAsync(stream1, 220, file.ContentType ?? "image/png");
                productImage.ThumbnailCard = cardBytes;
                productImage.ThumbnailCardContentType = cardType;

                using var stream2 = new MemoryStream(originalBytes);
                var (detailBytes, detailType) = await GenerateThumbnailAsync(stream2, 512, file.ContentType ?? "image/png");
                productImage.ThumbnailDetail = detailBytes;
                productImage.ThumbnailDetailContentType = detailType;

                using var stream3 = new MemoryStream(originalBytes);
                var (sliderBytes, sliderType) = await GenerateThumbnailAsync(stream3, 120, file.ContentType ?? "image/png");
                productImage.ThumbnailSlider = sliderBytes;
                productImage.ThumbnailSliderContentType = sliderType;

                await _imageService.CreateAsync(productImage);
                createdImages.Add(productImage);
            }

            return Ok(createdImages);
        }

        [HttpPut("{imageId}/set-cover")]
        public async Task<IActionResult> SetCoverImage(string productId, string imageId)
        {
            var image = await _imageService.GetAsync(imageId);
            if (image == null || image.ProductId != productId) return NotFound();

            await _imageService.SetCoverImageAsync(productId, imageId);
            return NoContent();
        }

        [HttpPut("reorder")]
        public async Task<IActionResult> ReorderImages(string productId, [FromBody] ReorderRequest request)
        {
            await _imageService.ReorderImagesAsync(productId, request.ImageIds);
            return NoContent();
        }

        [HttpDelete("{imageId}")]
        public async Task<IActionResult> DeleteImage(string productId, string imageId)
        {
            var image = await _imageService.GetAsync(imageId);
            if (image == null || image.ProductId != productId) return NotFound();

            // If deleting cover image, set another image as cover
            if (image.IsCover)
            {
                var allImages = await _imageService.GetByProductIdAsync(productId);
                var nextCover = allImages.FirstOrDefault(x => x.Id != imageId);
                if (nextCover != null)
                {
                    await _imageService.SetCoverImageAsync(productId, nextCover.Id!);
                }
            }

            await _imageService.DeleteAsync(imageId);
            return NoContent();
        }

        private bool IsAllowedImage(IFormFile file)
        {
            if (file == null) return false;
            var allowed = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp" };
            if (!allowed.Contains(file.ContentType?.ToLowerInvariant())) return false;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext == null) return false;
            if (!(ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp")) return false;

            return true;
        }

        private async Task<(byte[] data, string contentType)> GenerateThumbnailAsync(Stream inputStream, int size, string formatHint)
        {
            using var image = await Image.LoadAsync(inputStream);

            var options = new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Crop
            };

            image.Mutate(x => x.Resize(options));

            IImageEncoder encoder;
            var contentType = "image/png";
            formatHint = formatHint?.ToLowerInvariant() ?? "";
            if (formatHint.Contains("jpeg") || formatHint.Contains("jpg") || formatHint.Contains("image/jpeg"))
            {
                encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 85 };
                contentType = "image/jpeg";
            }
            else if (formatHint.Contains("webp"))
            {
                encoder = new SixLabors.ImageSharp.Formats.Webp.WebpEncoder { Quality = 85 };
                contentType = "image/webp";
            }
            else
            {
                encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder();
                contentType = "image/png";
            }

            using var ms = new MemoryStream();
            await image.SaveAsync(ms, encoder);
            return (ms.ToArray(), contentType);
        }
    }

    public class MultiImageUploadForm
    {
        public List<IFormFile> Images { get; set; } = new();
    }

    public class ReorderRequest
    {
        public List<string> ImageIds { get; set; } = new();
    }
}
