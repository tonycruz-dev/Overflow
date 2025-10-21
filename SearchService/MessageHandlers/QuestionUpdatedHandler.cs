using Contracts;
using SearchService.Models;
using System.Text.RegularExpressions;
using Typesense;

namespace SearchService.MessageHandlers;

public class QuestionUpdatedHandler(ITypesenseClient client)
{
    public async Task Handle(QuestionUpdated message)
    {
        await client.UpdateDocument("questions", message.QuestionId, new 
        {
            message.Title,
            Content = StripHtml(message.Content),
            Tags = message.Tags.ToArray(),
        });
    }

    private static string StripHtml(string content)
    {
        return Regex.Replace(content, "<.*?>", string.Empty);
    }
}