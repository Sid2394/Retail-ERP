using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var jwtKey = cfg["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key is not configured (min. 32 characters). Set it with 'dotnet user-secrets set \"Jwt:Key\" \"<random>\"' or the Jwt__Key environment variable. See README.");

builder.Services.AddDbContext<ErpDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<NumberingService>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ProcurementService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<ManufacturingService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = cfg["Jwt:Issuer"], ValidateAudience = true, ValidAudience = cfg["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1), NameClaimType = "name", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization(o => { foreach (var p in Perms.All) o.AddPolicy(p.Code, pol => pol.RequireAuthenticatedUser().RequireClaim("perm", p.Code)); });
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" }).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    try { await next(); }
    catch (BusinessException ex) { ctx.Response.StatusCode = ex.Status; await ctx.Response.WriteAsJsonAsync(new { message = ex.Message }); }
    catch (DbUpdateConcurrencyException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { message = "The record was changed by another request. Reload and try again." }); }
    catch (DbUpdateException ex) { log.LogError(ex, "Database update failed"); ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { message = "The change violates a database constraint (duplicate or referenced record)." }); }
    catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Unhandled error"); ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." }); }
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(); else await db.Database.EnsureCreatedAsync();
    await Seeder.EnsureReferenceData(db);
    if (cfg.GetValue<bool>("Seed:Enabled"))
        await Seeder.SeedDemo(scope.ServiceProvider, cfg["Seed:DefaultPassword"]);
}
app.Run();

public partial class Program { }
