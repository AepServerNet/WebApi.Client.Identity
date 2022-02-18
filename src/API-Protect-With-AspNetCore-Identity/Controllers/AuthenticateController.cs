using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API_Protect_With_AspNetCore_Identity.Models.Entities;
using API_Protect_With_AspNetCore_Identity.Models.InputModels;
using API_Protect_With_AspNetCore_Identity.Models.Options;
using API_Protect_With_AspNetCore_Identity.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SequentialGuid;

namespace API_Protect_With_AspNetCore_Identity.Controllers
{
    [Route("api/[controller]")]  
    [ApiController]  
    public class AuthenticateController : ControllerBase  
    {  
        private readonly UserManager<ApplicationUser> userManager;  
        private readonly RoleManager<IdentityRole> roleManager;  
        private readonly IConfiguration configuration;  
        private readonly IOptionsMonitor<JwtOptions> jwtOptionsMonitor;
  
        public AuthenticateController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, 
                                      IConfiguration configuration, IOptionsMonitor<JwtOptions> jwtOptionsMonitor)  
        {  
            this.userManager = userManager;  
            this.roleManager = roleManager;  
            this.configuration = configuration;
            this.jwtOptionsMonitor = jwtOptionsMonitor;  
        }  
  
        [HttpPost]  
        [Route("login")]  
        public async Task<IActionResult> Login([FromBody] LoginModel model)  
        {  
            var options = this.jwtOptionsMonitor.CurrentValue;
            var user = await userManager.FindByNameAsync(model.Username);  

            if (user != null && await userManager.CheckPasswordAsync(user, model.Password))  
            {  
                var userRoles = await userManager.GetRolesAsync(user);
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, SequentialGuidGenerator.Instance.NewGuid().ToString()),
                }.Union(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

                var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
                var token = new JwtSecurityToken(
                    issuer: options.ValidIssuer,
                    audience: options.ValidAudience,
                    expires: DateTime.Now.AddDays(options.Expires),
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    expiration = token.ValidTo
                });
            }  
            return Unauthorized();  
        }  
  
        [HttpPost]  
        [Route("register")]  
        public async Task<IActionResult> Register([FromBody] RegisterModel model)  
        {  
            var userExists = await userManager.FindByNameAsync(model.Username);

            if (userExists != null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new Response
                {
                    Status = "Error", Message = "User already exists!"
                });
            }
  
            ApplicationUser user = new ApplicationUser()  
            {  
                Email = model.Email,  
                SecurityStamp = SequentialGuidGenerator.Instance.NewGuid().ToString(),  
                UserName = model.Username  
            };  

            var result = await userManager.CreateAsync(user, model.Password);  

            if (!result.Succeeded)  
                return StatusCode(StatusCodes.Status500InternalServerError, new Response 
                { 
                    Status = "Error", Message = "User creation failed! Please check user details and try again." 
                });  
  
            return Ok(new Response 
            { 
                Status = "Success", Message = "User created successfully!" 
            });  
        }  
  
        [HttpPost]  
        [Route("register-admin")]  
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterModel model)  
        {  
            var userExists = await userManager.FindByNameAsync(model.Username);  

            if (userExists != null)  
                return StatusCode(StatusCodes.Status500InternalServerError, new Response 
                { 
                    Status = "Error", Message = "User already exists!" 
                });  
  
            ApplicationUser user = new ApplicationUser()  
            {  
                Email = model.Email,  
                SecurityStamp = SequentialGuidGenerator.Instance.NewGuid().ToString(),  
                UserName = model.Username  
            }; 

            var result = await userManager.CreateAsync(user, model.Password);  
            
            if (!result.Succeeded)  
                return StatusCode(StatusCodes.Status500InternalServerError, new Response 
                { 
                    Status = "Error", Message = "User creation failed! Please check user details and try again." 
                });  
  
            if (!await roleManager.RoleExistsAsync(UserRoles.Admin.ToString()))  
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin.ToString())); 

            if (!await roleManager.RoleExistsAsync(UserRoles.User.ToString()))  
                await roleManager.CreateAsync(new IdentityRole(UserRoles.User.ToString()));  
  
            if (await roleManager.RoleExistsAsync(UserRoles.Admin.ToString()))  
            {  
                await userManager.AddToRoleAsync(user, UserRoles.Admin.ToString());  
            }  
  
            return Ok(new Response 
            { 
                Status = "Success", Message = "User created successfully!" 
            });  
        }  
    }
}