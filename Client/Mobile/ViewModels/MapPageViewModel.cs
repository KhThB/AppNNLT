using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mobile.Models;
using Mobile.Services;

namespace Mobile.ViewModels;

public partial class MapPageViewModel : ObservableObject
{
    private readonly PoiService _poiService;

    // ĐỔI TỪ PIN SANG POIMODEL
    public ObservableCollection<PoiModel> Pois { get; } = new();

    public MapPageViewModel(PoiService poiService)
    {
        _poiService = poiService;
    }

    [RelayCommand]
    public async Task LoadPinsAsync()
    {
        var items = await _poiService.GetPoisAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Pois.Clear();
            foreach (var item in items)
            {
                Pois.Add(item);
            }
        });
    }
}