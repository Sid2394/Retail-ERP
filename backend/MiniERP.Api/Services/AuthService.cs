using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MiniERP.Api.Services;

public static class PasswordHasher
{
    const int Iterations = 120_000;
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }
    public static bool Verify(string password, string stored)
    {
        var p = stored.Split('.');
        if (p.Length != 4 || p[0] != "v1") return false;
        var salt = Convert.FromBase64String(p[2]); var expected = Convert.FromBase64String(p[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, int.Parse(p[1]), HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public class AuthService(ErpDbContext db, IConfiguration cfg)
{
    public async Task<LoginResponse> Login(LoginRequest r)
    {
        var u = await db.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(x => x.Username == r.Username);
        if (u == null || !u.IsActive || !PasswordHasher.Verify(r.Password ?? "", u.PasswordHash)) throw new BusinessException("Invalid username or password.", 401);
        var perms = u.Role!.RolePermissions.Select(rp => rp.Permission!.Code).OrderBy(x => x).ToList();
        return new LoginResponse { Token = CreateToken(u, perms), User = new AuthUser { Id = u.Id, Username = u.Username, FullName = u.FullName, Role = u.Role.Name, Permissions = perms } };
    }
    string CreateToken(User u, List<string> perms)
    {
        var key = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var claims = new List<Claim> { new("uid", u.Id.ToString()), new("name", u.Username), new("fullName", u.FullName), new("role", u.Role!.Name) };
        claims.AddRange(perms.Select(p => new Claim("perm", p)));
        var desc = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims), Issuer = cfg["Jwt:Issuer"], Audience = cfg["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddMinutes(cfg.GetValue("Jwt:ExpiryMinutes", 480)),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(desc);
    }

    // ----- users -----
    static UserDto ToDto(User u) => new() { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email, RoleId = u.RoleId, RoleName = u.Role?.Name, IsActive = u.IsActive };
    public IQueryable<User> Users => db.Users.Include(u => u.Role);
    public async Task<PagedResult<UserDto>> ListUsers(PageQuery q)
    {
        var x = Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search)) x = x.Where(u => u.Username.Contains(q.Search) || u.FullName.Contains(q.Search));
        var total = await x.CountAsync();
        var items = await x.OrderBy(u => u.Username).Skip((Math.Max(q.Page, 1) - 1) * q.PageSize).Take(q.PageSize).ToListAsync();
        return new PagedResult<UserDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = q.Page, PageSize = q.PageSize };
    }
    public async Task<UserDto> SaveUser(int? id, UserDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Username) || string.IsNullOrWhiteSpace(d.FullName)) throw new BusinessException("Username and full name are required.");
        if (!await db.Roles.AnyAsync(r => r.Id == d.RoleId)) throw new BusinessException("A valid role is required.");
        if (await db.Users.AnyAsync(u => u.Username == d.Username && u.Id != (id ?? 0))) throw new BusinessException("Username already exists.", 409);
        User u;
        if (id == null)
        {
            if (string.IsNullOrWhiteSpace(d.Password) || d.Password.Length < 8) throw new BusinessException("Password must be at least 8 characters.");
            u = new User { PasswordHash = PasswordHasher.Hash(d.Password) }; db.Users.Add(u);
        }
        else
        {
            u = await db.Users.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("User not found.", 404);
            if (!string.IsNullOrWhiteSpace(d.Password)) { if (d.Password.Length < 8) throw new BusinessException("Password must be at least 8 characters."); u.PasswordHash = PasswordHasher.Hash(d.Password); }
        }
        u.Username = d.Username.Trim(); u.FullName = d.FullName.Trim(); u.Email = d.Email; u.RoleId = d.RoleId; u.IsActive = d.IsActive;
        await db.SaveChangesAsync();
        return ToDto(await Users.AsNoTracking().FirstAsync(x => x.Id == u.Id));
    }
    public async Task DeleteUser(int id, int? currentUserId)
    {
        if (id == currentUserId) throw new BusinessException("You cannot delete your own account.");
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("User not found.", 404);
        u.IsDeleted = true; u.IsActive = false; await db.SaveChangesAsync();
    }

    // ----- roles -----
    public async Task<List<RoleDto>> Roles() =>
        (await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).AsNoTracking().OrderBy(r => r.Name).ToListAsync())
        .Select(r => new RoleDto { Id = r.Id, Name = r.Name, Description = r.Description, Permissions = r.RolePermissions.Select(p => p.Permission!.Code).OrderBy(x => x).ToList() }).ToList();
    public async Task<RoleDto> SaveRole(int? id, RoleDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Name)) throw new BusinessException("Role name is required.");
        if (await db.Roles.AnyAsync(r => r.Name == d.Name && r.Id != (id ?? 0))) throw new BusinessException("Role name already exists.", 409);
        Role r;
        if (id == null) { r = new Role(); db.Roles.Add(r); }
        else { r = await db.Roles.Include(x => x.RolePermissions).FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("Role not found.", 404); db.RemoveRange(r.RolePermissions.ToList()); }
        r.Name = d.Name.Trim(); r.Description = d.Description;
        var perms = await db.Permissions.Where(p => d.Permissions.Contains(p.Code)).ToListAsync();
        r.RolePermissions = perms.Select(p => new RolePermission { Permission = p }).ToList();
        await db.SaveChangesAsync();
        return (await Roles()).First(x => x.Id == r.Id);
    }
    public async Task DeleteRole(int id)
    {
        var r = await db.Roles.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("Role not found.", 404);
        if (await db.Users.AnyAsync(u => u.RoleId == id)) throw new BusinessException("This role is assigned to users.");
        db.Roles.Remove(r); await db.SaveChangesAsync();
    }
}
