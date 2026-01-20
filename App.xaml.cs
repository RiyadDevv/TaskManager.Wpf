using System.Windows;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Models.Data;
using TaskManager.Models.Models;
using Microsoft.Extensions.Configuration;


namespace TaskManager.Wpf
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            var config = new ConfigurationBuilder()
                .AddUserSecrets<App>(optional: true)
                .Build();

            services.AddSingleton<IConfiguration>(config);


            // DB in LocalAppData
            var dbPath = DbPath.GetDatabasePath();

            services.AddDbContext<TaskDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));


            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;

                // lockout fix
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TaskDbContext>()
            .AddRoleManager<RoleManager<IdentityRole>>()
            .AddUserManager<UserManager<ApplicationUser>>();

            Services = services.BuildServiceProvider();

            await EnsureDatabaseAndIdentitySeeded();

            var login = new LoginWindow();
            login.Show();
        }

        private static async Task EnsureDatabaseAndIdentitySeeded()
        {
            using var scope = Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
            await db.Database.MigrateAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // 1) roles
            var roles = new[] { "Admin", "User", "PowerUser" };
            foreach (var r in roles)
            {
                if (!await roleManager.RoleExistsAsync(r))
                    await roleManager.CreateAsync(new IdentityRole(r));
            }

            // 2) admin user (via user secrets)
            var adminEmail = config["TM_ADMIN_EMAIL"];
            var adminPassword = config["TM_ADMIN_PASSWORD"];

            ApplicationUser? admin = null;

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                admin = await userManager.FindByEmailAsync(adminEmail);

                if (admin == null)
                {
                    admin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        DisplayName = "Admin",
                        LockoutEnabled = true
                    };

                    var createResult = await userManager.CreateAsync(admin, adminPassword);
                    if (!createResult.Succeeded)
                    {
                        var msg = string.Join("\n", createResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                        MessageBox.Show(msg, "Admin seed fout", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (!await userManager.IsInRoleAsync(admin, "Admin"))
                    await userManager.AddToRoleAsync(admin, "Admin");

                if (!await userManager.IsInRoleAsync(admin, "User"))
                    await userManager.AddToRoleAsync(admin, "User");
            }

            // 3) dummy data alleen als admin bestaat
            if (admin == null) return;

            // 3a) categories
            var adminCategory = await db.Categories
                .IgnoreQueryFilters()
                .Where(c => c.ApplicationUserId == admin.Id)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            if (adminCategory == null)
            {
                var c1 = new Category { Name = "Algemeen", ApplicationUserId = admin.Id, IsDeleted = false };
                var c2 = new Category { Name = "Work", ApplicationUserId = admin.Id, IsDeleted = false };

                db.Categories.AddRange(c1, c2);
                await db.SaveChangesAsync();

                adminCategory = c1;
            }

            // 3b) task
            var demoTask = await db.Tasks
                .IgnoreQueryFilters()
                .Where(t => t.ApplicationUserId == admin.Id)
                .OrderBy(t => t.Id)
                .FirstOrDefaultAsync();

            if (demoTask == null)
            {
                demoTask = new TaskItem
                {
                    Title = "Demo Task",
                    Description = "Dummy task voor examen (seed).",
                    CategoryId = adminCategory.Id,
                    ApplicationUserId = admin.Id,
                    IsCompleted = false,
                    IsDeleted = false
                };

                db.Tasks.Add(demoTask);
                await db.SaveChangesAsync();
            }

            // 3c) agenda
            var hasAgenda = await db.Agenda
                .IgnoreQueryFilters()
                .AnyAsync(a => a.TaskItemId == demoTask.Id);

            if (!hasAgenda)
            {
                db.Agenda.Add(new AgendaItem
                {
                    TaskItemId = demoTask.Id,
                    PlannedDate = DateTime.Today,
                    ApplicationUserId = admin.Id,
                    IsDeleted = false
                });

                await db.SaveChangesAsync();
            }
        }

    }
}