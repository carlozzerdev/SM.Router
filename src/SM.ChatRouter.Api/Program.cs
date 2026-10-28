using System.Text.Json.Serialization;
using SM.ChatRouter.Api.Endpoints;
using SM.ChatRouter.Api.Infrastructure;
using SM.ChatRouter.Application;
using SM.ChatRouter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<InvalidChatMessageExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapChatEndpoints();

app.Run();

public partial class Program;
