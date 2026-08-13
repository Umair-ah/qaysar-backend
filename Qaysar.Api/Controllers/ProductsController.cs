using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _svc;
    public ProductsController(IProductService svc) => _svc = svc;

    /// <summary>
    /// Public listing (only visible products). Paginated for infinite scroll.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetPublic(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? brandId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? inStock = null)
    {
        var res = await _svc.GetPagedAsync(page, pageSize, search, brandId, categoryId, onlyVisible: true, inStock);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDetailCustomerDto>> Get(int id)
    {
        var p = await _svc.GetPublicByIdAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    // ---- Admin ----

    [Authorize]
    [HttpGet("admin")]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? brandId = null,
        [FromQuery] int? categoryId = null)
    {
        var res = await _svc.GetPagedAsync(page, pageSize, search, brandId, categoryId, onlyVisible: false, inStock: null);
        return Ok(res);
    }

    [Authorize]
    [HttpGet("admin/{id:int}")]
    public async Task<ActionResult<ProductDetailDto>> GetAdminById(int id)
    {
        var p = await _svc.GetByIdAsync(id, onlyVisible: false);
        return p is null ? NotFound() : Ok(p);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ProductDetailDto>> Create([FromBody] ProductUpsertDto dto)
    {
        try
        {
            var p = await _svc.CreateAsync(dto);
            return Ok(p);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDetailDto>> Update(int id, [FromBody] ProductUpsertDto dto)
    {
        try
        {
            var p = await _svc.UpdateAsync(id, dto);
            return p is null ? NotFound() : Ok(p);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _svc.DeleteAsync(id) ? NoContent() : NotFound();
}
