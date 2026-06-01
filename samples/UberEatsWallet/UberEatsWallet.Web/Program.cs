using UberEatsWallet.Application;
using UberEatsWallet.Infrastructure;
using UberEatsWallet.Web.Identity;

// Load this sample's OWN .env (one level above the Web project) before the host reads configuration,
// so WALLET_*, WEBHOOK_HMAC_SECRET, and ASPNETCORE_URLS are all picked up. The wallet's repo-root
// .env is intentionally not used.
var sampleEnv = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"));
if (File.Exists(sampleEnv))
{
    DotNetEnv.Env.Load(sampleEnv);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<ICurrentActorAccessor, CurrentActorAccessor>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllers(); // attribute-routed webhook receiver
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
