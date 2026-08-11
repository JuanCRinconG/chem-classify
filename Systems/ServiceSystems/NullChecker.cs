public static class NullChecker
{
	/// <summary>
	/// Checks whether all provided values are non-null.
	/// Each entry is a (value, name) pair. Use nameof() for the name at the call site.
	/// </summary>
	/// <returns>True if all values are non-null, false on the first null found.</returns>
	public static bool GroupNullChecker(params (object Value, string Name)[] entries)
	{
		bool result = true;

		if (entries.Length == 0) return true;

		foreach (var entry in entries)
		{
			if (entry.Value == null)
			{
				GD.PushWarning($"[NullChecker] '{entry.Name}' is null.");
				result = false;
			}
		}

		return result;
	}
}
