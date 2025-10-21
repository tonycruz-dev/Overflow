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
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetTags()
    {
        return await db.Tags.OrderBy(x => x.Name).ToListAsync();
    }
}