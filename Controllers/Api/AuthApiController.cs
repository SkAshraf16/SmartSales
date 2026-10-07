using SmartSales.Models;
using SmartSales.Services;
using SmartSales.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SmartSales.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;

    public AuthApiController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, AuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _audit = audit;
    }

    // login api sets the same auth cookie, max 5 tries per minute
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user == null || !user.IsActive)
        {
            await _audit.LogAsync("FailedLogin", "Api", result: "Failure", userName: req.Email);
            return Unauthorized(new ApiError(401, "Invalid credentials."));
        }

        var res = await _signIn.PasswordSignInAsync(user, req.Password, false, lockoutOnFailure: true);
        if (!res.Succeeded)
        {
            await _audit.LogAsync(res.IsLockedOut ? "Lockout" : "FailedLogin", "Api", user.Id, result: "Failure", userId: user.Id, userName: user.Email);
            return Unauthorized(new ApiError(401, res.IsLockedOut ? "Account locked. Try after 15 minutes." : "Invalid credentials."));
        }

        await _audit.LogAsync("Login", "Api", user.Id, userId: user.Id, userName: user.Email);
        var role = (await _users.GetRolesAsync(user)).FirstOrDefault();
        return Ok(new { user.Email, user.FullName, Role = role });
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", "Api", User.UserId());
        await _signIn.SignOutAsync();
        return Ok(new { message = "Logged out." });
    }
}
