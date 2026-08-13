using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Qaysar.Api.Configuration;
using Qaysar.Api.Data;
using Qaysar.Api.Services;
using Qaysar.Api.Services.Interfaces;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Bind config from env
builder.Configuration.AddEnvironmentVariables();

var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=QaysarDb;Username=postgres;Password=umair";

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS!!";
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "qaysar-api";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "qaysar-admin";

builder.Services.Configure<JwtOptions>(o =>
{
    o.Key = jwtKey;
    o.Issuer = jwtIssuer;
    o.Audience = jwtAudience;
});
builder.Services.Configure<R2Options>(o =>
{
    o.AccountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "";
    o.AccessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY") ?? "";
    o.SecretKey = Environment.GetEnvironmentVariable("R2_SECRET_KEY") ?? "";
    o.Bucket = Environment.GetEnvironmentVariable("R2_BUCKET") ?? "";
    o.PublicBaseUrl = Environment.GetEnvironmentVariable("R2_PUBLIC_BASE_URL") ?? "";
});

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<IStorageService, R2StorageService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false, // never expire until logout (client-side clears token)
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Qaysar API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Auto-migrate + seed admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch
    {
        db.Database.EnsureCreated();
    }
    await DbSeeder.SeedAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
