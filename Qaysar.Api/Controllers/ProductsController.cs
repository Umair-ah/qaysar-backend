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
    private readonly IProductExportService _exportSvc;
    private readonly IProductImportService _importSvc;

    public ProductsController(IProductService svc, IProductExportService exportSvc, IProductImportService importSvc)
    {
        _svc = svc;
        _exportSvc = exportSvc;
        _importSvc = importSvc;
    }

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

    // ---- Bulk import / export ----

    /// <summary>
    /// Exports every product to an .xlsx workbook. Image columns (Image1..Image10) contain
    /// filenames only — never URLs, never embedded images — matched to Images.zip on re-import.
    /// </summary>
    [Authorize]
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var bytes = await _exportSvc.ExportAsync(ct);
        var fileName = $"products-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Bulk-updates existing products from Products.xlsx, optionally replacing their images from
    /// Images.zip. All-or-nothing: if any row fails validation, nothing is written or uploaded —
    /// every validation error is returned together so the sheet can be fixed and re-submitted.
    /// </summary>
    [Authorize]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(500_000_000)]
    public async Task<ActionResult<BulkImportResultDto>> Import(IFormFile excel, IFormFile? imagesZip, CancellationToken ct)
    {
        if (excel is null || excel.Length == 0)
            return BadRequest(new { message = "An Excel (.xlsx) file is required." });

        await using var excelStream = excel.OpenReadStream();
        Stream? zipStream = imagesZip is { Length: > 0 } ? imagesZip.OpenReadStream() : null;
        try
        {
            var result = await _importSvc.ImportAsync(excelStream, zipStream, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
        finally
        {
            if (zipStream is not null) await zipStream.DisposeAsync();
        }
    }
}
