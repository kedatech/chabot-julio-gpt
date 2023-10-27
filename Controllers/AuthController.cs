﻿using ESFE.Chatbot.Models;
using ESFE.Chatbot.Models.DTOs;
using ESFE.Chatbot.Schemas;
using ESFE.Chatbot.Services.Interfaces;
using ESFE.Chatbot.Services.Statics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESFE.Chatbot;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
  private readonly IWebHostEnvironment _webHostEnvironment;
  private readonly IClienteUserService _userService;
  private readonly IEmailService _emailService;
  private readonly IAuthService _authService;
  public AuthController(IClienteUserService userService, IWebHostEnvironment webHostEnvironment, IEmailService emailService, IAuthService authService)
  {
    _userService = userService;
    _webHostEnvironment = webHostEnvironment;
    _emailService = emailService;
    _authService = authService;
  }


  [HttpPost]
  [Route("login")]
  public async Task<IActionResult> Login([FromBody] AuthRequest auth)
  {
    auth.Password = UtilsService.ConvertSHA256(auth.Password);
    var result = await _authService.GetToken(auth);
    if(result == null)
    {
      return Unauthorized();
    }
    return Ok(result);
  }

  [Authorize]
  [HttpPost]
  [Route("ping")]
  public async Task<IActionResult> Ping()
  {
    return Ok(new { pong = "pong"});
  }
  [HttpPost]
  [Route("register")]
  public async Task<IActionResult> Reg([FromBody] CreateClientUser usuario)
  {
    if (await _userService.GetByEmail(usuario.Email) == null)
    {
      string typeUser = UtilsService.ValidGmail(usuario.Email) ? "esfe-user" : "no-esfe-user";

      ClientUser nuevo = new ClientUser()
      {
        Email = usuario.Email,
        Password = UtilsService.ConvertSHA256(usuario.Password),
        FirstName = usuario.FirstName,
        LastName = usuario.LastName,
        RestartAccount = false,
        ConfirmAccount = false,
        Token = UtilsService.RandomCode(),
        TypeUserId = typeUser

      };

      bool respuesta = await _userService.Create(nuevo);

      if (respuesta)
      {
        string content = this.GetFileContent("Templates/VerifyEmail.html");
        content = content.Replace("{{FirstName}}", usuario.FirstName);
        content = content.Replace("{{LastName}}", usuario.LastName);
        content = content.Replace("{{Token}}", nuevo.Token);

        // string htmlBody = string.Format(content, $"{usuario.FirstName} {usuario.LastName}", nuevo.Token);

        EmailDTO EmailDTO = new EmailDTO()
        {
          To = usuario.Email,
          Subject = "Correo confirmación",
          Content = content
        };

        bool enviado = _emailService.SendEmail(EmailDTO);
        if (enviado == true)
        {
          return Ok(Res.Provider($"Su cuenta ha sido creada. Hemos enviado un mensaje al correo {usuario.Email} para confirmar su cuenta", "Operación exitosa", true));
        }
        else
        {
          return BadRequest(Res.Provider($"Su cuenta no se pudo crear", "Verifique su gmail", true));
        }
      }
      else
      {
        return BadRequest(Res.Provider(new { }, "No se pudo crear su cuenta", false));
      }
    }
    else
    {
      return BadRequest(Res.Provider(new { }, $"Ya existe un usuario registrado con {usuario.Email}", false));
    }
  }

  [HttpGet]
  [Route("verify")]
  public async Task<IActionResult> Confirm(string token, int userId)
  {
    bool result = await _userService.ConfirmAccount(token, userId);
    if (result == true)
    {
      return Ok(Res.Provider("Cuenta confirmada", "Ya puede aplicar a ofertas", true));
    }
    else
    {
      return BadRequest(Res.Provider("Error al confirmar cuenta", "Error", false));
    }
  }


  private string GetFileContent(string filePath)
  {
    string basePath = _webHostEnvironment.ContentRootPath;
    string fullPath = Path.Combine(basePath, filePath);

    if (System.IO.File.Exists(fullPath))
    {
      return System.IO.File.ReadAllText(fullPath);
    }

    throw new FileNotFoundException("El archivo no existe", fullPath);
  }
}
