using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Controllers;

[ApiController]
[Route("api/brands")]
public class BrandsController : ControllerBase
{
    private readonly IBrandService _svc;
    public BrandsController(IBrandService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<BrandDto>>> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BrandDto>> Get(int id)
    {
        var b = await _svc.GetByIdAsync(id);
        return b is null ? NotFound() : Ok(b);
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<BrandDto>> BySlug(string slug)
    {
        var b = await _svc.GetBySlugAsync(slug);
        return b is null ? NotFound() : Ok(b);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<BrandDto>> Create([FromBody] BrandUpsertDto dto)
        => Ok(await _svc.CreateAsync(dto));

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BrandDto>> Update(int id, [FromBody] BrandUpsertDto dto)
    {
        var b = await _svc.UpdateAsync(id, dto);
        return b is null ? NotFound() : Ok(b);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _svc.DeleteAsync(id) ? NoContent() : NotFound();
}
