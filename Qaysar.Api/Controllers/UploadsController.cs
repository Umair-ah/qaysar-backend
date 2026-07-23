using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IStorageService _storage;
    public UploadsController(IStorageService storage) => _storage = storage;

    /// <summary>
    /// Uploads a single image to Cloudflare R2 and returns the public URL.
    /// Once R2 env vars are configured, this endpoint is ready to use.
    /// </summary>
    [HttpPost("image")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<UploadResponse>> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        try
        {
            using var stream = file.OpenReadStream();
            var url = await _storage.UploadAsync(stream, file.FileName, file.ContentType);
            return Ok(new UploadResponse(url));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
    }
}
