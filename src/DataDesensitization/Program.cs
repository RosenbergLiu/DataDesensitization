using DataDesensitization.Components;
using DataDesensitization.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register database provider factories
builder.Services.AddSingleton<SqlServerProviderFactory>();
builder.Services.AddSingleton<PostgreSqlProviderFactory>();

// Register ConnectionManager (stateful — holds the active connection)
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// Register SchemaIntrospectorResolver as ISchemaIntrospector.
// It lazily creates the correct provider-specific introspector
// based on the current connection managed by IConnectionManager.
builder.Services.AddSingleton<ISchemaIntrospector, SchemaIntrospectorResolver>();

// Register SchemaService
builder.Services.AddSingleton<ISchemaService, SchemaService>();

// Register RuleConfigurationService (stateful — holds configured rules)
builder.Services.AddSingleton<IRuleConfigurationService, RuleConfigurationService>();

// Register DesensitizationEngine
builder.Services.AddSingleton<IDesensitizationEngine, DesensitizationEngine>();

// Register ProfileManager (uses factory to handle optional storageDir parameter)
builder.Services.AddSingleton<IProfileManager>(sp =>
    new ProfileManager(
        sp.GetRequiredService<ISchemaIntrospector>(),
        sp.GetRequiredService<IConnectionManager>()));

// Register ReportSerializer (stateless)
builder.Services.AddSingleton<IReportSerializer, ReportSerializer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
