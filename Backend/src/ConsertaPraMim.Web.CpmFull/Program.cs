using AppMobileCPM.Services;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IMarketplaceRepository, SqlMarketplaceRepository>();
builder.Services.AddSingleton<IAdminAuthService, SqlAdminAuthService>();
builder.Services.AddSingleton<IAdminSiteContentService, SqlAdminSiteContentService>();
builder.Services.AddSingleton<IAdminSupportFaqService, SqlAdminSupportFaqService>();
builder.Services.AddSingleton<IAdminKanbanService, SqlAdminKanbanService>();
builder.Services.AddScoped<ISiteContentResolver, SiteContentResolver>();
builder.Services.AddAuthentication(AdminAuthConstants.AuthenticationScheme)
    .AddCookie(AdminAuthConstants.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "cpm_admin_auth";
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });
builder.Services.AddAuthorization();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/xml", "text/xml", "image/svg+xml"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        const int maxAgeInSeconds = 60 * 60 * 24 * 30;
        context.Context.Response.Headers.CacheControl = $"public,max-age={maxAgeInSeconds}";
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
