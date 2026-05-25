using ErgStream.Services;
using ErgStream.ViewModels;
using System.ComponentModel;

namespace ErgStream.Pages;

public partial class ErgProgramPage : ContentPage
{
	private readonly ErgProgramViewModel viewModel;
	private readonly IBeepService? beepService;

	// Beep tracking: last whole-second floor we already beeped for
	private int? _lastBeepSecond;

	// Recovery/CoolDown red indicator: track when the interval started
	private DateTime? _recoveryIntervalStart;
	private ErgProgramIntervalType? _lastIntervalType;

	// PM5-style colors
	private static readonly Color ColorTransparent = Colors.Transparent;
	private static readonly Color ColorYellow = Color.FromArgb("#F5C518");
	private static readonly Color ColorGreen = Color.FromArgb("#2ECC40");
	private static readonly Color ColorRed = Color.FromArgb("#E74C3C");

	public ErgProgramPage(ErgProgramViewModel viewModel, IBeepService? beepService = null)
	{
		InitializeComponent();

		this.viewModel = viewModel;
		this.beepService = beepService;
		BindingContext = this.viewModel;

		this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ErgProgramViewModel.IntervalType) ||
			e.PropertyName == nameof(ErgProgramViewModel.IntervalTimeRemaining))
		{
			UpdateIntervalIndicator();
		}

		if (e.PropertyName == nameof(ErgProgramViewModel.IntervalTimeRemaining))
		{
			TryBeepCountdown();
		}
	}

	private void UpdateIntervalIndicator()
	{
		var intervalType = viewModel.IntervalType;

		// Detect interval type transitions to track recovery start time
		if (intervalType != _lastIntervalType)
		{
			_lastIntervalType = intervalType;
			if (intervalType == ErgProgramIntervalType.Recovery ||
				intervalType == ErgProgramIntervalType.CoolDown)
			{
				_recoveryIntervalStart = DateTime.UtcNow;
			}
			else
			{
				_recoveryIntervalStart = null;
			}
			// Reset beep tracking on interval change
			_lastBeepSecond = null;
		}

		Color indicatorColor = ColorTransparent;

		switch (intervalType)
		{
			case ErgProgramIntervalType.BuildToMaxEffort:
				indicatorColor = ColorYellow;
				break;

			case ErgProgramIntervalType.MaxEffort:
				indicatorColor = ColorGreen;
				break;

			case ErgProgramIntervalType.Recovery:
			case ErgProgramIntervalType.CoolDown:
				if (_recoveryIntervalStart.HasValue &&
					(DateTime.UtcNow - _recoveryIntervalStart.Value).TotalSeconds <= 3.0)
				{
					indicatorColor = ColorRed;
				}
				break;
		}

		IntervalIndicator.Color = indicatorColor;
	}

	private void TryBeepCountdown()
	{
		var intervalType = viewModel.IntervalType;
		if (intervalType != ErgProgramIntervalType.BuildToMaxEffort &&
			intervalType != ErgProgramIntervalType.MaxEffort)
		{
			return;
		}

		int currentSecond = (int)Math.Floor(viewModel.IntervalTimeRemaining.TotalSeconds);
		if (currentSecond > 2)
			return;
		
		if (_lastBeepSecond == currentSecond)
		{
			return;
		}

		_lastBeepSecond = currentSecond;

		if (currentSecond > 0)
		{
			beepService?.Beep(BeepType.Prepare);
		}
		else
		{
			beepService?.Beep(intervalType == ErgProgramIntervalType.BuildToMaxEffort ? BeepType.Go : BeepType.Stop);
		}
    }
}