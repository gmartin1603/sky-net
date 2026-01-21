using Microsoft.JSInterop;

namespace SkyNet.Simulator.Dashboard.Services;

public sealed class ThemeService
{
	private readonly IJSRuntime _js;

	public ThemeService(IJSRuntime js)
	{
		_js = js;
	}

	public ValueTask<string?> GetAsync(CancellationToken cancellationToken = default)
		=> _js.InvokeAsync<string?>("skynetTheme.get", cancellationToken);

	public ValueTask<string> SetAsync(string theme, CancellationToken cancellationToken = default)
		=> _js.InvokeAsync<string>("skynetTheme.set", cancellationToken, theme);

	public ValueTask<string> ToggleAsync(CancellationToken cancellationToken = default)
		=> _js.InvokeAsync<string>("skynetTheme.toggle", cancellationToken);
}
