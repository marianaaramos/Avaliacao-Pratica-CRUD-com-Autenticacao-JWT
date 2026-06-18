using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=salas.db"));

var chaveSecreta = "SuaChaveSuperSecretaComPeloMenos32Caracteres123!";
var key = Encoding.ASCII.GetBytes(chaveSecreta);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTudo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("PermitirTudo");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}


app.MapPost("/login", (LoginModel login) =>
{
    if (login.Email == "admin@email.com" && login.Senha == "123456")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, login.Email) }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Results.Ok(new { token = tokenHandler.WriteToken(token) });
    }

    return Results.Unauthorized();
});

app.MapGet("/salas", [Authorize] async (AppDbContext db) => 
    Results.Ok(await db.SalasReuniao.ToListAsync()));

app.MapPost("/salas", [Authorize] async (SalaReuniao sala, AppDbContext db) =>
{
    db.SalasReuniao.Add(sala);
    await db.SaveChangesAsync();
    return Results.Created($"/salas/{sala.Id}", sala);
});

app.MapPut("/salas/{id}", [Authorize] async (int id, SalaReuniao salaAtualizada, AppDbContext db) =>
{
    var sala = await db.SalasReuniao.FindAsync(id);
    if (sala == null) return Results.NotFound();

    sala.Nome = salaAtualizada.Nome;
    sala.Capacidade = salaAtualizada.Capacidade;
    sala.PossuiProjetor = salaAtualizada.PossuiProjetor;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/salas/{id}", [Authorize] async (int id, AppDbContext db) =>
{
    var sala = await db.SalasReuniao.FindAsync(id);
    if (sala == null) return Results.NotFound();

    db.SalasReuniao.Remove(sala);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run("http://localhost:5000");

public class LoginModel 
{ 
    public string Email { get; set; } = string.Empty; 
    public string Senha { get; set; } = string.Empty; 
}

public class SalaReuniao
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Capacidade { get; set; }
    public bool PossuiProjetor { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<SalaReuniao> SalasReuniao => Set<SalaReuniao>();
}