using System.Windows;
using Microsoft.AspNetCore.Identity;
using TaskManager.Models.Models;

namespace TaskManager.Wpf
{
    public partial class AdminWindow : Window
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminWindow()
        {
            InitializeComponent();
            _userManager = (UserManager<ApplicationUser>)App.Services.GetService(typeof(UserManager<ApplicationUser>))!;
            LoadUsers();
        }

        private void LoadUsers()
        {
            UserList.ItemsSource = _userManager.Users.ToList();
        }

        private ApplicationUser? SelectedUser =>
            UserList.SelectedItem as ApplicationUser;

        private async void MakeAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null) return;

            // 1 rol per user; eerst andere rollen weg
            await _userManager.RemoveFromRoleAsync(SelectedUser, "User");
            await _userManager.RemoveFromRoleAsync(SelectedUser, "PowerUser");
            await _userManager.AddToRoleAsync(SelectedUser, "Admin");

            MessageBox.Show("User is now Admin");
        }

        private async void MakePowerUser_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null) return;

            await _userManager.RemoveFromRoleAsync(SelectedUser, "Admin");
            await _userManager.RemoveFromRoleAsync(SelectedUser, "User");
            await _userManager.AddToRoleAsync(SelectedUser, "PowerUser");

            MessageBox.Show("User is now PowerUser");
        }

        private async void MakeUser_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null) return;

            await _userManager.RemoveFromRoleAsync(SelectedUser, "Admin");
            await _userManager.RemoveFromRoleAsync(SelectedUser, "PowerUser");
            await _userManager.AddToRoleAsync(SelectedUser, "User");

            MessageBox.Show("User is now User");
        }

        private async void BlockUser_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null) return;

            await _userManager.SetLockoutEndDateAsync(
                SelectedUser,
                DateTimeOffset.MaxValue
            );

            MessageBox.Show("User blocked");
        }

        private async void UnblockUser_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null) return;

            await _userManager.SetLockoutEndDateAsync(
                SelectedUser,
                null
            );

            MessageBox.Show("User unblocked");
        }
    }
}