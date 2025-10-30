using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.Models;

namespace QuestionService.Controllers;

[Route("[controller]")]
[ApiController]
public class TagsController(QuestionDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetTags(string? sort)
    {
        var query = db.Tags.AsQueryable();

        query = sort == "popular"
            ? query.OrderByDescending(x => x.UsageCount).ThenBy(x => x.Name)
                : query.OrderBy(x => x.Name);

        return await query.ToListAsync();
    }
}