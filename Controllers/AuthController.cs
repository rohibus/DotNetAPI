using System.Data;
using DotnetAPI.Data;
using DotnetAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DotnetAPI.Helpers;
using Dapper;
using AutoMapper;
using DotnetAPI.Models;

namespace DotnetAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly DataContextDapper _dapper;
        private readonly AuthHelper _authHelper;
        private readonly ReusableSql _reusableSql;
        private readonly IMapper _mapper;
        public AuthController(IConfiguration config)
        {
            _dapper = new DataContextDapper(config);
            _authHelper = new AuthHelper(config);
            _reusableSql = new ReusableSql(config);
            _mapper = new Mapper(new MapperConfiguration(cfg => 
                        {
                            cfg.CreateMap<UserForRegistrationDto, UserComplete>();
                        }
                    )
                );
        }

        [AllowAnonymous]
        [HttpPost("Register")]
        public IActionResult Register(UserForRegistrationDto userForRegistrationDto)
        {
            if (userForRegistrationDto.Password == userForRegistrationDto.PasswordConfirm)
            {
                string sqlCheckUserExist = @"SELECT [Email] FROM TutorialAppSchema.Auth WHERE Email = '" +
                                        userForRegistrationDto.Email + "'";
                
                IEnumerable<string> existingUsers = _dapper.LoadData<string>(sqlCheckUserExist);

                if (existingUsers.Count() == 0)
                {
                    UserForLoginDto userForLoginDto = new UserForLoginDto()
                                                        {
                                                            Email = userForRegistrationDto.Email,
                                                            Password = userForRegistrationDto.Password
                                                        };

                    if (_authHelper.SetPassword(userForLoginDto))
                    {
                        UserComplete user = _mapper.Map<UserComplete>(userForRegistrationDto);
                        user.Active = true;

                        if (_reusableSql.UpsertUser(user))
                        {
                            return Ok();
                        }
                        throw new Exception("Failed to add user!");
                    }
                    throw new Exception("User with this email already exists!");
                }
                throw new Exception("Failed to register user!");
            }
            throw new Exception("Password do not match!");
        }

        [HttpPut("ResetPassword")]
        public IActionResult ResetPassword(UserForLoginDto userForLoginDto)
        {
            if (_authHelper.SetPassword(userForLoginDto))
            {
                return Ok();
            }
            throw new Exception("Failed to update password!");
        }

        [AllowAnonymous]
        [HttpPost("Login")]
        public IActionResult Login(UserForLoginDto userForLoginDto)
        {
            string sqlForHashAndSalt = @"EXEC TutorialAppSchema.spLoginConfirmation_Get 
                                            @Email = @EmailParam";
            
            DynamicParameters sqlParameters = new DynamicParameters();
            sqlParameters.Add("@EmailParam", userForLoginDto.Email, DbType.String);

            UserForLoginConformationDto userForLoginConformationDto = _dapper
                        .LoadDataSingleWithParameter<UserForLoginConformationDto>(sqlForHashAndSalt, sqlParameters);
            
            byte[] passwordHash = _authHelper.GetPasswordHash(userForLoginDto.Password, userForLoginConformationDto.PasswordSalt);

            for (int i = 0; i < passwordHash.Length; i++)
            {
                if (passwordHash[i] != userForLoginConformationDto.PasswordHash[i])
                {
                    return StatusCode(401, "Incorect password!");
                }
            }

            string userIdSql = @"SELECT UserId from TutorialAppSchema.Users WHERE Email = '"
                + userForLoginDto.Email + "'";

            int userId = _dapper.LoadDataSingle<int>(userIdSql);

            return Ok(new Dictionary<string, string>{
                {"token", _authHelper.CreateToken(userId)}
            });
        }

        [HttpGet("RefreshToken")]
        public string RefreshToken()
        {
            string userIdSql = @"SELECT UserId from TutorialAppSchema.Users WHERE UserId = '"
                + User.FindFirst("userId")?.Value + "'";

            int userId = _dapper.LoadDataSingle<int>(userIdSql);

            return _authHelper.CreateToken(userId);
        }
    }
}