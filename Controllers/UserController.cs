using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Login.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

public class UserController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public UserController(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("/register")]
    public async Task<IActionResult> CreateUser([FromBody] User user)
    {
        if (user != null)
        {
            // Verifica se já existe um usuário com o mesmo email
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
            {
                return BadRequest($"O email '{user.Email}' já está sendo usado por outro usuário.");
            }

            // Verifica se outra conta já está usando a senha
            var existingUserWithPassword = await _dbContext.Users.FirstOrDefaultAsync(u => u.Password == user.Password);
            if (existingUserWithPassword != null)
            {
                return BadRequest($"A senha já está sendo usada por outro usuário. O email associado é '{existingUserWithPassword.Email}'.");
            }

            // Adiciona o novo usuário ao contexto
            _dbContext.Users.Add(user);

            // Salva as alterações no contexto
            await _dbContext.SaveChangesAsync();

            // Retorna a resposta de sucesso
            return Created($"/users/{user.Id}", user);
        }
        else
        {
            return BadRequest("Dados do usuário inválidos.");
        }
    }


    [HttpPost("/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email && u.Password == loginRequest.Password);

        if (user == null)
            return Unauthorized("Credenciais inválidas.");

        var token = GenerateJwtToken(user);
        return Ok(new { Token = token });
    }
    
    
    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"]);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [HttpPost("/claims")]
    public async Task<IActionResult> GetUserInfo([FromBody] TokenRequest tokenRequest)
        {
            if (tokenRequest == null || string.IsNullOrEmpty(tokenRequest.Token))
            {
                return BadRequest("Token inválido.");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"])),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };

            SecurityToken securityToken;
            ClaimsPrincipal principal;

            try
            {
                principal = tokenHandler.ValidateToken(tokenRequest.Token, tokenValidationParameters, out securityToken);
            }
            catch (Exception ex)
            {
                return BadRequest("Token inválido.");
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null || !int.TryParse(userId, out int userIdInt))
            {
                return BadRequest("ID do usuário inválido.");
            }

            var user = await _dbContext.Users.Include(u => u.Permissions).FirstOrDefaultAsync(u => u.Id == userIdInt);

            if (user == null)
            {
                return BadRequest("Usuário não encontrado no banco de dados.");
            }

            var name = user.Name;
            var email = user.Email;
            var permissions = user.Permissions?.Select(p => p.Name).ToList() ?? new List<string>();

            var userInfo = new
            {
                Name = name,
                Email = email,
                Permissions = permissions
            };

            return Ok(userInfo);
    }

}
