
using Microsoft.AspNetCore.Identity;

namespace UsersAPI.Models;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? Nome { get; set; }
    public bool Ativo { get; set; } = true;
}
