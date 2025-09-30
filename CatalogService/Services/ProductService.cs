using CatalogService.Data;
using CatalogService.DTOs;
using CatalogService.Models;
using ECommerce.Common.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Services
{
    public class ProductService : IProductService
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<ProductService> _logger;

        public ProductService(CatalogDbContext context, ILogger<ProductService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PagedResponse<ProductDto>> GetProductsAsync(
        int pageNumber, int pageSize, string? category, decimal? minPrice, decimal? maxPrice)
        {
            var query = _context.Products.Where(p => p.IsActive);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(p => p.Category == category);

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderBy(p => p.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    Category = p.Category,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return new PagedResponse<ProductDto>
            {
                Items = items,
                TotalItems = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<ProductDto?> GetProductByIdAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return null;

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                Category = product.Category,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt
            };
        }

        public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
        {
            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock,
                Category = dto.Category
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Producto creado con ID {ProductId}", product.Id);

            return await GetProductByIdAsync(product.Id) ?? throw new InvalidOperationException();
        }

        public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return null;

            if (!string.IsNullOrEmpty(dto.Name))
                product.Name = dto.Name;

            if (!string.IsNullOrEmpty(dto.Description))
                product.Description = dto.Description;

            if (dto.Price.HasValue)
                product.Price = dto.Price.Value;

            if (dto.Stock.HasValue)
                product.Stock = dto.Stock.Value;

            if (!string.IsNullOrEmpty(dto.Category))
                product.Category = dto.Category;

            if (dto.IsActive.HasValue)
                product.IsActive = dto.IsActive.Value;

            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Producto {ProductId} actualizado", id);

            return await GetProductByIdAsync(id);
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return false;

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Producto {ProductId} borrado", id);

            return true;
        }

        public async Task<PagedResponse<ProductDto>> SearchProductsAsync(
            string query, int pageNumber, int pageSize)
        {
            var products = _context.Products
                .Where(p => p.IsActive &&
                       (p.Name.Contains(query) || p.Description.Contains(query)));

            var totalItems = await products.CountAsync();

            var items = await products
                .OrderBy(p => p.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    Category = p.Category,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return new PagedResponse<ProductDto>
            {
                Items = items,
                TotalItems = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<int?> GetProductStockAsync(int id)
        {
            var product = await _context.Products
                .Where(p => p.Id == id)
                .Select(p => new { p.Stock })
                .FirstOrDefaultAsync();

            return product?.Stock;
        }

        //public async Task<List<ProductDto>> GetAllProductsAsync()
        //{
        //    return await _context.Products
        //        .Where(p => p.IsActive)
        //        .Select(p => MapToDto(p))
        //        .ToListAsync();
        //}

        //public async Task<ProductDto?> GetProductByIdAsync(int id)
        //{
        //    var product = await _context.Products.FindAsync(id);
        //    return product == null ? null : MapToDto(product);
        //}

        //public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
        //{
        //    var product = new Product
        //    {
        //        Name = dto.Name,
        //        Description = dto.Description,
        //        Price = dto.Price,
        //        Stock = dto.Stock,
        //        Category = dto.Category
        //    };

        //    _context.Products.Add(product);
        //    await _context.SaveChangesAsync();

        //    _logger.LogInformation("Product created with ID {ProductId}", product.Id);

        //    return MapToDto(product);
        //}

        //public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto)
        //{
        //    var product = await _context.Products.FindAsync(id);
        //    if (product == null)
        //        return null;

        //    if (!string.IsNullOrEmpty(dto.Name))
        //        product.Name = dto.Name;

        //    if (!string.IsNullOrEmpty(dto.Description))
        //        product.Description = dto.Description;

        //    if (dto.Price.HasValue)
        //        product.Price = dto.Price.Value;

        //    if (dto.Stock.HasValue)
        //        product.Stock = dto.Stock.Value;

        //    if (!string.IsNullOrEmpty(dto.Category))
        //        product.Category = dto.Category;

        //    if (dto.IsActive.HasValue)
        //        product.IsActive = dto.IsActive.Value;

        //    product.UpdatedAt = DateTime.UtcNow;

        //    await _context.SaveChangesAsync();

        //    _logger.LogInformation("Product {ProductId} updated", id);

        //    return MapToDto(product);
        //}

        //public async Task<bool> DeleteProductAsync(int id)
        //{
        //    var product = await _context.Products.FindAsync(id);
        //    if (product == null)
        //        return false;

        //    _context.Products.Remove(product);
        //    await _context.SaveChangesAsync();

        //    _logger.LogInformation("Product {ProductId} deleted", id);

        //    return true;
        //}

        //public async Task<List<ProductDto>> SearchProductsAsync(string searchTerm)
        //{
        //    return await _context.Products
        //        .Where(p => p.IsActive &&
        //               (p.Name.Contains(searchTerm) ||
        //                p.Description.Contains(searchTerm) ||
        //                p.Category.Contains(searchTerm)))
        //        .Select(p => MapToDto(p))
        //        .ToListAsync();
        //}

        //private static ProductDto MapToDto(Product product)
        //{
        //    return new ProductDto
        //    {
        //        Id = product.Id,
        //        Name = product.Name,
        //        Description = product.Description,
        //        Price = product.Price,
        //        Stock = product.Stock,
        //        Category = product.Category,
        //        IsActive = product.IsActive,
        //        CreatedAt = product.CreatedAt
        //    };
        //}

        //public Task<int?> GetProductStockAsync(int id)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
