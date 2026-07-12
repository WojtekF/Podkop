using Podkop.Findings.Infrastructure;
using Podkop.Findings.Server;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddFindings();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


app.MapFindings();

// Legacy mock feed — the frontend still consumes it; removed once the frontend
// switches to /api/findings.
var api = app.MapGroup("/api");
api.MapGet("sink", () =>
    {
        string[] authors = ["ada_lovelace", "grace_hopper", "linus_t", "margaret_h", "dennis_r"];
        string[] domains = ["github.com", "news.ycombinator.com", "dev.to", "medium.com", "stackoverflow.blog"];
        string[] allTags = ["dotnet", "angular", "webdev", "csharp", "typescript", "aspire", "performance", "ui"];
        string[] sentences =
        [
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
            "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
            "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.",
            "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
            "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium.",
            "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos.",
            "Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit.",
            "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque.",
        ];

        var items = Enumerable.Range(1, 15).Select(index =>
                new MainPostDto
                (
                    index,
                    $"Card {index}",
                    string.Join(" ", Random.Shared.GetItems(sentences, Random.Shared.Next(1, 9))),
                    $"https://picsum.photos/id/{index * 10}/220/142",
                    DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 72)),
                    Random.Shared.GetItems(allTags, Random.Shared.Next(1, 4)).Distinct().ToArray(),
                    authors[Random.Shared.Next(authors.Length)],
                    Random.Shared.Next(0, 250),
                    Random.Shared.Next(0, 1500),
                    domains[Random.Shared.Next(domains.Length)]
                ))
            .ToArray();
        return items;
    })
    .WithName("GetSinkItems");

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

record MainPostDto(int Id, string Title, string Content, string Image, DateTime  CreatedAt, string[] Tags, string Author, int CommentCount, int UpvoteCount, string Domain);

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;