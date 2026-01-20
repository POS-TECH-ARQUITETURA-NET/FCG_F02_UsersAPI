
//using MassTransit;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.IdentityModel.Tokens;
//using Microsoft.OpenApi.Models;
//using System.Text;
//using UsersAPI.Data;
//using UsersAPI.Services;

//var builder = WebApplication.CreateBuilder(args);

//// Configs
//var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq";
//var rabbitUser = builder.Configuration["RabbitMQ:User"] ?? "guest";
//var rabbitPass = builder.Configuration["RabbitMQ:Pass"] ?? "guest";
//var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev_secret_change_me";

//// DB
//var conn = builder.Configuration.GetConnectionString("UsersDb") ??
//           "Host=users-db;Database=users;Username=postgres;Password=postgres";
//builder.Services.AddDbContext<UsersDbContext>(opt => opt.UseNpgsql(conn));

//// MassTransit - only publishing here
//builder.Services.AddMassTransit(x => {
//    x.UsingRabbitMq((context, cfg) => {
//        cfg.Host(rabbitHost, "/", h => {
//            h.Username(rabbitUser);
//            h.Password(rabbitPass);
//        });
//    });
//});

//// JWT
//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options => {
//        options.TokenValidationParameters = new TokenValidationParameters {
//            ValidateIssuerSigningKey = true,
//            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
//            ValidateIssuer = false,
//            ValidateAudience = false,
//            ClockSkew = TimeSpan.Zero
//        };
//    });

//builder.Services.AddAuthorization();
//builder.Services.AddScoped<JwtTokenService>();

//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(c => {
//    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UsersAPI", Version = "v1" });
//});

//var app = builder.Build();

//app.UseSwagger();
//app.UseSwaggerUI();
//app.UseAuthentication();
//app.UseAuthorization();
//app.MapControllers();

//app.Run();


using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UsersAPI.Data;
using UsersAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// SQL Server
var conn = builder.Configuration.GetConnectionString("UsersDb")
    ?? "Server=sqlserver;Database=usersdb;User Id=sa;Password=Strong!Passw0rd;TrustServerCertificate=True";
builder.Services.AddDbContext<UsersDbContext>(o => o.UseSqlServer(conn));

// Identity (GUID keys)
builder.Services.AddIdentityCore<ApplicationUser>(opt =>
{
    opt.Password.RequiredLength = 6;
    opt.Password.RequireDigit = true;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireLowercase = true;
    opt.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<UsersDbContext>()
.AddSignInManager();

// JWT
var issuer = builder.Configuration["Jwt:Issuer"] ?? "FCG_API";
var audience = builder.Configuration["Jwt:Audience"] ?? "FCG_Cliente";
var key = builder.Configuration["Jwt:Key"] ?? "CHANGE_ME";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

// MassTransit/RabbitMQ (publishers)
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq";
var rabbitUser = builder.Configuration["RabbitMQ:User"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Pass"] ?? "guest";
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h => { h.Username(rabbitUser); h.Password(rabbitPass); });
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UsersAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id="Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await db.Database.MigrateAsync();

    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    foreach (var r in new[] { "Admin", "User" })
        if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new IdentityRole<Guid>(r));

    var adminEmail = "admin@local";
    var admin = await userMgr.FindByEmailAsync(adminEmail);
    if (admin is null)
    {
        admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, Nome = "Administrador", Ativo = true };
        if ((await userMgr.CreateAsync(admin, "Admin@123")).Succeeded)
            await userMgr.AddToRoleAsync(admin, "Admin");
    }
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
