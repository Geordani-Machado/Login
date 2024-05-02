using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Login.Models;
using BCrypt.Net;

public class UserController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public UserController(AppDbContext dbContext, IConfiguration configuration, JwtService jwtService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _jwtService = jwtService;
    }

    [HttpPost("/register")]
    public async Task<IActionResult> CreateUser([FromBody] User user)
    {
        if (user != null)
        {
            // Verifica se o email e o email de confirmação correspondem
            if (user.Email != user.EmailConfirmed)
            {
                return BadRequest("O email e o email de confirmação não correspondem.");
            }

            // Verifica se já existe um usuário com o mesmo email
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingUser != null)
            {
                return BadRequest($"O email '{user.Email}' já está sendo usado por outro usuário.");
            }

            // Criptografa a senha antes de salvar
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);

            // Substitui a senha do usuário pela versão criptografada
            user.Password = hashedPassword;

            // Adiciona o novo usuário ao contexto
            _dbContext.Users.Add(user);

            // Salva as alterações no contexto
            await _dbContext.SaveChangesAsync(); // Aqui está a chamada para salvar as alterações

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
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

        if (user == null)
            return Unauthorized("Credenciais inválidas.");

        // Verifica se a senha fornecida corresponde à senha armazenada após descriptografar
        if (!BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password))
            return Unauthorized("Credenciais inválidas.");

        var token = _jwtService.GenerateJwtToken(user);
        return Ok(new { Token = token });
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
