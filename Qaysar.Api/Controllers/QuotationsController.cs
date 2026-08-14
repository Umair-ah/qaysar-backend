using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Controllers;

[ApiController]
[Route("api/quotations")]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationService _svc;
    private readonly IQuotationPdfService _pdfSvc;
    public QuotationsController(IQuotationService svc, IQuotationPdfService pdfSvc)
    {
        _svc = svc;
        _pdfSvc = pdfSvc;
    }

    /// <summary>
    /// Public — submitted by customers from the "Request a Quotation" form.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<QuotationDetailDto>> Create([FromBody] QuotationCreateDto dto)
    {
        try
        {
            var q = await _svc.CreateAsync(dto);
            return Ok(q);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ---- Admin ----

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResult<QuotationListItemDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var res = await _svc.GetPagedAsync(page, pageSize);
        return Ok(res);
    }

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuotationDetailDto>> Get(int id)
    {
        var q = await _svc.GetByIdAsync(id);
        return q is null ? NotFound() : Ok(q);
    }

    [Authorize]
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<QuotationDetailDto>> UpdateStatus(int id, [FromBody] QuotationStatusUpdateDto dto)
    {
        try
        {
            var q = await _svc.UpdateStatusAsync(id, dto.Status);
            return q is null ? NotFound() : Ok(q);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id:int}/prices")]
    public async Task<ActionResult<QuotationDetailDto>> UpdatePrices(int id, [FromBody] QuotationPricesUpdateDto dto)
    {
        var q = await _svc.UpdatePricesAsync(id, dto);
        return q is null ? NotFound() : Ok(q);
    }

    [Authorize]
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        var bytes = await _pdfSvc.GenerateAsync(id, HttpContext.RequestAborted);
        return bytes is null ? NotFound() : File(bytes, "application/pdf", $"Quotation-{id:D5}.pdf");
    }
}
