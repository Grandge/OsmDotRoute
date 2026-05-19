using MapVerifier.Server.Endpoints;
using MapVerifier.Server.Services;
using Microsoft.AspNetCore.ResponseCompression;
using OsmDotRoute;

const string CorsPolicyName = "MapVerifierWeb";

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("MapVerifier:AllowedOrigins")
    .Get<string[]>()
    ?? new[] { "http://localhost:5173" };

var maxBodyBytes = builder.Configuration.GetValue<long?>("MapVerifier:MaxRequestBodyBytes") ?? 2_147_483_648L;

builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = maxBodyBytes);

builder.Services.AddCors(o => o.AddPolicy(CorsPolicyName, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/geo+json" });
});

builder.Services.AddSingleton<RouterState>();
builder.Services.AddSingleton<RestrictedAreaService>();

var app = builder.Build();

app.UseResponseCompression();
app.UseCors(CorsPolicyName);

app.MapVersionEndpoints();
app.MapLoadEndpoints();
app.MapRoadNetworkEndpoints();
app.MapSnapAndRouteEndpoints();
app.MapMeshEndpoints();
app.MapRestrictionEndpoints();
app.MapBrowseEndpoints();

app.Run();
