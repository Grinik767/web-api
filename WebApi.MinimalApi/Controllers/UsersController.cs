using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;

    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user is null)
            return NotFound();

        return Ok(mapper.Map<UserDto>(user));
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] UserCreateDto? user)
    {
        if (user is null)
            return BadRequest();

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        if (!user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Логин должен состоять только из букв и цифр");
            return UnprocessableEntity(ModelState);
        }

        var createdUserEntity = userRepository.Insert(mapper.Map<UserEntity>(user));

        return CreatedAtRoute(nameof(GetUserById), new { userId = createdUserEntity.Id }, createdUserEntity.Id);
    }

    [HttpPut("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult UpdateUser([FromBody] UserUpdateDto? userDto, Guid userId)
    {
        if (userDto is null || Guid.Empty == userId)
            return BadRequest();

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        var userEntity = userRepository.FindById(userId) ?? new UserEntity(userId);
        userEntity = mapper.Map(userDto, userEntity);

        userRepository.UpdateOrInsert(userEntity, out var isInserted);
        if (isInserted)
            return CreatedAtRoute(nameof(GetUserById), new { userId = userEntity.Id }, userEntity.Id);

        return NoContent();
    }
}