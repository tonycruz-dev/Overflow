﻿using Common;
using Contracts;
using FastExpressionCompiler;
using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.RequestHelpers;
using QuestionService.Services;
using Reputation;
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
        var name = User.FindFirstValue("name");
        
        if (userId is null || name is null) return BadRequest("Cannot get user details");

        var sanitizer = new HtmlSanitizer();

        var question = new Question
        {
            Title = dto.Title,
            Content = sanitizer.Sanitize(dto.Content),
            TagSlugs = dto.Tags,
            AskerId = userId
        };
        
        await using var tx = await context.Database.BeginTransactionAsync();

        try
        {
            await context.Questions.AddAsync(question);
            
            await context.SaveChangesAsync();
        
            await bus.PublishAsync(new QuestionCreated(question.Id, question.Title, question.Content, 
                question.CreatedAt, question.TagSlugs));
            
            await tx.CommitAsync();
        }
        catch (Exception e)
        {
            await tx.RollbackAsync();
            Console.WriteLine(e);
            throw;
        }
        

        
        var slugs = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (slugs.Length > 0)
        {
            await context.Tags
                .Where(t => slugs.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount, 
                    t => t.UsageCount + 1)); 
        }
        
        return Created($"/questions/{question.Id}", question);
    }
    [HttpGet]
    public async Task<ActionResult<PaginationResult<Question>>> GetQuestions([FromQuery] QuestionsQuery q)
    {
        var query = context.Questions.AsQueryable();

        if (!string.IsNullOrEmpty(q.Tag))
        {
            query = query.Where(x => x.TagSlugs.Contains(q.Tag));
        }

        query = q.Sort switch
        {
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            "active" => query.OrderByDescending(x => new[]
            {
                x.CreatedAt,
                x.UpdatedAt ?? DateTime.MinValue,
                x.Answers.Max(a => (DateTime?)a.CreatedAt) ?? DateTime.MinValue,
                x.Answers.Max(a => a.UpdatedAt) ?? DateTime.MinValue,
            }.Max()),
            "unanswered" => query.Where(x => x.AnswerCount == 0)
                .OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var result = await query.ToPaginatedListAsync(q);

        return result;
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await context.Questions
           .Include(x => x.Answers)
           .FirstOrDefaultAsync(x => x.Id == id);
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

        var original = question.TagSlugs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var incoming = dto.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var removed = original.Except(incoming, StringComparer.OrdinalIgnoreCase).ToArray();
        var added = incoming.Except(original, StringComparer.OrdinalIgnoreCase).ToArray();


        var sanitizer = new HtmlSanitizer();
        question.Title = dto.Title;
        question.Content = sanitizer.Sanitize(dto.Content);
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        if (removed.Length > 0)
        {
            await context.Tags
                .Where(t => removed.Contains(t.Slug) && t.UsageCount > 0)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount,
                    t => t.UsageCount - 1));
        }

        if (added.Length > 0)
        {
            await context.Tags
                .Where(t => added.Contains(t.Slug))
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsageCount,
                    t => t.UsageCount + 1));
        }

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

    [Authorize]
    [HttpPost("{questionId}/answers")]
    public async Task<ActionResult> PostAnswer(string questionId, CreateAnswerDto dto)
    {
        var question = await context.Questions.FindAsync(questionId);

        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");

        if (userId is null || name is null) return BadRequest("Cannot get user details");

        var sanitizer = new HtmlSanitizer();

        var answer = new Answer
        {
            Content = sanitizer.Sanitize(dto.Content),
            UserId = userId,
            QuestionId = questionId
        };

        question.Answers.Add(answer);
        question.AnswerCount++;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));

        return Created($"/questions/{questionId}", answer);
    }

    [Authorize]
    [HttpPut("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
    {
        var answer = await context.Answers.FindAsync(answerId);
        if (answer is null) return NotFound();
        if (answer.QuestionId != questionId) return BadRequest("Cannot update answer details");

        var sanitizer = new HtmlSanitizer();

        answer.Content = sanitizer.Sanitize(dto.Content);
        answer.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    {
        var answer = await context.Answers.FindAsync(answerId);
        var question = await context.Questions.FindAsync(questionId);
        if (answer is null || question is null) return NotFound();
        if (answer.QuestionId != questionId || answer.Accepted) return BadRequest("Cannot delete this answer");

        context.Answers.Remove(answer);
        question.AnswerCount--;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));

        return NoContent();
    }

    [Authorize]
    [HttpPost("{questionId}/answers/{answerId}/accept")]
    public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
    {
        var answer = await context.Answers.FindAsync(answerId);
        var question = await context.Questions.FindAsync(questionId);
        if (answer is null || question is null) return NotFound();
        if (answer.QuestionId != questionId || question.HasAcceptedAnswer) return BadRequest("Cannot accept answer");

        answer.Accepted = true;
        question.HasAcceptedAnswer = true;

        await context.SaveChangesAsync();

        await bus.PublishAsync(new AnswerAccepted(questionId));
        await bus.PublishAsync(ReputationHelper.MakeEvent(answer.UserId,
           ReputationReason.AnswerAccepted, question.AskerId));

        return NoContent();
    }

    
}
