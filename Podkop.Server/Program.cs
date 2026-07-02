var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


var api = app.MapGroup("/api");
api.MapGet("sink", () =>
    {
        var items = Enumerable.Range(1, 5).Select(index =>
                new SinkItem
                (
                    index,
                    $"Card {index}",
                    $"Content of card {index}",
                    $"https://picsum.photos/id/{index * 10}/220/142"
                ))
            .ToArray();
        return items;
    })
    .WithName("GetSinkItems");

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

record SinkItem(int Id, string Title, string Content, string Image);