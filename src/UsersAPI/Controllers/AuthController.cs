
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UsersAPI.Data;
using UsersAPI.Events;
using UsersAPI.Models;
using UsersAPI.Services;
using BCrypt.Net;

namespace UsersAPI.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase {
        private readonly UsersDbContext _db;
        private readonly JwtTokenService _jwt;
        private readonly IPublishEndpoint _publish;
        public AuthController(UsersDbContext db, JwtTokenService jwt, IPublishEndpoint publish) {
            _db = db; _jwt = jwt; _publish = publish;
        }

        public record RegisterRequest(string Email, string Password);
        public record LoginRequest(string Email, string Password);
        public record LoginResponse(string Token);

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest req) {
            if (await _db.Users.AnyAsync(u => u.Email == req.Email))
                return Conflict(new { message = "Email já cadastrado" });
            var user = new User { Email = req.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password) };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await _publish.Publish(new UserCreatedEvent(user.Id, user.Email));
            return Created($"api/auth/users/{user.Id}", new { user.Id, user.Email });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req) {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message = "Credenciais inválidas" });
            var token = _jwt.GenerateToken(user.Id, user.Email);
            return Ok(new LoginResponse(token));
        }
    }
}
