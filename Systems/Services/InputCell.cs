using Godot;

public sealed class InputCell<T>: InputHandler where T : struct
{
	public T Input;

    private volatile InputBox _published = new(default);

    public T Output => _published.Input;

    public void Write() => _published = new InputBox(Input);

	public void ResetInput() => _published = new InputBox(default);

    private sealed class InputBox
    {
        public readonly T Input;
        public InputBox(T value) => Input = value;
    }

	public bool TryAttachTo(Node entity)
	{
		if (entity is not InputReciever<T> targetEntity)
		{
			return false;
		}

		targetEntity.InputCell = this;
		return true;
	}
}

public interface InputReciever<T> where T : struct
{
    InputCell<T> InputCell { get; set; }
}

public interface InputHandler
{
	void Write();
	void ResetInput();
	bool TryAttachTo(Node entity);
}
