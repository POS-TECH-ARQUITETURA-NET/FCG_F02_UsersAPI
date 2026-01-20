
//using MassTransit;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using UsersAPI.Data;
//using UsersAPI.Events;
//using UsersAPI.Models;
//using UsersAPI.Services;
//using BCrypt.Net;

//namespace UsersAPI.Controllers {
//    [ApiController]
//    [Route("api/[controller]")]
//    public class AuthController : ControllerBase {
//        private readonly UsersDbContext _db;
//        private readonly JwtTokenService _jwt;
//        private readonly IPublishEndpoint _publish;
//        public AuthController(UsersDbContext db, JwtTokenService jwt, IPublishEndpoint publish) {
//            _db = db; _jwt = jwt; _publish = publish;
//        }

//        public record RegisterRequest(string Email, string Password);
//        public record LoginRequest(string Email, string Password);
//        public record LoginResponse(string Token);

//        [HttpPost("register")]
//        public async Task<IActionResult> Register(RegisterRequest req) {
//            if (await _db.Users.AnyAsync(u => u.Email == req.Email))
//                return Conflict(new { message = "Email já cadastrado" });
//            var user = new User { Email = req.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password) };
//            _db.Users.Add(user);
//            await _db.SaveChangesAsync();
//            await _publish.Publish(new UserCreatedEvent(user.Id, user.Email));
//            return Created($"api/auth/users/{user.Id}", new { user.Id, user.Email });
//        }

//        [HttpPost("login")]
//        public async Task<IActionResult> Login(LoginRequest req) {
//            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
//            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
//                return Unauthorized(new { message = "Credenciais inválidas" });
//            var token = _jwt.GenerateToken(user.Id, user.Email);
//            return Ok(new LoginResponse(token));
//        }
//    }
//}


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UsersAPI.Models;

namespace UsersAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly SignInManager<ApplicationUser> _signInMgr;
    private readonly IConfiguration _cfg;
    private readonly IPublishEndpoint _publish;

    public AuthController(UserManager<ApplicationUser> userMgr, SignInManager<ApplicationUser> signInMgr, IConfiguration cfg, IPublishEndpoint publish)
    { _userMgr = userMgr; _signInMgr = signInMgr; _cfg = cfg; _publish = publish; }

    public record RegisterRequest(string Nome, string Email, string Senha);
    public record LoginRequest(string Email, string Senha);
    public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string Email, string[] Roles);

    [HttpPost("register"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, EmailConfirmed = true, Nome = req.Nome, Ativo = true };
        var result = await _userMgr.CreateAsync(user, req.Senha);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userMgr.AddToRoleAsync(user, "User");
        await _publish.Publish(new UsersAPI.Events.UserCreatedEvent(user.Id, user.Email!, user.Nome, user.Ativo), ct);

        return Ok(await IssueToken(user));
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _userMgr.FindByEmailAsync(req.Email);
        if (user is null || !user.Ativo) return Unauthorized(new { message = "Credenciais inválidas" });

        var signIn = await _signInMgr.CheckPasswordSignInAsync(user, req.Senha, false);
        if (!signIn.Succeeded) return Unauthorized(new { message = "Credenciais inválidas" });

        return Ok(await IssueToken(user));
    }

    private async Task<AuthResponse> IssueToken(ApplicationUser user)
    {
        var roles = (await _userMgr.GetRolesAsync(user)).ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.Email ?? ""),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var issuer = _cfg["Jwt:Issuer"]!;
        var audience = _cfg["Jwt:Audience"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(double.Parse(_cfg["Jwt:ExpiresMinutes"] ?? "120"));
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: creds);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Email ?? "", roles);
    }
}
