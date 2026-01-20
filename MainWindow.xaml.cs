using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Models.Data;
using TaskManager.Models.Models;
using Microsoft.Extensions.DependencyInjection;

namespace TaskManager.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Category> _categories = new();
        private readonly ObservableCollection<TaskItem> _tasks = new();
        private readonly ObservableCollection<AgendaRow> _agendaRows = new();
        private readonly ObservableCollection<ApplicationUser> _adminUsers = new();


        private int? _editingTaskId;
        private bool _suppressTaskSelectionChanged;
        private bool _uiReady;

        private readonly ApplicationUser _currentUser;
        private readonly UserManager<ApplicationUser> _userManager;
        private bool _isAdmin;
        private bool _isPowerUser;

        public MainWindow(ApplicationUser currentUser)
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            _uiReady = false;

            _currentUser = currentUser;
            _userManager = (UserManager<ApplicationUser>)App.Services.GetService(typeof(UserManager<ApplicationUser>))!;

            // we starten altijd op Tasks tab
            MainTabs.SelectedIndex = 0;

            CategoryList.ItemsSource = _categories;
            TaskList.ItemsSource = _tasks;
            AgendaList.ItemsSource = _agendaRows;
            AdminUserList.ItemsSource = _adminUsers;

        }

        private async Task ApplyRoleUi()
        {
            _isAdmin = await _userManager.IsInRoleAsync(_currentUser, "Admin");
            _isPowerUser = await _userManager.IsInRoleAsync(_currentUser, "PowerUser");

            AdminTab.Visibility = _isAdmin ? Visibility.Visible : Visibility.Collapsed;
            KpiTab.Visibility = (_isAdmin || _isPowerUser) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // afmelden = terug naar login
            var login = new LoginWindow();
            login.Show();
            Close();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ApplyRoleUi();

            if (_isAdmin)
                await LoadAdminUsersAsync();



            CurrentUserText.Text = $"Ingelogd als: {_currentUser.Email}";

            await EnsureDefaultCategoriesForUser();
            LoadCategories();

            AgendaDatePicker.SelectedDate = DateTime.Today;

            if (_categories.Count > 0)
                CategoryList.SelectedIndex = 0;

            LoadAgendaForSelectedDate();

            _uiReady = true;
            UpdateUiState();
            RefreshKpis();
        }

        // ==============================
        // DB helpers
        // ==============================
        private void Db(Action<TaskDbContext> action)
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();

                action(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database fout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
        private T Db<T>(Func<TaskDbContext, T> func)
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();

                return func(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database fout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return default!;
            }
        }

        private async Task DbAsync(Func<TaskDbContext, Task> action)
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();

                await action(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database fout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void Info(string message) =>
            MessageBox.Show(message, "Info", MessageBoxButton.OK, MessageBoxImage.Information);

        private bool Confirm(string message) =>
            MessageBox.Show(message, "Bevestigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

        // ==============================
        // Seeding + Loads
        // ==============================
        private async Task LoadAdminUsersAsync()
        {
            _adminUsers.Clear();

            var users = await _userManager.Users
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.Email)
                .ToListAsync();

            foreach (var u in users)
                _adminUsers.Add(u);
        }


        private ApplicationUser? SelectedAdminUser =>
            AdminUserList.SelectedItem as ApplicationUser;
        private async Task<ApplicationUser?> GetSelectedAdminUserFreshAsync()
        {
            if (SelectedAdminUser == null) return null;
            return await _userManager.FindByIdAsync(SelectedAdminUser.Id);
        }


        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            var name = NewCategoryBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var cat = new Category
            {
                Name = name,
                ApplicationUserId = _currentUser.Id
            };

            await DbAsync(async db =>
            {
                db.Categories.Add(cat);
                await db.SaveChangesAsync();
            });

            _categories.Add(cat);
            NewCategoryBox.Clear();
            CategoryList.SelectedItem = cat;
        }



        private async Task EnsureDefaultCategoriesForUser()
        {
            await DbAsync(async db =>
            {
                var hasAny = await db.Categories
                    .AnyAsync(c => c.ApplicationUserId == _currentUser.Id);

                if (hasAny) return;

                db.Categories.AddRange(
                    new Category { Name = "Algemeen", ApplicationUserId = _currentUser.Id },
                    new Category { Name = "Work", ApplicationUserId = _currentUser.Id }
                );

                await db.SaveChangesAsync();
            });
        }


        private void LoadCategories()
        {
            var cats = Db(db =>
            {
                return db.Categories
                         .Where(c => c.ApplicationUserId == _currentUser.Id)
                         .OrderBy(c => c.Name)
                         .ToList();
            });

            _categories.Clear();
            foreach (var c in cats) _categories.Add(c);
        }


        private TaskStatusFilter GetStatusFilter()
        {
            var text = (StatusFilterBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            return text switch
            {
                "Open" => TaskStatusFilter.Open,
                "Completed" => TaskStatusFilter.Completed,
                _ => TaskStatusFilter.All
            };
        }

        private void RefreshTasks()
        {
            if (CategoryList.SelectedItem is not Category cat) return;

            var filter = GetStatusFilter();

            var list = Db(db =>
            {
                var q = db.Tasks.Where(t => t.CategoryId == cat.Id
                         && t.ApplicationUserId == _currentUser.Id);


                if (filter == TaskStatusFilter.Open) q = q.Where(t => !t.IsCompleted);
                if (filter == TaskStatusFilter.Completed) q = q.Where(t => t.IsCompleted);

                return q.OrderBy(t => t.IsCompleted).ThenBy(t => t.Title).ToList();
            });

            _tasks.Clear();
            foreach (var t in list) _tasks.Add(t);

            UpdateUiState();
            RefreshKpis();
        }

        private void RefreshAgenda()
        {
            if (AgendaDatePicker.SelectedDate is not DateTime date) return;
            var day = date.Date;

            var items = Db(db =>
                db.Agenda
                  .Include(a => a.Task)
                  .Where(a => a.PlannedDate.Date == day
                           && a.ApplicationUserId == _currentUser.Id)

                  .OrderBy(a => a.Id)
                  .ToList()
            );



            _agendaRows.Clear();
            foreach (var a in items)
            {
                _agendaRows.Add(new AgendaRow
                {
                    AgendaId = a.Id,
                    TaskId = a.TaskItemId,
                    TaskTitle = a.Task?.Title ?? "(unknown task)",
                    TaskDescription = a.Task?.Description ?? ""
                });
            }

            UpdateUiState();
            RefreshKpis();

        }

        private void LoadAgendaForSelectedDate() => RefreshAgenda();
        private void RefreshKpis()
        {
            // alleen berekenen als KPI-tab zichtbaar is (admin of poweruser)
            if (KpiTab.Visibility != Visibility.Visible) return;

            var today = DateTime.Today;
            var next7 = today.AddDays(7);

            var totalTasks = Db(db =>
                db.Tasks.Count(t => t.ApplicationUserId == _currentUser.Id));

            var openTasks = Db(db =>
                db.Tasks.Count(t => t.ApplicationUserId == _currentUser.Id && !t.IsCompleted));

            var completedTasks = Db(db =>
                db.Tasks.Count(t => t.ApplicationUserId == _currentUser.Id && t.IsCompleted));

            var agendaToday = Db(db =>
                db.Agenda.Count(a => a.ApplicationUserId == _currentUser.Id && a.PlannedDate.Date == today));

            var agendaNext7Days = Db(db =>
                db.Agenda.Count(a => a.ApplicationUserId == _currentUser.Id
                                  && a.PlannedDate.Date >= today
                                  && a.PlannedDate.Date <= next7));

            KpiTotalTasksText.Text = $"Totaal taken: {totalTasks}";
            KpiOpenTasksText.Text = $"Open taken: {openTasks}";
            KpiCompletedTasksText.Text = $"Voltooide taken: {completedTasks}";
            KpiAgendaTodayText.Text = $"Agenda vandaag: {agendaToday}";
            KpiAgendaNext7DaysText.Text = $"Agenda komende 7 dagen: {agendaNext7Days}";

            var hasAnyData = totalTasks > 0 || agendaToday > 0 || agendaNext7Days > 0;
            KpiEmptyHint.Visibility = hasAnyData ? Visibility.Collapsed : Visibility.Visible;
        }


        // ==============================
        // UI state
        // ==============================
        private void UpdateUiState()
        {
            TasksEmptyText.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AgendaEmptyText.Visibility = _agendaRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var hasTask = TaskList.SelectedItem is TaskItem;
            var hasAgenda = AgendaList.SelectedItem is AgendaRow;
            var hasDate = AgendaDatePicker.SelectedDate is not null;
            var editing = _editingTaskId is not null;

            SaveTaskButton.IsEnabled = editing;
            CancelEditButton.IsEnabled = editing;

            DeleteTaskButton.IsEnabled = hasTask;
            PlanTaskButton.IsEnabled = hasTask && hasDate;
            DeleteAgendaButton.IsEnabled = hasAgenda;
            RescheduleAgendaButton.IsEnabled = hasAgenda && RescheduleDatePicker.SelectedDate is not null;
        }

        private void EnterEditMode(int taskId) => _editingTaskId = taskId;

        private void ExitEditMode()
        {
            _editingTaskId = null;

            _suppressTaskSelectionChanged = true;
            TaskList.SelectedItem = null;
            _suppressTaskSelectionChanged = false;

            TaskTitleBox.Clear();
            TaskDescBox.Clear();

            UpdateUiState();
        }

        // ==============================
        // Categories events
        // ==============================
        private async void RenameCategory_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (CategoryList.SelectedItem is not Category cat)
            {
                Info("Selecteer eerst een category om te hernoemen.");
                return;
            }

            var newName = (RenameCategoryBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                Info("Geef een nieuwe naam in.");
                return;
            }

            await DbAsync(async db =>
            {
                var dbCat = await db.Categories
                    .FirstOrDefaultAsync(c => c.Id == cat.Id && c.ApplicationUserId == _currentUser.Id);

                if (dbCat == null) return;

                dbCat.Name = newName;
                await db.SaveChangesAsync();
            });

            RenameCategoryBox.Clear();
            LoadCategories();

            var refreshed = _categories.FirstOrDefault(c => c.Id == cat.Id);
            if (refreshed != null) CategoryList.SelectedItem = refreshed;
        }



        private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (CategoryList.SelectedItem is not Category cat)
            {
                Info("Selecteer eerst een category om te verwijderen.");
                return;
            }

            var taskCount = Db(db =>
                db.Tasks.Count(t => t.CategoryId == cat.Id && t.ApplicationUserId == _currentUser.Id)
            );

            if (!Confirm($"Category verwijderen?\n\nNaam: {cat.Name}\nTasks die ook verdwijnen: {taskCount}"))
                return;

            await DbAsync(async db =>
            {
                var dbCat = await db.Categories
                    .FirstOrDefaultAsync(c => c.Id == cat.Id && c.ApplicationUserId == _currentUser.Id);

                if (dbCat == null) return;

                dbCat.IsDeleted = true;

                var relatedTasks = await db.Tasks
                    .Where(t => t.CategoryId == cat.Id && t.ApplicationUserId == _currentUser.Id)
                    .ToListAsync();

                foreach (var t in relatedTasks)
                    t.IsDeleted = true;

                var taskIds = relatedTasks.Select(t => t.Id).ToList();

                var relatedAgenda = await db.Agenda
                    .Where(a => taskIds.Contains(a.TaskItemId) && a.ApplicationUserId == _currentUser.Id)
                    .ToListAsync();

                foreach (var a in relatedAgenda)
                    a.IsDeleted = true;

                await db.SaveChangesAsync();
            });

            _categories.Remove(cat);
            ExitEditMode();
            _tasks.Clear();

            if (_categories.Count > 0)
                CategoryList.SelectedIndex = 0;

            RefreshAgenda();
            UpdateUiState();
        }


        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ExitEditMode();
            RefreshTasks();
        }

        private void StatusFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ExitEditMode();
            RefreshTasks();
        }

        // ==============================
        // Tasks events
        // ==============================
        private void TaskList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            if (_suppressTaskSelectionChanged) return;

            if (TaskList.SelectedItem is not TaskItem task)
            {
                UpdateUiState();
                return;
            }

            TaskTitleBox.Text = task.Title;
            TaskDescBox.Text = task.Description ?? "";

            EnterEditMode(task.Id);
            UpdateUiState();
        }

        private async void AddTask_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (CategoryList.SelectedItem is not Category cat)
            {
                Info("Selecteer eerst een category.");
                return;
            }

            var title = TaskTitleBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            var desc = string.IsNullOrWhiteSpace(TaskDescBox.Text) ? null : TaskDescBox.Text.Trim();

            await DbAsync(async db =>
            {
                db.Tasks.Add(new TaskItem
                {
                    Title = title,
                    Description = desc,
                    IsCompleted = false,
                    CategoryId = cat.Id,
                    ApplicationUserId = _currentUser.Id
                });

                await db.SaveChangesAsync();
            });

            ExitEditMode();
            RefreshTasks();
        }


        private async void SaveTask_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            if (_editingTaskId is null) return;

            var title = TaskTitleBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            var desc = string.IsNullOrWhiteSpace(TaskDescBox.Text) ? null : TaskDescBox.Text.Trim();
            var id = _editingTaskId.Value;

            await DbAsync(async db =>
            {
                var dbTask = await db.Tasks
                    .FirstOrDefaultAsync(t => t.Id == id && t.ApplicationUserId == _currentUser.Id);

                if (dbTask == null) return;

                dbTask.Title = title;
                dbTask.Description = desc;

                await db.SaveChangesAsync();
            });

            ExitEditMode();
            RefreshTasks();
        }


        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            ExitEditMode();
        }

        private async void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (TaskList.SelectedItem is not TaskItem task)
            {
                Info("Selecteer eerst een task om te verwijderen.");
                return;
            }

            if (!Confirm($"Task verwijderen?\n\nTitel: {task.Title}"))
                return;

            await DbAsync(async db =>
            {
                var dbTask = await db.Tasks
                    .FirstOrDefaultAsync(t => t.Id == task.Id && t.ApplicationUserId == _currentUser.Id);

                if (dbTask == null) return;

                dbTask.IsDeleted = true;

                var relatedAgenda = await db.Agenda
                    .Where(a => a.TaskItemId == task.Id && a.ApplicationUserId == _currentUser.Id)
                    .ToListAsync();

                foreach (var a in relatedAgenda)
                    a.IsDeleted = true;

                await db.SaveChangesAsync();
            });

            ExitEditMode();
            RefreshTasks();
            AgendaList.SelectedItem = null;
            RefreshAgenda();
        }


        private async void TaskCompleted_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (sender is not CheckBox cb) return;
            if (cb.DataContext is not TaskItem task) return;

            await DbAsync(async db =>
            {
                var dbTask = await db.Tasks
                    .FirstOrDefaultAsync(t => t.Id == task.Id && t.ApplicationUserId == _currentUser.Id);

                if (dbTask == null) return;

                dbTask.IsCompleted = task.IsCompleted;

                await db.SaveChangesAsync();
            });

            RefreshTasks();
        }


        private void TaskCheckBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // bewust laten staan (handler in XAML)
            e.Handled = false;
        }

        // ==============================
        // Agenda events
        // ==============================
        private void AgendaDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            RefreshAgenda();
        }

        private void RescheduleDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            UpdateUiState();
        }


        private async void PlanTask_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (TaskList.SelectedItem is not TaskItem task)
            {
                Info("Selecteer eerst een task om te plannen in de agenda.");
                return;
            }

            if (AgendaDatePicker.SelectedDate is not DateTime date)
            {
                Info("Selecteer eerst een datum.");
                return;
            }

            await DbAsync(async db =>
            {
                // ownership check: task moet van current user zijn
                var dbTask = await db.Tasks
                    .FirstOrDefaultAsync(t => t.Id == task.Id && t.ApplicationUserId == _currentUser.Id);

                if (dbTask == null) return;

                db.Agenda.Add(new AgendaItem
                {
                    TaskItemId = dbTask.Id,
                    PlannedDate = date.Date,
                    ApplicationUserId = _currentUser.Id
                });

                await db.SaveChangesAsync();
            });

            RefreshAgenda();
        }


        private void AgendaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            UpdateUiState();
        }

        private async void DeleteAgendaItem_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (AgendaList.SelectedItem is not AgendaRow row)
            {
                Info("Selecteer eerst een agenda item om te verwijderen.");
                return;
            }

            if (!Confirm($"Agenda item verwijderen?\n\nTask: {row.TaskTitle}"))
                return;

            await DbAsync(async db =>
            {
                var dbAgenda = await db.Agenda
                    .FirstOrDefaultAsync(a => a.Id == row.AgendaId && a.ApplicationUserId == _currentUser.Id);

                if (dbAgenda == null) return;

                dbAgenda.IsDeleted = true;
                await db.SaveChangesAsync();
            });

            RefreshAgenda();
        }

        private async void RescheduleAgendaItem_Click(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            if (AgendaList.SelectedItem is not AgendaRow row)
            {
                Info("Selecteer eerst een agenda item om te verplaatsen.");
                return;
            }

            if (RescheduleDatePicker.SelectedDate is not DateTime newDate)
            {
                Info("Kies eerst een nieuwe datum.");
                return;
            }

            await DbAsync(async db =>
            {
                var dbAgenda = await db.Agenda
                    .FirstOrDefaultAsync(a => a.Id == row.AgendaId && a.ApplicationUserId == _currentUser.Id);

                if (dbAgenda == null) return;

                dbAgenda.PlannedDate = newDate.Date;
                await db.SaveChangesAsync();
            });

            RefreshAgenda();
        }

        // ==============================
        // Admin tab actions
        // ==============================

        private async void MakeAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;

            var user = await GetSelectedAdminUserFreshAsync();
            if (user == null) { Info("Selecteer eerst een user."); return; }

            await _userManager.RemoveFromRoleAsync(user, "User");
            await _userManager.RemoveFromRoleAsync(user, "PowerUser");
            await _userManager.AddToRoleAsync(user, "Admin");

            await LoadAdminUsersAsync();
            Info("User is nu Admin.");

        }

        private async void MakePowerUser_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;

            var user = await GetSelectedAdminUserFreshAsync();
            if (user == null) { Info("Selecteer eerst een user."); return; }

            await _userManager.RemoveFromRoleAsync(user, "User");
            await _userManager.RemoveFromRoleAsync(user, "Admin");
            await _userManager.AddToRoleAsync(user, "PowerUser");

            await LoadAdminUsersAsync();
            Info("User is nu PowerUser.");

        }

        private async void MakeUser_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;

            var user = await GetSelectedAdminUserFreshAsync();
            if (user == null) { Info("Selecteer eerst een user."); return; }

            await _userManager.RemoveFromRoleAsync(user, "Admin");
            await _userManager.RemoveFromRoleAsync(user, "PowerUser");
            await _userManager.AddToRoleAsync(user, "User");

            await LoadAdminUsersAsync();
            Info("User is nu User.");

        }

        private async void BlockUser_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;

            var user = await GetSelectedAdminUserFreshAsync();
            if (user == null) { Info("Selecteer eerst een user."); return; }

            // block = lockout tot max
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var msg = string.Join("\n", result.Errors.Select(er => $"{er.Code}: {er.Description}"));
                MessageBox.Show(msg, "Block user fout", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadAdminUsersAsync();
            Info("User geblokkeerd.");
        }


        private async void UnblockUser_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;

            var user = await GetSelectedAdminUserFreshAsync();
            if (user == null) { Info("Selecteer eerst een user."); return; }

            // Unblock = lockout verwijderen
            user.LockoutEnd = null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var msg = string.Join("\n", result.Errors.Select(er => $"{er.Code}: {er.Description}"));
                MessageBox.Show(msg, "Unblock user fout", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadAdminUsersAsync();
            Info("User gedeblokkeerd.");
        }


        private async void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (!_isAdmin) return;

            var user = await GetSelectedAdminUserFreshAsync();
            if (user == null) { Info("Selecteer eerst een user."); return; }

            // Je kan jezelf niet verwijderen
            if (user.Id == _currentUser.Id)
            {
                Info("Je kan jezelf niet verwijderen.");
                return;
            }

            if (!Confirm($"User verwijderen?\n\nEmail: {user.Email}\n\nDit is een SOFT delete (user wordt geblokkeerd en verdwijnt uit de lijst)."))
                return;

            // Soft delete + block
            user.IsDeleted = true;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var msg = string.Join("\n", result.Errors.Select(er => $"{er.Code}: {er.Description}"));
                MessageBox.Show(msg, "Delete user fout", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadAdminUsersAsync();
            Info("User deleted (soft) + blocked.");
        }


        // ==============================
        // Helper types
        // ==============================
        private enum TaskStatusFilter { All, Open, Completed }

        private class AgendaRow
        {
            public int AgendaId { get; set; }
            public int TaskId { get; set; }
            public string TaskTitle { get; set; } = "";
            public string TaskDescription { get; set; } = "";
        }
    }
}