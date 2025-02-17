using AutoMapper;
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

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [HttpHead("{userId}")]
    [HttpGet("{userId}", Name = nameof(GetUserById))]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user == null) return NotFound();
        if (HttpMethods.IsHead(Request.Method))
            return Ok();
        var userDto = mapper.Map<UserDto>(user);
        return Ok(userDto);
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] UserToCreateDto? userDto)
    {
        if (userDto == null) return BadRequest();
        if (string.IsNullOrEmpty(userDto.Login))
            ModelState.AddModelError(nameof(UserToCreateDto.Login), "");
        else if (!userDto.Login.All(char.IsLetterOrDigit))
            ModelState.AddModelError(nameof(UserToCreateDto.Login), "Логин должен состоять только из цифр или букв");
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        var userEntity = mapper.Map<UserEntity>(userDto);
        var value = userRepository.Insert(userEntity);
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = userEntity.Id },
            value.Id);
    }

    [HttpPut("{userId}")]
    public IActionResult UpdateUser([FromRoute] Guid? userId, [FromBody] UserToUpdateDto? userDto)
    {
        if (userDto == null || userId == null) return BadRequest();
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        var userEntity = new UserEntity(userId.Value);
        mapper.Map(userDto, userEntity);
        userRepository.UpdateOrInsert(userEntity, out var isSuccess);

        if (isSuccess)
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = userEntity.Id },
                userEntity.Id);
        return NoContent();
    }

    [HttpPatch("{userId}")]
    public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<UserToUpdateDto>? patchDoc)
    {
        if (patchDoc == null) return BadRequest();

        var userEntity = userRepository.FindById(userId);
        if (userEntity == null) return NotFound();

        var updateDto = new UserToUpdateDto();
        patchDoc.ApplyTo(updateDto, ModelState);

        if (!TryValidateModel(updateDto))
            return UnprocessableEntity(ModelState);

        mapper.Map(updateDto, userEntity);
        userRepository.Update(userEntity);

        return NoContent();
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user == null) return NotFound();
        userRepository.Delete(userId);

        return NoContent();
    }

    [HttpGet(Name = nameof(GetUsers))]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), 200)]
    public ActionResult<IEnumerable<UserDto>> GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Min(Math.Max(pageSize, 1), 20);

        var page = userRepository.GetPage(pageNumber, pageSize);

        var paginationHeader = new
        {
            previousPageLink = page.HasPrevious
                ? CreateGetUsersUri(page.CurrentPage - 1, page.PageSize)
                : null,
            nextPageLink = page.HasNext
                ? CreateGetUsersUri(page.CurrentPage + 1, page.PageSize)
                : null,
            totalCount = page.TotalCount,
            pageSize = page.PageSize,
            currentPage = page.CurrentPage,
            totalPages = page.TotalPages
        };
        Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(paginationHeader));

        return Ok(page);
    }

    private string? CreateGetUsersUri(int pageNumber, int pageSize)
    {
        return linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers),
            new
            {
                pageNumber,
                pageSize
            });
    }
}