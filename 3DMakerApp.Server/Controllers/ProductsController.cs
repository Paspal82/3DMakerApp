using Microsoft.AspNetCore.Mvc;
using _3DMakerApp.Server.Models;
using _3DMakerApp.Server.Services;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace _3DMakerApp.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ProductService _service;

        public ProductsController(ProductService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IEnumerable<Product>> Get()
        {
            return await _service.GetAsync();
        }

        [HttpGet("query")]
        public async Task<IActionResult> Query([FromQuery] string? search, [FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] string? sortBy = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 12;

            var (items, total) = await _service.QueryAsync(search, name, page, pageSize, sortBy);

            // Ensure thumbnails exist for items returned (generate on-the-fly for products imported before thumbnail generation was added)
            var tasks = new List<Task>();
            foreach (var item in items)
            {
                if ((item.ThumbnailCard == null || item.ThumbnailCard.Length == 0) && item.Image != null && item.Image.Length > 0)
                {
                    // generate card thumbnail in background and attach to the item (do not persist)
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using var msImg = new MemoryStream(item.Image);
                            var (thumb, ctype) = await GenerateThumbnailAsync(msImg, 220, item.ImageContentType ?? "image/png");
                            item.ThumbnailCard = thumb;
                            item.ThumbnailCardContentType = ctype;
                        }
                        catch
                        {
                            // ignore thumbnail generation errors for response
                        }
                    }));
                }

                if ((item.ThumbnailDetail == null || item.ThumbnailDetail.Length == 0) && item.Image != null && item.Image.Length > 0)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using var msImg = new MemoryStream(item.Image);
                            var (thumb, ctype) = await GenerateThumbnailAsync(msImg, 512, item.ImageContentType ?? "image/png");
                            item.ThumbnailDetail = thumb;
                            item.ThumbnailDetailContentType = ctype;
                        }
                        catch { }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            return Ok(new
            {
                items,
                total,
                page,
                pageSize
            });
        }

        [HttpGet("names")]
        public async Task<IActionResult> Names()
        {
            var names = await _service.GetDistinctNamesAsync();
            return Ok(names);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> Get(string id)
        {
            var product = await _service.GetAsync(id);
            if (product == null) return NotFound();
            return product;
        }

        private bool IsAllowedImage(IFormFile file)
        {
            if (file == null) return false;
            var allowed = new[] { "image/png", "image/jpeg" };
            if (!allowed.Contains(file.ContentType?.ToLowerInvariant())) return false;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext == null) return false;
            if (!(ext == ".png" || ext == ".jpg" || ext == ".jpeg")) return false;

            return true;
        }

        private bool TryParsePrice(string? value, out decimal result)
        {
            result = 0m;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var v = value.Trim();
            // remove spaces
            v = v.Replace(" ", string.Empty);

            // If no separators, try simple parse
            if (!v.Contains('.') && !v.Contains(','))
            {
                return decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
            }

            // Treat last separator (dot or comma) as decimal separator, remove other separators (thousands)
            var lastDot = v.LastIndexOf('.');
            var lastComma = v.LastIndexOf(',');
            var lastSepIndex = Math.Max(lastDot, lastComma);
            var sepChar = v[lastSepIndex];

            var before = v.Substring(0, lastSepIndex);
            var after = v.Substring(lastSepIndex + 1);

            // remove any separator characters from integer and fraction parts
            before = before.Replace(".", string.Empty).Replace(",", string.Empty);
            after = after.Replace(".", string.Empty).Replace(",", string.Empty);

            var normalized = before + "." + after; // use dot as decimal for invariant

            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            // Fallbacks (try replacing comma with dot and invariant)
            var alt = v.Replace(',', '.');
            if (decimal.TryParse(alt, NumberStyles.Number, CultureInfo.InvariantCulture, out result)) return true;

            // Try current culture as last resort
            if (decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, out result)) return true;

            return false;
        }

        private async Task<(byte[] data, string contentType)> GenerateThumbnailAsync(Stream inputStream, int size, string formatHint)
        {
            // use ImageSharp to load, resize with cover (crop) and encode to original format
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
            else
            {
                encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder();
                contentType = "image/png";
            }

            using var ms = new MemoryStream();
            await image.SaveAsync(ms, encoder);
            return (ms.ToArray(), contentType);
        }

        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ProductForm form)
        {
            if (!TryParsePrice(form.Price, out var parsedPrice))
            {
                return BadRequest("Invalid price format.");
            }

            // Round to 2 decimal places
            parsedPrice = decimal.Round(parsedPrice, 2, MidpointRounding.AwayFromZero);

            var product = new Product
            {
                Name = form.Name,
                Description = form.Description,
                Price = parsedPrice
            };

            if (form.Image != null)
            {
                if (!IsAllowedImage(form.Image))
                {
                    return BadRequest("Only PNG and JPEG images are allowed.");
                }

                using var ms = new MemoryStream();
                await form.Image.CopyToAsync(ms);
                var originalBytes = ms.ToArray();
                product.Image = originalBytes;
                product.ImageContentType = form.Image.ContentType;

                // generate thumbnails: card (220) and detail (512)
                using var stream1 = new MemoryStream(originalBytes);
                var (cardBytes, cardType) = await GenerateThumbnailAsync(stream1, 220, form.Image.ContentType ?? "image/png");
                product.ThumbnailCard = cardBytes;
                product.ThumbnailCardContentType = cardType;

                using var stream2 = new MemoryStream(originalBytes);
                var (detailBytes, detailType) = await GenerateThumbnailAsync(stream2, 512, form.Image.ContentType ?? "image/png");
                product.ThumbnailDetail = detailBytes;
                product.ThumbnailDetailContentType = detailType;
            }

            await _service.CreateAsync(product);
            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        [RequestSizeLimit(10_000_000)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(string id, [FromForm] ProductForm form)
        {
            var existing = await _service.GetAsync(id);
            if (existing == null) return NotFound();

            if (!TryParsePrice(form.Price, out var parsedPrice))
            {
                return BadRequest("Invalid price format.");
            }

            // Round to 2 decimal places
            parsedPrice = decimal.Round(parsedPrice, 2, MidpointRounding.AwayFromZero);

            existing.Name = form.Name;
            existing.Description = form.Description;
            existing.Price = parsedPrice;

            if (form.Image != null)
            {
                if (!IsAllowedImage(form.Image))
                {
                    return BadRequest("Only PNG and JPEG images are allowed.");
                }

                using var ms = new MemoryStream();
                await form.Image.CopyToAsync(ms);
                var originalBytes = ms.ToArray();
                existing.Image = originalBytes;
                existing.ImageContentType = form.Image.ContentType;

                // generate thumbnails: card (220) and detail (512)
                using var stream1 = new MemoryStream(originalBytes);
                var (cardBytes, cardType) = await GenerateThumbnailAsync(stream1, 220, form.Image.ContentType ?? "image/png");
                existing.ThumbnailCard = cardBytes;
                existing.ThumbnailCardContentType = cardType;

                using var stream2 = new MemoryStream(originalBytes);
                var (detailBytes, detailType) = await GenerateThumbnailAsync(stream2, 512, form.Image.ContentType ?? "image/png");
                existing.ThumbnailDetail = detailBytes;
                existing.ThumbnailDetailContentType = detailType;
            }

            await _service.UpdateAsync(id, existing);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _service.GetAsync(id);
            if (existing == null) return NotFound();
            await _service.RemoveAsync(id);
            return NoContent();
        }
    }

    public class ProductForm
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        // accept price as string so we can parse various decimal separators
        public string Price { get; set; } = "0";

        public IFormFile? Image { get; set; }
    }
}
