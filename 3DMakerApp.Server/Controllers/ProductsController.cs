using Microsoft.AspNetCore.Mvc;
using _3DMakerApp.Server.Models;
using _3DMakerApp.Server.Services;

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

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> Get(string id)
        {
            var product = await _service.GetAsync(id);
            if (product == null) return NotFound();
            return product;
        }

        [HttpPost]
        public async Task<IActionResult> Create(Product product)
        {
            await _service.CreateAsync(product);
            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, Product product)
        {
            var existing = await _service.GetAsync(id);
            if (existing == null) return NotFound();
            product.Id = id;
            await _service.UpdateAsync(id, product);
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
}
