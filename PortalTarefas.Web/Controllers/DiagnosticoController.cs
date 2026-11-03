using Microsoft.AspNetCore.Mvc;
namespace PortalTarefas.Web.Controllers;
[ApiController,Route("diagnostico/mvc")]
public class DiagnosticoController:ControllerBase { [HttpGet] public object Get()=>new{aplicacao="PortalTarefas",versao=1}; }
