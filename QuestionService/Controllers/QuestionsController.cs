using FastExpressionCompiler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.Services;
using System.Security.Claims;
using Wolverine;

namespace QuestionService.Controllers;

[Route("[controller]")]
[ApiController]
public class QuestionsController(QuestionDbContext context, IMessageBus bus, TagService tagService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestions(CreateQuestionDto dto)
    {
        if (!await tagService.AreTagsValidAsync(dto.Tags))
            return BadRequest("Invalid tags");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.FindFirstValue("name");
        if (userId is null || userName is null)
        {
            return BadRequest("User information is missing.");
        }

        var question = new Question
        {
            Title = dto.Title,
            Content = dto.Content,
            TagSlugs = dto.Tags,
            AskerId = userId,
            AskerDisplayName = userName,
        };

        context.Questions.Add(question);
        await context.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.QuestionCreated(
            question.Id,
            question.Title,
            question.Content,
            question.CreatedAt,
            question.TagSlugs
        ));

        return Created($"/api/questions/{question.Id}", question);
        //return CreatedAtAction(nameof(GetQuestions), new { id = question.Id }, question);
    }
    [HttpGet]
    public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
    {
        var query = context.Questions.AsQueryable();

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(x => x.TagSlugs.Contains(tag));
        }
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await context.Questions.FindAsync(id);
        if (question is null) return NotFound();

        await context.Questions.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setter => setter.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));

        return question;
    }
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var question = await context.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (question.AskerId != userId)
        {
            return Forbid();
        }

        if (!await tagService.AreTagsValidAsync(dto.Tags))
            return BadRequest("Invalid tags");

        question.Title = dto.Title;
        question.Content = dto.Content;
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.QuestionUpdated(
            question.Id,
            question.Title,
            question.Content,
            question.TagSlugs.AsArray()
        ));

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteQuestion(string id)
    {
        var question = await context.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (question.AskerId != userId)
        {
            return Forbid();
        }
        context.Questions.Remove(question);
        await context.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.QuestionDeleted(id));
        return NoContent();
    }
}
