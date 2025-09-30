﻿using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/xml")]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;

    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user is null)
            return NotFound();

        if (!HttpMethods.IsHead(Request.Method))
            return Ok(mapper.Map<UserDto>(user));

        return Content(string.Empty, "application/json; charset=utf-8");
    }

    [HttpPost]
    public ActionResult<Guid> CreateUser([FromBody] UserCreateDto? user)
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
    public ActionResult UpdateUser([FromBody] UserUpdateDto? userDto, Guid userId)
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

    [HttpPatch("{userId}")]
    public ActionResult PatchUser([FromBody] JsonPatchDocument<UserUpdateDto>? patchDoc, Guid userId)
    {
        if (patchDoc is null)
            return BadRequest();

        var userEntity = userRepository.FindById(userId);
        if (userEntity is null)
            return NotFound();

        var updateDto = mapper.Map<UserUpdateDto>(userEntity);
        patchDoc.ApplyTo(updateDto, ModelState);

        if (!TryValidateModel(updateDto))
            return UnprocessableEntity(ModelState);

        userRepository.Update(mapper.Map(updateDto, userEntity));
        return NoContent();
    }

    [HttpDelete("{userId}")]
    public ActionResult DeleteUser(Guid userId)
    {
        if (userRepository.FindById(userId) is null)
            return NotFound();

        userRepository.Delete(userId);
        return NoContent();
    }

    [HttpGet(Name = nameof(GetUsers))]
    public ActionResult GetUsers([FromQuery] UsersGetDto dto)
    {
        var usersPage = userRepository.GetPage(dto.PageNumber, dto.PageSize);
        var paginationHeader = new
        {
            previousPageLink = usersPage.HasPrevious
                ? linkGenerator
                    .GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageNumber = usersPage.CurrentPage - 1, pageSize = usersPage.PageSize })
                : null,
            nextPageLink = usersPage.HasNext
                ? linkGenerator
                    .GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageNumber = usersPage.CurrentPage + 1, pageSize = usersPage.PageSize })
                : null,
            totalCount = usersPage.TotalCount,
            pageSize = usersPage.PageSize,
            currentPage = usersPage.CurrentPage,
            totalPages = usersPage.TotalPages,
        };

        Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        return Ok(mapper.Map<IEnumerable<UserDto>>(usersPage));
    }
}