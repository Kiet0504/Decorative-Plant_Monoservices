// decorativeplant-be.API/Controllers/CompanyController.cs

using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Company.Commands;
using decorativeplant_be.Application.Features.Company.DTOs;
using decorativeplant_be.Application.Features.Company.Queries;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompanyController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<CompanyDto>>> GetAll()
    {
        try
        {
            var result = await _mediator.Send(new GetAllCompaniesQuery());
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id)
    {
        try
        {
            var result = await _mediator.Send(new GetCompanyByIdQuery { Id = id });
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<CompanyDto>> Create([FromBody] CreateCompanyCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, [FromBody] UpdateCompanyCommand command)
    {
        try
        {
            var updatedCommand = command with { Id = id };
            var result = await _mediator.Send(updatedCommand);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Deactivate(Guid id)
    {
        try
        {
            await _mediator.Send(new DeactivateCompanyCommand { Id = id });
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(Problem(detail: ex.Message, statusCode: 404));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Problem(detail: ex.Message, statusCode: 409));
        }
        catch (Exception)
        {
            return StatusCode(500, Problem("Unexpected error", statusCode: 500));
        }
    }
}
