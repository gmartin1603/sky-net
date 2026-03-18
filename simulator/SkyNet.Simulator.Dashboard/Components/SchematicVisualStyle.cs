namespace SkyNet.Simulator.Dashboard.Components;

public sealed record SchematicVisualStyle(
	string ShellFill,
	string ShellStroke,
	string CoreFill,
	string DetailStroke,
	string BaseFill,
	string PortCapFill,
	string TankFill,
	string AlertFill,
	string PanelFill,
	string PanelBorder,
	string PanelDivider,
	string TitleColor,
	string MutedColor,
	string ValueColor)
{
	public static SchematicVisualStyle Default { get; } = new(
		ShellFill: "var(--node-shell-fill)",
		ShellStroke: "var(--node-shell-stroke)",
		CoreFill: "var(--node-core-fill)",
		DetailStroke: "var(--node-detail-stroke)",
		BaseFill: "var(--node-base-fill)",
		PortCapFill: "var(--node-port-cap-fill)",
		TankFill: "var(--node-tank-fill)",
		AlertFill: "var(--node-alert)",
		PanelFill: "var(--node-panel-fill)",
		PanelBorder: "var(--node-border-off)",
		PanelDivider: "var(--node-panel-divider)",
		TitleColor: "var(--node-title-color)",
		MutedColor: "var(--node-muted-color)",
		ValueColor: "var(--node-value-color)");
}