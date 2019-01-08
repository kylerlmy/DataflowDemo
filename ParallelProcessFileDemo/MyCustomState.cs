namespace ParallelProcessFileDemo
{
    internal class MyCustomState
    {
        public string StateData { get; internal set; }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}