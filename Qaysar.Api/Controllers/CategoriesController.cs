using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _svc;
    public CategoriesController(ICategoryService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll([FromQuery] string? search = null) => Ok(await _svc.GetAllAsync(search));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Get(int id)
    {
        var c = await _svc.GetByIdAsync(id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<CategoryDto>> BySlug(string slug)
    {
        var c = await _svc.GetBySlugAsync(slug);
        return c is null ? NotFound() : Ok(c);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryUpsertDto dto)
        => Ok(await _svc.CreateAsync(dto));

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] CategoryUpsertDto dto)
    {
        var c = await _svc.UpdateAsync(id, dto);
        return c is null ? NotFound() : Ok(c);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => await _svc.DeleteAsync(id) ? NoContent() : NotFound();
}
