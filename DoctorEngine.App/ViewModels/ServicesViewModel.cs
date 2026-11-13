using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoctorEngine.App.Services;

namespace DoctorEngine.App.ViewModels;

/// <summary>Bütün xidmətlər və videoları (saytın Xidmətlər səhifəsi).</summary>
public partial class ServicesViewModel : BaseViewModel
{
    private readonly ContentService _content;

    [ObservableProperty] private bool isRefreshing;

    public ObservableCollection<CategoryItem> Categories { get; } = new();

    public ServicesViewModel(ContentService content) => _content = content;

    [RelayCommand]
    public async Task LoadAsync(bool force = false)
    {
        await RunAsync(async () =>
        {
            var home = await _content.GetHomeAsync(force);
            Categories.Clear();
            foreach (var c in home?.Categories ?? new List<DoctorEngine.Contracts.CategoryDto>())
                Categories.Add(CategoryItem.From(c));
        });
        IsRefreshing = false;
    }

    [RelayCommand] private Task Refresh() => LoadAsync(true);

    [RelayCommand] private Task PlayVideo(string? url) => OpenAsync(url);

    [RelayCommand]
    private Task Book(CategoryItem? category) =>
        Shell.Current.GoToAsync(category?.ReservationServiceId is int id ? $"//booking?serviceId={id}" : "//booking");
}
