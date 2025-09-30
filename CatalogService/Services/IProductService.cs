using CatalogService.DTOs;
using ECommerce.Common.DTOs;

namespace CatalogService.Services
{
    public interface IProductService
    {
        Task<PagedResponse<ProductDto>> GetProductsAsync(int pageNumber, int pageSize,
       string? category, decimal? minPrice, decimal? maxPrice);
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<ProductDto> CreateProductAsync(CreateProductDto dto);
        Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto);
        Task<bool> DeleteProductAsync(int id);
        Task<PagedResponse<ProductDto>> SearchProductsAsync(string query, int pageNumber, int pageSize);
        Task<int?> GetProductStockAsync(int id);
        //Task<List<ProductDto>> GetAllProductsAsync();
        //Task<ProductDto?> GetProductByIdAsync(int id);
        //Task<ProductDto> CreateProductAsync(CreateProductDto dto);
        //Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto);
        //Task<bool> DeleteProductAsync(int id);
        //Task<List<ProductDto>> SearchProductsAsync(string searchTerm);
        //Task<int?> GetProductStockAsync(int id);
    }
}
