using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TaskManager.Models.Data;
using TaskManager.Models.Models;

namespace TaskManager.Wpf
{
    public partial class LoginWindow : Window
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginWindow()
        {
            InitializeComponent();
            _userManager = (UserManager<ApplicationUser>)App.Services.GetService(typeof(UserManager<ApplicationUser>))!;
        }
        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var email = (EmailBox.Text ?? "").Trim();
            var password = PasswordBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Vul email en password in.");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = email,
                LockoutEnabled = true //zodat block/unblock werkt
            };



            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var msg = string.Join("\n", result.Errors.Select(e2 => $"- {e2.Code}: {e2.Description}"));
                MessageBox.Show(msg);
                return;
            }

            // nieuwe users worden standaard "User"
            await _userManager.AddToRoleAsync(user, "User");

            // default categories voor nieuwe user
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();

            db.Categories.Add(new Category { Name = "Algemeen", ApplicationUserId = user.Id });
            db.Categories.Add(new Category { Name = "Work", ApplicationUserId = user.Id });
            await db.SaveChangesAsync();


            MessageBox.Show("Account aangemaakt. Je kan nu inloggen.");
            //EmailBox.Clear();
            PasswordBox.Clear();

        }
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var email = (EmailBox.Text ?? "").Trim();
            var password = PasswordBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Vul email en password in.");
                return;
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
            {
                MessageBox.Show("User niet gevonden.");
                return;
            }

            // Als je later users blokkeert via lockout
            if (await _userManager.IsLockedOutAsync(user))
            {
                MessageBox.Show("Deze user is geblokkeerd.");
                return;
            }

            var ok = await _userManager.CheckPasswordAsync(user, password);
            if (!ok)
            {
                MessageBox.Show("Fout password.");
                return;
            }

            // Login OK; open MainWindow met current user
            var main = new MainWindow(user);
            main.Show();

            Close();
        }
    }
}