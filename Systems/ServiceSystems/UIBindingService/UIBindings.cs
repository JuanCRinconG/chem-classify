using System;
using System.Collections.Generic;

/// <summary>Tracks UI signal/control subscriptions for deterministic teardown.</summary>
public sealed class UIBindings
{
	private readonly List<Action> _pendingBindings = new();

	public void Clear()
	{
		for (int i = _pendingBindings.Count - 1; i >= 0; i--)
		{
			_pendingBindings[i]();
		}

		_pendingBindings.Clear();
	}

	public void BindButton(Button button, Action handler)
	{
		button.Pressed += handler;
		_pendingBindings.Add(() => button.Pressed -= handler);
	}

	public void BindOptionButton(OptionButton button, Action<long> handler)
	{
		OptionButton.ItemSelectedEventHandler callback = item => handler(item);
		button.ItemSelected += callback;
		_pendingBindings.Add(() => button.ItemSelected -= callback);
	}

	public void BindCheckBox(CheckBox button, Action<bool> handler)
	{
		CheckBox.ToggledEventHandler callback = pressed => handler(pressed);
		button.Toggled += callback;
		_pendingBindings.Add(() => button.Toggled -= callback);
	}

	public void BindSignal(GodotObject source, StringName signalName, Callable callback)
	{
		if (source.Connect(signalName, callback) != Error.Ok)
		{
			return;
		}

		_pendingBindings.Add(() =>
		{
			if (GodotObject.IsInstanceValid(source)
				&& source.IsConnected(signalName, callback))
			{
				source.Disconnect(signalName, callback);
			}
		});
	}

	public void Bind<THandler>(Action<THandler> subscribe, Action<THandler> unsubscribe, THandler handler)
	{
		subscribe(handler);
		_pendingBindings.Add(() => unsubscribe(handler));
	}
}