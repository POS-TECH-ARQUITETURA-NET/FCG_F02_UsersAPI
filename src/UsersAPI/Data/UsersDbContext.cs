
using Microsoft.EntityFrameworkCore;
using UsersAPI.Models;

namespace UsersAPI.Data {
    public class UsersDbContext : DbContext {
        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) {}
        public DbSet<User> Users => Set<User>();
    }
}
