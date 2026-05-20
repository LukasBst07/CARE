using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CARE.Data;
using CARE.Models;
using CARE.Services;
using CARE.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddIdentity<AppUser, IdentityRole>(opts =>
{
    opts.Password.RequireDigit = true;
    opts.Password.RequiredLength = 8;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Admin/Login";
    opts.LogoutPath = "/Admin/Logout";
    opts.AccessDeniedPath = "/Admin/Login";
});

builder.Services.AddSingleton<LiveDataStore>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<DemoDataSeeder>();
builder.Services.AddSingleton<BattleNotifier>();
builder.Services.AddHostedService<MqttService>();
builder.Services.AddHostedService<ChallengeFinalizerService>();
builder.Services.AddHostedService<ThingsBoardSyncService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    db.Database.Migrate();

    if (!await roleMgr.RoleExistsAsync("Admin"))
        await roleMgr.CreateAsync(new IdentityRole("Admin"));

    const string adminEmail = "admin@care.local";
    var adminPw = cfg["Admin:DefaultPassword"] ?? "Care2024!";

    if (await userMgr.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userMgr.CreateAsync(admin, adminPw);
        if (result.Succeeded)
            await userMgr.AddToRoleAsync(admin, "Admin");
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    seeder.Seed();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHub<BattleHub>("/hubs/battle");

app.Run();