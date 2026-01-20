
//using Microsoft.EntityFrameworkCore;
//using UsersAPI.Models;

//namespace UsersAPI.Data {
//    public class UsersDbContext : DbContext {
//        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) {}
//        public DbSet<User> Users => Set<User>();
//    }
//}


using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UsersAPI.Models;

namespace UsersAPI.Data;

public sealed class UsersDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }
}
