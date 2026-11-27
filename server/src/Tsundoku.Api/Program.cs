using Scalar.AspNetCore;
using Tsundoku.Api.Hosting;
using Tsundoku.Common;
using Tsundoku.Common.Persistence;
using Tsundoku.Common.Valkey;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCommon();
builder.AddValkey();
builder.AddModules();
builder.AddApiHosting();

var app = builder.Build();

app.UseApiHosting();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// `migrate` is the deploy step: apply the migrations and the login role, then exit without serving.
var migrateOnly = args is ["migrate"];
if (migrateOnly || ModulePersistence.MigratesOnStartup(app.Configuration)) await app.MigrateModulesAsync();
if (migrateOnly) return;

app.MapDefaultEndpoints();
app.MapModules();

await app.RunAsync();

/// <summary>The entry point. Public, so the integration tests can start the app with WebApplicationFactory.</summary>
public partial class Program;
