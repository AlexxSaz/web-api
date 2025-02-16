﻿using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;

    private readonly IMapper mapper;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
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
        if (user == null) return NotFound();
        var userDto = mapper.Map<UserDto>(user);
        return Ok(userDto);
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
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
    [Produces("application/json", "application/xml")]
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
    [Produces("application/json", "application/xml")]
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
}