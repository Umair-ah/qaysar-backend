using Microsoft.AspNetCore.Mvc;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _svc;
    public AuthController(IAuthService svc) => _svc = svc;

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var res = await _svc.LoginAsync(req);
        if (res is null) return Unauthorized(new { message = "Invalid credentials" });
        return Ok(res);
    }
}
