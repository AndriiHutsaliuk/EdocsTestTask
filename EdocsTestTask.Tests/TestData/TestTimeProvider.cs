namespace EdocsTestTask.Tests.TestData
{
    /// <summary>
    /// Deterministic clock for tests. Only advances when told to.
    /// </summary>
    public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
    {
        #region Fields

        private DateTimeOffset _utcNow = start;

        #endregion

        #region Methods

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);

        #endregion
    }
}
